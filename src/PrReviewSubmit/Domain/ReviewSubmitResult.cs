namespace PrReviewSubmit.Domain;

/// <summary>单次调用的结构化结果（success | error），供调用方程序化解析。</summary>
public sealed class ReviewSubmitResult
{
    public required string Status { get; init; }
    public long? ReviewId { get; init; }
    public string? HtmlUrl { get; init; }
    public ReviewSubmitErrorCode? Code { get; init; }
    public string? Message { get; init; }
    public int? HttpStatus { get; init; }
    public GitHubErrorDetails? Details { get; init; }

    public static ReviewSubmitResult Success(long reviewId, string htmlUrl) => new()
    {
        Status = "success",
        ReviewId = reviewId,
        HtmlUrl = htmlUrl,
    };

    public static ReviewSubmitResult Error(ReviewSubmitErrorCode code, string message, int? httpStatus = null, GitHubErrorDetails? details = null) => new()
    {
        Status = "error",
        Code = code,
        Message = message,
        HttpStatus = httpStatus,
        Details = details,
    };
}

/// <summary>错误详情（脱敏、截断至 2048 字符；retryable 供调用方分支）。</summary>
public sealed record GitHubErrorDetails(
    string? Message,
    IReadOnlyList<GitHubApiError>? Errors,
    int? RetryAfterSeconds,
    bool? Retryable);

/// <summary>GitHub 422/错误响应 errors 数组条目的白名单字段。</summary>
public sealed record GitHubApiError(string? Resource, string? Field, string? Code, string? Message);
