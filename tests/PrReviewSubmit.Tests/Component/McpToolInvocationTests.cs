using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

public class McpToolInvocationTests
{
    [Fact]
    public async Task SubmitPrReview_SuccessPath_ReturnsSuccessJsonAndPassesPayloadThrough()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("test-installation-token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState("open", false));

        string? capturedBody = null;
        IReadOnlyList<ReviewComment>? capturedComments = null;
        string? capturedToken = null;
        stub.CreateReviewAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ReviewComment>>(),
                Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                capturedToken = ci.ArgAt<string>(3);
                capturedBody = ci.ArgAt<string>(4);
                capturedComments = ci.ArgAt<IReadOnlyList<ReviewComment>>(5);
                return new SubmittedReview(123456789, "https://github.com/octo/repo/pull/42#pullrequestreview-123456789");
            });

        var options = new GitHubAppOptions
        {
            AppId = 111111,
            InstallationId = 222222,
            PrivateKeyPath = "private-key/test.pem",
        };

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddSingleton(options);
        services.AddSingleton(stub);
        services.AddSingleton<ReviewSubmitTool>();
        services
            .AddMcpServer()
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(ReviewSubmitTool).Assembly);

        await using var provider = services.BuildServiceProvider();
        await using var server = provider.GetRequiredService<McpServer>();
        var serverTask = server.RunAsync(TestContext.Current.CancellationToken);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()),
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await client.CallToolAsync(
            "submit_pr_review",
            new Dictionary<string, object?>
            {
                ["owner"] = "octo",
                ["repo"] = "repo",
                ["pullNumber"] = 42,
                ["body"] = "整体审查结论",
                ["comments"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["path"] = "README.md",
                        ["line"] = 3,
                        ["side"] = "RIGHT",
                        ["body"] = "这里需要修改",
                    },
                },
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var text = result.Content.OfType<TextContentBlock>().First().Text;
        using var json = JsonDocument.Parse(text);
        var root = json.RootElement;
        Assert.Equal("success", root.GetProperty("status").GetString());
        Assert.Equal(123456789, root.GetProperty("reviewId").GetInt64());
        Assert.Equal(
            "https://github.com/octo/repo/pull/42#pullrequestreview-123456789",
            root.GetProperty("htmlUrl").GetString());

        Assert.Equal("test-installation-token", capturedToken);
        Assert.Equal("整体审查结论", capturedBody);
        Assert.NotNull(capturedComments);
        var comment = Assert.Single(capturedComments!);
        Assert.Equal("README.md", comment.Path);
        Assert.Equal(3, comment.Line);
        Assert.Equal("RIGHT", comment.Side);
        Assert.Equal("这里需要修改", comment.Body);

        await client.DisposeAsync();
        clientToServer.Writer.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }
}
