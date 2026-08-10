using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;

namespace PrReviewSubmit.GitHub;

/// <summary>
/// 三请求链路实现：令牌交换 → GET /pulls/{n}（仅 state/merged）→ POST create review（event=COMMENT）。
/// 无隐式重试（FR-010）；各请求超时由 HttpClient 控制（10s）。
/// </summary>
public sealed class GitHubReviewClient : IGitHubReviewClient
{
    private readonly HttpClient _http;
    private readonly GitHubAppAuthClient _auth;

    public GitHubReviewClient(HttpClient httpClient, GitHubAppOptions options)
    {
        _http = httpClient;
        _auth = new GitHubAppAuthClient(httpClient, options);
    }

    public Task<string> GetInstallationTokenAsync(string owner, string repo, CancellationToken cancellationToken)
        => _auth.GetInstallationTokenAsync(repo, cancellationToken);

    public async Task<PullRequestState> GetPullRequestStateAsync(
        string owner,
        string repo,
        int pullNumber,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"repos/{Escape(owner)}/{Escape(repo)}/pulls/{pullNumber}");
        ApplyHeaders(request, token);

        using var response = await SendAsync(request, GitHubRequestStage.PullState, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw GitHubErrorMapper.ToException(response, body, GitHubRequestStage.PullState);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("state", out var stateElement) ||
                stateElement.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("merged", out var mergedElement) ||
                mergedElement.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new GitHubRequestException(
                    ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                    "PR 状态响应缺少 state/merged 字段或类型异常",
                    200);
            }

            return new PullRequestState(stateElement.GetString()!, mergedElement.GetBoolean());
        }
        catch (JsonException)
        {
            throw new GitHubRequestException(ReviewSubmitErrorCode.UNEXPECTED_ERROR, "PR 状态响应不是有效 JSON", 200);
        }
    }

    public async Task<SubmittedReview> CreateReviewAsync(
        string owner,
        string repo,
        int pullNumber,
        string token,
        string body,
        IReadOnlyList<ReviewComment> comments,
        CancellationToken cancellationToken)
    {
        var payload = new
        {
            @event = "COMMENT",
            body,
            comments = comments
                .Select(c => new { path = c.Path, line = c.Line, side = c.Side, body = c.Body })
                .ToArray(),
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"repos/{Escape(owner)}/{Escape(repo)}/pulls/{pullNumber}/reviews");
        ApplyHeaders(request, token);
        request.Content = JsonContent.Create(payload);

        using var response = await SendAsync(request, GitHubRequestStage.Submit, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw GitHubErrorMapper.ToException(response, responseBody, GitHubRequestStage.Submit);

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.Number ||
                !root.TryGetProperty("html_url", out var urlElement) ||
                urlElement.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(urlElement.GetString()))
            {
                throw new GitHubRequestException(
                    ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                    "提交 review 成功响应缺少 id/html_url 或类型异常；review 可能已创建，请调用方核验目标 PR",
                    200);
            }

            var id = idElement.GetInt64();
            if (id <= 0)
            {
                throw new GitHubRequestException(
                    ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                    "提交 review 响应 id 非正整数；review 可能已创建，请调用方核验目标 PR",
                    200);
            }

            return new SubmittedReview(id, urlElement.GetString()!);
        }
        catch (GitHubRequestException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw new GitHubRequestException(
                ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                "提交 review 成功响应不是有效 JSON；review 可能已创建，请调用方核验目标 PR",
                200);
        }
    }

    private static string Escape(string value) => Uri.EscapeDataString(value);

    private static void ApplyHeaders(HttpRequestMessage request, string token)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.TryParseAdd(GitHubAppOptions.UserAgent);
        request.Headers.TryAddWithoutValidation(GitHubAppOptions.ApiVersionHeader, GitHubAppOptions.ApiVersion);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        GitHubRequestStage stage,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw GitHubErrorMapper.ToNetworkException(stage);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw GitHubErrorMapper.ToNetworkException(stage);
        }
    }
}
