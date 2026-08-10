using PrReviewSubmit.Domain;

namespace PrReviewSubmit.Validation;

/// <summary>
/// FR-002/FR-003 本地载荷校验：在任何 GitHub 请求之前完成；trim 仅用于有效性判断，
/// 提交时保持调用方原始内容（FR-005）。
/// </summary>
public static class ReviewPayloadValidator
{
    public static (bool IsValid, string? ErrorMessage) Validate(ReviewSubmitRequest? request)
    {
        if (request is null)
            return (false, "载荷缺失");
        if (string.IsNullOrWhiteSpace(request.Owner))
            return (false, "owner 必须为去除首尾空白后非空的字符串");
        if (string.IsNullOrWhiteSpace(request.Repo))
            return (false, "repo 必须为去除首尾空白后非空的字符串");
        if (request.PullNumber < 1)
            return (false, "pullNumber 必须为大于等于 1 的整数");
        if (string.IsNullOrWhiteSpace(request.Body))
            return (false, "body（整体结论）必须为去除首尾空白后非空的字符串");
        if (request.Comments is null)
            return (false, "comments 必须为数组（可为空数组），不允许 null");

        for (var i = 0; i < request.Comments.Count; i++)
        {
            var comment = request.Comments[i];
            if (comment is null)
                return (false, $"comments[{i}] 不能为 null");
            if (string.IsNullOrEmpty(comment.Path))
                return (false, $"comments[{i}].path 必须为非空字符串");
            if (comment.Line < 1)
                return (false, $"comments[{i}].line 必须为大于等于 1 的整数");
            if (comment.Side is not ("RIGHT" or "LEFT"))
                return (false, $"comments[{i}].side 必须为 RIGHT 或 LEFT");
            if (string.IsNullOrWhiteSpace(comment.Body))
                return (false, $"comments[{i}].body 必须为去除首尾空白后非空的字符串");
        }

        return (true, null);
    }
}
