using System.Net;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using NSubstitute;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

public class ReviewSubmitFlowTests
{
    [Theory]
    [InlineData("closed", false)]
    [InlineData("open", true)]
    [InlineData("CLOSED", false)]
    public async Task NonOpenOrMergedPr_ReturnsPrNotOpen_WithoutSubmitting(string state, bool merged)
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState(state, merged));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("PR_NOT_OPEN", root.GetProperty("code").GetString());
        await stub.DidNotReceive().CreateReviewAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ReviewComment>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthNotFound_ReturnsAppNotInstalled()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(GitHubRequestException.FromMapped(GitHubErrorMapper.MapResponse(
                HttpStatusCode.NotFound,
                """{"message":"not found"}""",
                GitHubRequestStage.Auth))));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("APP_NOT_INSTALLED", root.GetProperty("code").GetString());
        Assert.Equal(404, root.GetProperty("httpStatus").GetInt32());
    }

    [Fact]
    public async Task PullStateNotFound_ReturnsTargetNotFound()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PullRequestState>(GitHubRequestException.FromMapped(GitHubErrorMapper.MapResponse(
                HttpStatusCode.NotFound,
                """{"message":"not found"}""",
                GitHubRequestStage.PullState))));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("TARGET_NOT_FOUND", root.GetProperty("code").GetString());
        await stub.DidNotReceive().CreateReviewAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ReviewComment>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitUnprocessable_ReturnsReviewUnprocessable()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState("open", false));
        stub.CreateReviewAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ReviewComment>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SubmittedReview>(GitHubRequestException.FromMapped(GitHubErrorMapper.MapResponse(
                HttpStatusCode.UnprocessableEntity,
                """{"message":"comments not on diff"}""",
                GitHubRequestStage.Submit))));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("REVIEW_UNPROCESSABLE", root.GetProperty("code").GetString());
        Assert.Equal(422, root.GetProperty("httpStatus").GetInt32());
    }

    [Fact]
    public async Task SubmitNetworkError_ReturnsNetworkError()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState("open", false));
        stub.CreateReviewAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ReviewComment>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SubmittedReview>(
                GitHubRequestException.FromMapped(GitHubErrorMapper.MapNetwork(GitHubRequestStage.Submit))));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("NETWORK_ERROR", root.GetProperty("code").GetString());
        Assert.Contains("核验目标 PR", root.GetProperty("message").GetString());
        Assert.True(root.GetProperty("details").GetProperty("retryable").GetBoolean());
    }

    [Fact]
    public async Task InvalidPayload_ReturnsInvalidPayload_WithZeroGitHubRequests()
    {
        var stub = Substitute.For<IGitHubReviewClient>();

        var tool = new ReviewSubmitTool(stub);
        var result = await tool.SubmitPrReviewAsync(
            "octo",
            "repo",
            42,
            "   ",
            [],
            CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal("error", root.GetProperty("status").GetString());
        Assert.Equal("INVALID_PAYLOAD", root.GetProperty("code").GetString());
        await stub.DidNotReceive().GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await stub.DidNotReceive().GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await stub.DidNotReceive().CreateReviewAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<ReviewComment>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnexpectedException_ReturnsUnexpectedError()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState("open", false));
        stub.CreateReviewAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ReviewComment>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromException<SubmittedReview>(new InvalidOperationException("boom")));

        var root = await InvokeErrorAsync(stub);

        Assert.Equal("UNEXPECTED_ERROR", root.GetProperty("code").GetString());
    }

    private static async Task<JsonElement> InvokeErrorAsync(IGitHubReviewClient stub)
    {
        var tool = new ReviewSubmitTool(stub);
        var result = await tool.SubmitPrReviewAsync(
            "octo",
            "repo",
            42,
            "整体审查结论",
            [
                new ReviewComment
                {
                    Path = "README.md",
                    Line = 3,
                    Side = "RIGHT",
                    Body = "这里需要修改",
                },
            ],
            CancellationToken.None);

        Assert.True(result.IsError);
        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal("error", root.GetProperty("status").GetString());
        return root.Clone();
    }
}
