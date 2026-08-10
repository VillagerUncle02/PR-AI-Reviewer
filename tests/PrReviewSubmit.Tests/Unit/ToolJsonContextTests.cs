using System.Text.Json;
using PrReviewSubmit.Domain;
using PrReviewSubmit.Json;

namespace PrReviewSubmit.Tests.Unit;

public class ToolJsonContextTests
{
    [Fact]
    public void SuccessResult_SerializesCamelCaseWithExpectedFields()
    {
        var json = JsonSerializer.Serialize(
            ReviewSubmitResult.Success(123456789, "https://github.com/octo/repo/pull/42#pullrequestreview-123456789"),
            ToolJsonContext.Default.ReviewSubmitResult);

        Assert.Equal(
            """{"status":"success","reviewId":123456789,"htmlUrl":"https://github.com/octo/repo/pull/42#pullrequestreview-123456789"}""",
            json);
    }

    [Fact]
    public void ErrorResult_SerializesCodeAsContractString()
    {
        var json = JsonSerializer.Serialize(
            ReviewSubmitResult.Error(ReviewSubmitErrorCode.INVALID_PAYLOAD, "载荷校验失败"),
            ToolJsonContext.Default.ReviewSubmitResult);

        Assert.Contains("\"status\":\"error\"", json);
        Assert.Contains("\"code\":\"INVALID_PAYLOAD\"", json);
        Assert.DoesNotContain("\"code\":", json.Replace("\"code\":\"INVALID_PAYLOAD\"", string.Empty));
    }

    [Fact]
    public void ErrorResult_OmitsNullOptionalFields()
    {
        var json = JsonSerializer.Serialize(
            ReviewSubmitResult.Error(ReviewSubmitErrorCode.PR_NOT_OPEN, "目标 PR 不是 open 状态"),
            ToolJsonContext.Default.ReviewSubmitResult);

        Assert.DoesNotContain("\"httpStatus\":null", json);
        Assert.DoesNotContain("\"details\":null", json);
        Assert.DoesNotContain("\"reviewId\":null", json);
        Assert.DoesNotContain("\"htmlUrl\":null", json);
    }
}
