namespace PrReviewSubmit.Domain;

/// <summary>submit_pr_review 工具入参：显式目标 + 整体结论 + 逐文件评论。</summary>
public sealed class ReviewSubmitRequest
{
    public required string Owner { get; init; }
    public required string Repo { get; init; }
    public required int PullNumber { get; init; }
    public required string Body { get; init; }
    public required IReadOnlyList<ReviewComment> Comments { get; init; }
}

/// <summary>逐文件行内评论（提交时原样透传，FR-005）。</summary>
public sealed class ReviewComment
{
    public required string Path { get; init; }
    public required int Line { get; init; }

    /// <summary>RIGHT（新文件侧）或 LEFT（旧文件侧），大小写敏感。</summary>
    public required string Side { get; init; }

    public required string Body { get; init; }
}
