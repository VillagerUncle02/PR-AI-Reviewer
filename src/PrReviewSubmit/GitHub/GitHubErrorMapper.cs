using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using PrReviewSubmit.Domain;
using PrReviewSubmit.Json;

namespace PrReviewSubmit.GitHub;

/// <summary>请求阶段（影响 404 的错误码归属，CHK124）。</summary>
public enum GitHubRequestStage
{
    Auth,
    PullState,
    Submit,
}

/// <summary>映射后的结构化错误。</summary>
public sealed record MappedError(
    ReviewSubmitErrorCode Code,
    string Message,
    int? HttpStatus,
    GitHubErrorDetails? Details);

/// <summary>携带结构化错误码的请求异常，供编排层转换为结果 JSON。</summary>
public sealed class GitHubRequestException : Exception
{
    public GitHubRequestException(
        ReviewSubmitErrorCode code,
        string message,
        int? httpStatus = null,
        GitHubErrorDetails? details = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        HttpStatus = httpStatus;
        Details = details;
    }

    public ReviewSubmitErrorCode Code { get; }
    public int? HttpStatus { get; }
    public GitHubErrorDetails? Details { get; }

    public static GitHubRequestException FromMapped(MappedError error)
        => new(error.Code, error.Message, error.HttpStatus, error.Details);
}

/// <summary>
/// 状态码 → 错误码映射（tool-contract.md / github-rest.md）；details 脱敏并截断至 2048 字符。
/// </summary>
public static class GitHubErrorMapper
{
    private const int MaxDetailsLength = 2048;
    private const int MaxMessageLength = 512;
    private static readonly JsonSerializerOptions MeasurementOptions = new(JsonSerializerDefaults.Web)
    {
        // 按 Unicode 字符计量（CHK107/CHK154）：非 ASCII 字符不被转义成 \uXXXX。
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static MappedError MapResponse(HttpStatusCode status, string? body, GitHubRequestStage stage, int? retryAfterSeconds = null)
    {
        var code = (int)status;
        var (errorCode, message) = status switch
        {
            HttpStatusCode.Unauthorized => (
                ReviewSubmitErrorCode.CREDENTIALS_INVALID,
                "凭据无效：GitHub 拒绝了 JWT 或安装令牌（401），请检查私钥与系统时钟偏差后由调用方重试"),
            HttpStatusCode.Forbidden => (
                ReviewSubmitErrorCode.APP_NOT_INSTALLED,
                "GitHub App 未安装到目标仓库或权限不足（403），请检查 App 安装与 Pull requests Read & Write 权限"),
            HttpStatusCode.NotFound when stage == GitHubRequestStage.Auth => (
                ReviewSubmitErrorCode.APP_NOT_INSTALLED,
                "GitHub App 安装不存在或目标仓库不在安装授权范围（404），请检查安装 ID 与仓库授权"),
            HttpStatusCode.NotFound => (
                ReviewSubmitErrorCode.TARGET_NOT_FOUND,
                "目标仓库或 PR 不存在，或无访问权限（404），请核对 owner/repo/pullNumber"),
            HttpStatusCode.UnprocessableEntity => (
                ReviewSubmitErrorCode.REVIEW_UNPROCESSABLE,
                "GitHub 拒绝提交：评论可能不在目标 PR 的 file change 范围内，或数量/长度超限（422），请修正后重试"),
            HttpStatusCode.TooManyRequests => (
                ReviewSubmitErrorCode.RATE_LIMITED,
                "GitHub 限流（429），请按 details.retryAfterSeconds 等待后由调用方重试"),
            _ when code >= 500 => (
                ReviewSubmitErrorCode.NETWORK_ERROR,
                $"GitHub 服务端错误（{code}），请稍后由调用方重试"),
            _ => (
                ReviewSubmitErrorCode.UNEXPECTED_ERROR,
                $"GitHub 返回了未预期的状态码（{code}）"),
        };

        var retryable = errorCode is ReviewSubmitErrorCode.RATE_LIMITED or ReviewSubmitErrorCode.NETWORK_ERROR;
        var details = TruncateDetails(BuildDetails(body, retryAfterSeconds, retryable));
        return new MappedError(errorCode, TruncateMessage(message), code, details);
    }

    public static MappedError MapNetwork(GitHubRequestStage stage)
    {
        var message = stage == GitHubRequestStage.Submit
            ? "网络错误或请求超时：提交请求可能已发出，请先核验目标 PR 是否已创建 review；若未创建可由调用方重试"
            : "网络错误或请求超时，请检查网络后由调用方重试";
        return new MappedError(
            ReviewSubmitErrorCode.NETWORK_ERROR,
            message,
            null,
            new GitHubErrorDetails(null, null, null, true));
    }

    public static MappedError MapUnexpected(string message, int? httpStatus = null, GitHubErrorDetails? details = null)
        => new(ReviewSubmitErrorCode.UNEXPECTED_ERROR, TruncateMessage(message), httpStatus, details);

    public static GitHubRequestException ToException(HttpResponseMessage response, string body, GitHubRequestStage stage)
    {
        var retryAfter = TryGetRetryAfterSeconds(response);
        return GitHubRequestException.FromMapped(MapResponse(response.StatusCode, body, stage, retryAfter));
    }

    public static GitHubRequestException ToNetworkException(GitHubRequestStage stage)
        => GitHubRequestException.FromMapped(MapNetwork(stage));

    private static GitHubErrorDetails? BuildDetails(string? body, int? retryAfterSeconds, bool retryable)
    {
        string? message = null;
        var errors = new List<GitHubApiError>();

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        message = m.GetString();
                    if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in e.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object)
                                continue;
                            errors.Add(new GitHubApiError(
                                GetStringProperty(item, "resource"),
                                GetStringProperty(item, "field"),
                                GetStringProperty(item, "code"),
                                GetStringProperty(item, "message")));
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // 响应体非 JSON：不附带非结构化详情
            }
        }

        return new GitHubErrorDetails(
            message,
            errors.Count > 0 ? errors : null,
            retryAfterSeconds,
            retryable);
    }

    private static GitHubErrorDetails? TruncateDetails(GitHubErrorDetails? details)
    {
        if (details is null)
            return null;

        var errors = details.Errors?.ToList() ?? [];
        while (errors.Count > 0 && JsonLength(details with { Message = null, Errors = errors }) > MaxDetailsLength)
            errors.RemoveAt(errors.Count - 1);

        var truncated = details with { Errors = errors.Count > 0 ? errors : null };
        if (truncated.Message is not null)
        {
            var baseLength = JsonLength(truncated with { Message = string.Empty });
            var maxMessageChars = MaxDetailsLength - baseLength - 2; // 前后引号
            truncated = maxMessageChars > 0
                ? truncated with { Message = TruncateText(truncated.Message, maxMessageChars) }
                : truncated with { Message = null };
        }

        return JsonLength(truncated) > MaxDetailsLength
            ? truncated with { Message = null, Errors = null }
            : truncated;
    }

    private static int JsonLength(GitHubErrorDetails details)
    {
        try
        {
            return JsonSerializer.Serialize(details, MeasurementOptions).Length;
        }
        catch
        {
            return int.MaxValue;
        }
    }

    private static int? TryGetRetryAfterSeconds(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Retry-After", out var values))
            return null;
        var raw = values.FirstOrDefault();
        if (raw is null)
            return null;
        if (int.TryParse(raw, out var seconds))
            return seconds;
        if (DateTimeOffset.TryParse(raw, out var when))
            return Math.Max(0, (int)(when - DateTimeOffset.UtcNow).TotalSeconds);
        return null;
    }

    private static string? GetStringProperty(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string TruncateMessage(string message)
        => message.Length <= MaxMessageLength ? message : TruncateText(message, MaxMessageLength);

    private static string TruncateText(string text, int maxChars)
    {
        if (text.Length <= maxChars)
            return text;
        var span = text.AsSpan(0, Math.Max(0, maxChars - 1));
        var length = span.Length;
        if (length > 0 && char.IsHighSurrogate(span[length - 1]))
            length--;
        return new string(span[..length]) + "…";
    }
}
