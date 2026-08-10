using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.Json;
using PrReviewSubmit.Validation;

namespace PrReviewSubmit.MCP;

/// <summary>
/// MCP 工具类型：submit_pr_review（本项目唯一工具，FR-001）。
/// 仅负责把 Agent 已完成的 PR 审查结论经 GitHub App 渠道上传，不做任何其他产出。
/// </summary>
[McpServerToolType]
public sealed class ReviewSubmitTool
{
    private readonly IGitHubReviewClient _client;

    public ReviewSubmitTool(IGitHubReviewClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 以 GitHub App bot 身份把整体结论（body）与逐文件评论（comments）
    /// 作为一条已提交 review（event=COMMENT）上传到指定仓库的指定 PR。
    /// </summary>
    [McpServerTool(Name = "submit_pr_review", Title = "上传 AI PR 审查结论")]
    [Description("以 GitHub App bot 身份将 AI 完成的 PR 审查结论（整体结论 + 逐文件评论）通过 submit review 上传到指定仓库的指定 PR。")]
    public async Task<CallToolResult> SubmitPrReviewAsync(
        [Description("目标账号（owner），trim 后非空，无默认值")] string owner,
        [Description("目标仓库名（不含 .git 后缀），trim 后非空")] string repo,
        [Description("目标 PR 编号，大于等于 1")] int pullNumber,
        [Description("整体审查结论，trim 后非空")] string body,
        [Description("逐文件行内评论数组（可为空数组，不允许 null）")] IReadOnlyList<ReviewComment> comments,
        CancellationToken cancellationToken)
    {
        var request = new ReviewSubmitRequest
        {
            Owner = owner,
            Repo = repo,
            PullNumber = pullNumber,
            Body = body,
            Comments = comments,
        };

        var (isValid, errorMessage) = ReviewPayloadValidator.Validate(request);
        if (!isValid)
        {
            return ToResult(ReviewSubmitResult.Error(
                ReviewSubmitErrorCode.INVALID_PAYLOAD,
                errorMessage ?? "载荷校验失败"));
        }

        try
        {
            var token = await _client.GetInstallationTokenAsync(owner, repo, cancellationToken);
            var state = await _client.GetPullRequestStateAsync(owner, repo, pullNumber, token, cancellationToken);
            if (!IsOpen(state))
            {
                return ToResult(ReviewSubmitResult.Error(
                    ReviewSubmitErrorCode.PR_NOT_OPEN,
                    "目标 PR 不是 open 状态或已合并，无法提交 review"));
            }

            var review = await _client.CreateReviewAsync(
                owner, repo, pullNumber, token, body, comments, cancellationToken);
            return ToResult(ReviewSubmitResult.Success(review.Id, review.HtmlUrl));
        }
        catch (GitHubRequestException ex)
        {
            return ToResult(ReviewSubmitResult.Error(ex.Code, ex.Message, ex.HttpStatus, ex.Details));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return ToResult(ReviewSubmitResult.Error(
                ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                "未预期的内部错误，请重试；若持续失败请检查工具运行环境"));
        }
    }

    private static bool IsOpen(PullRequestState state)
        => string.Equals(state.State, "open", StringComparison.OrdinalIgnoreCase) && !state.Merged;

    private static CallToolResult ToResult(ReviewSubmitResult result)
    {
        var json = JsonSerializer.Serialize(result, ToolJsonContext.Default.ReviewSubmitResult);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = json }],
            IsError = result.Status == "error",
        };
    }
}
