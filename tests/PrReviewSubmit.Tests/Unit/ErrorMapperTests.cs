using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.Json;

namespace PrReviewSubmit.Tests.Unit;

public class ErrorMapperTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ReviewSubmitErrorCode.CREDENTIALS_INVALID)]
    [InlineData(HttpStatusCode.Forbidden, ReviewSubmitErrorCode.APP_NOT_INSTALLED)]
    [InlineData(HttpStatusCode.UnprocessableEntity, ReviewSubmitErrorCode.REVIEW_UNPROCESSABLE)]
    [InlineData(HttpStatusCode.TooManyRequests, ReviewSubmitErrorCode.RATE_LIMITED)]
    [InlineData(HttpStatusCode.InternalServerError, ReviewSubmitErrorCode.NETWORK_ERROR)]
    [InlineData(HttpStatusCode.BadGateway, ReviewSubmitErrorCode.NETWORK_ERROR)]
    public void MapResponse_MapsStatusCodes(HttpStatusCode status, ReviewSubmitErrorCode expected)
    {
        var mapped = GitHubErrorMapper.MapResponse(status, """{"message":"detail"}""", GitHubRequestStage.Submit);

        Assert.Equal(expected, mapped.Code);
        Assert.Equal((int)status, mapped.HttpStatus);
    }

    [Fact]
    public void MapResponse_NotFoundDuringAuth_MapsToAppNotInstalled()
    {
        var mapped = GitHubErrorMapper.MapResponse(
            HttpStatusCode.NotFound,
            """{"message":"not found"}""",
            GitHubRequestStage.Auth);

        Assert.Equal(ReviewSubmitErrorCode.APP_NOT_INSTALLED, mapped.Code);
    }

    [Fact]
    public void MapResponse_NotFoundDuringPullStateOrSubmit_MapsToTargetNotFound()
    {
        var pullState = GitHubErrorMapper.MapResponse(
            HttpStatusCode.NotFound,
            """{"message":"not found"}""",
            GitHubRequestStage.PullState);
        var submit = GitHubErrorMapper.MapResponse(
            HttpStatusCode.NotFound,
            """{"message":"not found"}""",
            GitHubRequestStage.Submit);

        Assert.Equal(ReviewSubmitErrorCode.TARGET_NOT_FOUND, pullState.Code);
        Assert.Equal(ReviewSubmitErrorCode.TARGET_NOT_FOUND, submit.Code);
    }

    [Fact]
    public void MapResponse_UnexpectedStatus_MapsToUnexpectedError()
    {
        var mapped = GitHubErrorMapper.MapResponse(
            (HttpStatusCode)418,
            null,
            GitHubRequestStage.Submit);

        Assert.Equal(ReviewSubmitErrorCode.UNEXPECTED_ERROR, mapped.Code);
        Assert.Equal(418, mapped.HttpStatus);
    }

    [Fact]
    public void MapResponse_RateLimited_SetsRetryableAndPassesRetryAfter()
    {
        var mapped = GitHubErrorMapper.MapResponse(
            HttpStatusCode.TooManyRequests,
            """{"message":"rate limited"}""",
            GitHubRequestStage.Auth,
            retryAfterSeconds: 37);

        Assert.Equal(ReviewSubmitErrorCode.RATE_LIMITED, mapped.Code);
        Assert.True(mapped.Details!.Retryable);
        Assert.Equal(37, mapped.Details.RetryAfterSeconds);
        Assert.Equal("rate limited", mapped.Details.Message);
    }

    [Fact]
    public void ToException_ParsesRetryAfterHeader()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("Retry-After", "42");

        var ex = GitHubErrorMapper.ToException(response, """{"message":"rate"}""", GitHubRequestStage.Submit);

        Assert.Equal(ReviewSubmitErrorCode.RATE_LIMITED, ex.Code);
        Assert.Equal(42, ex.Details!.RetryAfterSeconds);
        Assert.True(ex.Details.Retryable);
    }

    [Fact]
    public void MapResponse_NonRetryableErrors_HaveRetryableFalse()
    {
        var mapped = GitHubErrorMapper.MapResponse(
            HttpStatusCode.UnprocessableEntity,
            """{"message":"bad"}""",
            GitHubRequestStage.Submit);

        Assert.False(mapped.Details!.Retryable);
    }

    [Fact]
    public void MapResponse_TruncatesDetailsToJsonLengthLimit()
    {
        var longMessage = new string('长', 3000);
        var mapped = GitHubErrorMapper.MapResponse(
            HttpStatusCode.UnprocessableEntity,
            $$"""{"message":"{{longMessage}}"}""",
            GitHubRequestStage.Submit);

        var json = JsonSerializer.Serialize(mapped.Details, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });

        Assert.True(json.Length <= 2048);
        Assert.EndsWith("…", mapped.Details!.Message);
    }

    [Fact]
    public void MapNetwork_SubmitStage_WarnsReviewMayHaveBeenCreated()
    {
        var mapped = GitHubErrorMapper.MapNetwork(GitHubRequestStage.Submit);

        Assert.Equal(ReviewSubmitErrorCode.NETWORK_ERROR, mapped.Code);
        Assert.True(mapped.Details!.Retryable);
        Assert.Contains("可能已发出", mapped.Message);
    }

    [Fact]
    public void MapUnexpected_TruncatesMessageTo512Chars()
    {
        var mapped = GitHubErrorMapper.MapUnexpected(new string('x', 2000));

        Assert.Equal(ReviewSubmitErrorCode.UNEXPECTED_ERROR, mapped.Code);
        Assert.True(mapped.Message.Length <= 512);
        Assert.EndsWith("…", mapped.Message);
    }
}
