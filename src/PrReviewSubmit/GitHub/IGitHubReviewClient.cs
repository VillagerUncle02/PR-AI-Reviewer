using PrReviewSubmit.Domain;

namespace PrReviewSubmit.GitHub;

/// <summary>
/// GitHub 交互薄接口（可替身测试）：令牌交换 → PR 状态读取 → 单次提交 review。
/// </summary>
public interface IGitHubReviewClient
{
    Task<string> GetInstallationTokenAsync(string owner, string repo, CancellationToken cancellationToken);
    Task<PullRequestState> GetPullRequestStateAsync(string owner, string repo, int pullNumber, string token, CancellationToken cancellationToken);
    Task<SubmittedReview> CreateReviewAsync(
        string owner,
        string repo,
        int pullNumber,
        string token,
        string body,
        IReadOnlyList<ReviewComment> comments,
        CancellationToken cancellationToken);
}

/// <summary>提交前读取到的目标 PR 状态（仅消费 state/merged，FR-004/FR-014）。</summary>
public sealed record PullRequestState(string State, bool Merged);

/// <summary>GitHub 平台已提交 review 的最小产物（id/html_url 透传）。</summary>
public sealed record SubmittedReview(long Id, string HtmlUrl);
