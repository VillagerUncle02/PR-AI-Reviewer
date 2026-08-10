using System.IO.Pipelines;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Smoke;

/// <summary>冒烟场景串行执行，避免多个场景并发读写同一 PR 的 reviews 列表。</summary>
[CollectionDefinition("Smoke", DisableParallelization = true)]
public sealed class SmokeCollectionDefinition;

/// <summary>
/// 可选端到端冒烟测试（quickstart 场景 A/B/C/D，真实 GitHub App 上传）。
/// 未设置 GITHUB_APP_ID / GITHUB_APP_INSTALLATION_ID / GITHUB_PRIVATE_KEY_PATH 时自动跳过；
/// 设置后还需 GITHUB_SMOKE_OWNER / GITHUB_SMOKE_REPO / GITHUB_SMOKE_PR_NUMBER，
/// 以及场景 A 需要的 GITHUB_SMOKE_PATH / GITHUB_SMOKE_LINE / GITHUB_SMOKE_SIDE。
/// </summary>
[Collection("Smoke")]
public class SmokeTests
{
    [Fact]
    public async Task ScenarioA_RealUpload_ReadBackMatchesAndIsBot()
    {
        var config = GetScenarioConfigOrSkip();
        await using var harness = await CreateHarnessAsync(
            config.AppOptions,
            TestContext.Current.CancellationToken);

        var marker = $"smoke-a-{Guid.NewGuid():N}";
        var body = $"整体审查结论 {marker}";
        var commentBody = $"逐行评论 {marker}";
        var arguments = BuildArguments(
            config.Owner,
            config.Repo,
            config.PullNumber,
            body,
            new object[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = config.CommentPath,
                    ["line"] = config.CommentLine,
                    ["side"] = config.CommentSide,
                    ["body"] = commentBody,
                },
            });

        var root = await CallToolAsync(harness.Client, arguments, TestContext.Current.CancellationToken);
        Assert.Equal("success", root.GetProperty("status").GetString());
        var reviewId = root.GetProperty("reviewId").GetInt64();
        Assert.True(reviewId > 0);

        var token = await harness.GitHub.GetInstallationTokenAsync(
            config.Owner,
            config.Repo,
            TestContext.Current.CancellationToken);
        var review = await GetJsonObjectAsync(
            harness.Http,
            $"repos/{config.Owner}/{config.Repo}/pulls/{config.PullNumber}/reviews/{reviewId}",
            token,
            TestContext.Current.CancellationToken);
        Assert.Equal(body, review.GetProperty("body").GetString());
        Assert.Equal("Bot", review.GetProperty("user").GetProperty("type").GetString());

        var comments = await GetJsonArrayAsync(
            harness.Http,
            $"repos/{config.Owner}/{config.Repo}/pulls/{config.PullNumber}/comments?review_id={reviewId}",
            token,
            TestContext.Current.CancellationToken);
        var comment = Assert.Single(comments.EnumerateArray());
        Assert.Equal(config.CommentPath, comment.GetProperty("path").GetString());
        Assert.Equal(config.CommentLine, comment.GetProperty("line").GetInt32());
        Assert.Equal(config.CommentSide, comment.GetProperty("side").GetString());
        Assert.Equal(commentBody, comment.GetProperty("body").GetString());
    }

    [Fact]
    public async Task ScenarioB_OutOfRangeComment_FailsWholeReviewWithoutNewReview()
    {
        var config = GetScenarioConfigOrSkip();
        await using var harness = await CreateHarnessAsync(
            config.AppOptions,
            TestContext.Current.CancellationToken);

        var token = await harness.GitHub.GetInstallationTokenAsync(
            config.Owner,
            config.Repo,
            TestContext.Current.CancellationToken);
        var reviewsUri = $"repos/{config.Owner}/{config.Repo}/pulls/{config.PullNumber}/reviews";
        var before = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);

        var arguments = BuildArguments(
            config.Owner,
            config.Repo,
            config.PullNumber,
            "越界评论场景",
            new object[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = $"smoke-nonexistent-{Guid.NewGuid():N}.txt",
                    ["line"] = 1,
                    ["side"] = "RIGHT",
                    ["body"] = "不存在的文件评论",
                },
            });
        var root = await CallToolAsync(harness.Client, arguments, TestContext.Current.CancellationToken);

        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("REVIEW_UNPROCESSABLE", root.GetProperty("code").GetString());

        var after = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);
        Assert.Equal(before.EnumerateArray().Count(), after.EnumerateArray().Count());
    }

    [Fact]
    public async Task ScenarioC_InvalidPayload_RejectedLocallyWithoutAnyRequest()
    {
        var config = GetScenarioConfigOrSkip();
        await using var harness = await CreateHarnessAsync(
            config.AppOptions,
            TestContext.Current.CancellationToken);

        var requestsBefore = harness.RequestCount;
        var arguments = BuildArguments(
            config.Owner,
            config.Repo,
            config.PullNumber,
            "   ",
            new object[]
            {
                new Dictionary<string, object?>
                {
                    ["path"] = "README.md",
                    ["line"] = 1,
                    ["side"] = "RIGHT",
                    ["body"] = "   ",
                },
            });
        var root = await CallToolAsync(harness.Client, arguments, TestContext.Current.CancellationToken);

        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("INVALID_PAYLOAD", root.GetProperty("code").GetString());

        // FR-003：本地校验拒绝，未向 GitHub 发起任何请求。
        Assert.Equal(requestsBefore, harness.RequestCount);
    }

    [Fact]
    public async Task ScenarioD_NonexistentPullRequest_ReturnsTargetNotFound()
    {
        var config = GetScenarioConfigOrSkip();
        await using var harness = await CreateHarnessAsync(
            config.AppOptions,
            TestContext.Current.CancellationToken);

        var token = await harness.GitHub.GetInstallationTokenAsync(
            config.Owner,
            config.Repo,
            TestContext.Current.CancellationToken);
        var reviewsUri = $"repos/{config.Owner}/{config.Repo}/pulls/{config.PullNumber}/reviews";
        var before = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);

        var arguments = BuildArguments(
            config.Owner,
            config.Repo,
            int.MaxValue,
            "目标不存在场景",
            Array.Empty<object>());
        var root = await CallToolAsync(harness.Client, arguments, TestContext.Current.CancellationToken);

        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("TARGET_NOT_FOUND", root.GetProperty("code").GetString());

        var after = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);
        Assert.Equal(before.EnumerateArray().Count(), after.EnumerateArray().Count());
    }

    [Fact]
    public async Task ScenarioD_ClosedOrMergedPullRequest_ReturnsPrNotOpen()
    {
        var config = GetScenarioConfigOrSkip();
        await using var harness = await CreateHarnessAsync(
            config.AppOptions,
            TestContext.Current.CancellationToken);

        var token = await harness.GitHub.GetInstallationTokenAsync(
            config.Owner,
            config.Repo,
            TestContext.Current.CancellationToken);
        var closedPulls = await GetJsonArrayAsync(
            harness.Http,
            $"repos/{config.Owner}/{config.Repo}/pulls?state=closed&per_page=1",
            token,
            TestContext.Current.CancellationToken);
        var closedPull = closedPulls.EnumerateArray().FirstOrDefault();
        if (closedPull.ValueKind == JsonValueKind.Undefined)
            Assert.Skip("目标仓库没有已关闭/已合并 PR，跳过 PR_NOT_OPEN 子场景");

        var closedPullNumber = closedPull.GetProperty("number").GetInt32();
        var reviewsUri = $"repos/{config.Owner}/{config.Repo}/pulls/{closedPullNumber}/reviews";
        var before = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);

        var arguments = BuildArguments(
            config.Owner,
            config.Repo,
            closedPullNumber,
            "目标已关闭场景",
            Array.Empty<object>());
        var root = await CallToolAsync(harness.Client, arguments, TestContext.Current.CancellationToken);

        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("PR_NOT_OPEN", root.GetProperty("code").GetString());

        var after = await GetJsonArrayAsync(
            harness.Http,
            reviewsUri,
            token,
            TestContext.Current.CancellationToken);
        Assert.Equal(before.EnumerateArray().Count(), after.EnumerateArray().Count());
    }

    private static SmokeScenarioConfig GetScenarioConfigOrSkip()
    {
        var appIdText = Environment.GetEnvironmentVariable("GITHUB_APP_ID");
        var installationIdText = Environment.GetEnvironmentVariable("GITHUB_APP_INSTALLATION_ID");
        var privateKeyPath = Environment.GetEnvironmentVariable("GITHUB_PRIVATE_KEY_PATH");

        if (string.IsNullOrWhiteSpace(appIdText)
            || string.IsNullOrWhiteSpace(installationIdText)
            || string.IsNullOrWhiteSpace(privateKeyPath))
        {
            Assert.Skip("未设置 GITHUB_APP_ID / GITHUB_APP_INSTALLATION_ID / GITHUB_PRIVATE_KEY_PATH，跳过真实 GitHub App 冒烟测试");
        }

        if (!long.TryParse(appIdText, out var appId) || appId <= 0)
            Assert.Skip($"GITHUB_APP_ID 不是正整数：{appIdText}");
        if (!long.TryParse(installationIdText, out var installationId) || installationId <= 0)
            Assert.Skip($"GITHUB_APP_INSTALLATION_ID 不是正整数：{installationIdText}");
        if (!File.Exists(privateKeyPath))
            Assert.Skip($"GITHUB_PRIVATE_KEY_PATH 文件不存在：{privateKeyPath}");

        var owner = Environment.GetEnvironmentVariable("GITHUB_SMOKE_OWNER");
        var repo = Environment.GetEnvironmentVariable("GITHUB_SMOKE_REPO");
        var pullNumberText = Environment.GetEnvironmentVariable("GITHUB_SMOKE_PR_NUMBER");
        if (string.IsNullOrWhiteSpace(owner)
            || string.IsNullOrWhiteSpace(repo)
            || string.IsNullOrWhiteSpace(pullNumberText))
        {
            Assert.Skip("未设置 GITHUB_SMOKE_OWNER / GITHUB_SMOKE_REPO / GITHUB_SMOKE_PR_NUMBER，缺少冒烟目标仓库信息");
        }

        if (!int.TryParse(pullNumberText, out var pullNumber) || pullNumber <= 0)
            Assert.Skip($"GITHUB_SMOKE_PR_NUMBER 不是正整数：{pullNumberText}");

        var commentPath = Environment.GetEnvironmentVariable("GITHUB_SMOKE_PATH");
        var commentLineText = Environment.GetEnvironmentVariable("GITHUB_SMOKE_LINE");
        var commentSide = Environment.GetEnvironmentVariable("GITHUB_SMOKE_SIDE");
        if (string.IsNullOrWhiteSpace(commentPath))
            Assert.Skip("未设置 GITHUB_SMOKE_PATH（场景 A 需要真实改动文件路径）");
        var commentLine = 0;
        if (string.IsNullOrWhiteSpace(commentLineText)
            || !int.TryParse(commentLineText, out commentLine)
            || commentLine <= 0)
        {
            Assert.Skip($"GITHUB_SMOKE_LINE 不是正整数：{commentLineText}");
        }

        if (string.IsNullOrWhiteSpace(commentSide) || commentSide is not ("LEFT" or "RIGHT"))
            Assert.Skip($"GITHUB_SMOKE_SIDE 必须是 LEFT 或 RIGHT：{commentSide}");

        return new SmokeScenarioConfig(
            new GitHubAppOptions
            {
                AppId = appId,
                InstallationId = installationId,
                PrivateKeyPath = privateKeyPath,
            },
            owner,
            repo,
            pullNumber,
            commentPath,
            commentLine,
            commentSide);
    }

    private static Dictionary<string, object?> BuildArguments(
        string owner,
        string repo,
        int pullNumber,
        string body,
        IReadOnlyList<object> comments)
        => new()
        {
            ["owner"] = owner,
            ["repo"] = repo,
            ["pullNumber"] = pullNumber,
            ["body"] = body,
            ["comments"] = comments,
        };

    private static async Task<JsonElement> CallToolAsync(
        McpClient client,
        Dictionary<string, object?> arguments,
        CancellationToken cancellationToken)
    {
        var result = await client.CallToolAsync(
            "submit_pr_review",
            arguments,
            cancellationToken: cancellationToken);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    private static async Task<SmokeHarness> CreateHarnessAsync(
        GitHubAppOptions options,
        CancellationToken cancellationToken)
    {
        var countingHandler = new CountingHandler(new SocketsHttpHandler());
        var http = new HttpClient(countingHandler)
        {
            BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        };
        var github = new GitHubReviewClient(http, options);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton<IGitHubReviewClient>(github);
        services.AddSingleton<ReviewSubmitTool>();
        services
            .AddMcpServer()
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(ReviewSubmitTool).Assembly);

        var provider = services.BuildServiceProvider();
        var server = provider.GetRequiredService<McpServer>();
        var serverTask = server.RunAsync(cancellationToken);
        var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()),
            cancellationToken: cancellationToken);

        return new SmokeHarness(
            provider,
            server,
            serverTask,
            clientToServer,
            http,
            countingHandler,
            client,
            github);
    }

    private static async Task<JsonElement> GetJsonObjectAsync(
        HttpClient http,
        string requestUri,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(
            response.IsSuccessStatusCode,
            $"GET {requestUri} 失败：{(int)response.StatusCode} {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> GetJsonArrayAsync(
        HttpClient http,
        string requestUri,
        string token,
        CancellationToken cancellationToken)
    {
        var root = await GetJsonObjectAsync(http, requestUri, token, cancellationToken);
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        return root;
    }

    private sealed record SmokeScenarioConfig(
        GitHubAppOptions AppOptions,
        string Owner,
        string Repo,
        int PullNumber,
        string CommentPath,
        int CommentLine,
        string CommentSide);

    private sealed class SmokeHarness : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly McpServer _server;
        private readonly Task _serverTask;
        private readonly Pipe _clientToServer;
        private readonly HttpClient _http;
        private readonly CountingHandler _countingHandler;

        public SmokeHarness(
            ServiceProvider provider,
            McpServer server,
            Task serverTask,
            Pipe clientToServer,
            HttpClient http,
            CountingHandler countingHandler,
            McpClient client,
            GitHubReviewClient github)
        {
            _provider = provider;
            _server = server;
            _serverTask = serverTask;
            _clientToServer = clientToServer;
            _http = http;
            _countingHandler = countingHandler;
            Client = client;
            GitHub = github;
        }

        public McpClient Client { get; }

        public GitHubReviewClient GitHub { get; }

        public HttpClient Http => _http;

        public int RequestCount => _countingHandler.RequestCount;

        public async ValueTask DisposeAsync()
        {
            await Client.DisposeAsync();
            _clientToServer.Writer.Complete();
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(15), CancellationToken.None);
            await _server.DisposeAsync();
            await _provider.DisposeAsync();
            _http.Dispose();
        }
    }

    private sealed class CountingHandler(HttpMessageHandler innerHandler) : DelegatingHandler(innerHandler)
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
