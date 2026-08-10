using System.Net;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

public class RetryFlowTests
{
    [Fact]
    public async Task CallerRetryAfterFailure_CreatesExactlyOneNewReview()
    {
        var stub = new FailOnceStub();
        var tool = new ReviewSubmitTool(stub);

        var first = await tool.SubmitPrReviewAsync(
            "octo",
            "repo",
            42,
            "整体审查结论",
            [],
            CancellationToken.None);

        Assert.True(first.IsError);
        Assert.Equal(1, stub.SubmitCalls);

        var second = await tool.SubmitPrReviewAsync(
            "octo",
            "repo",
            42,
            "整体审查结论",
            [],
            CancellationToken.None);

        Assert.False(second.IsError);
        var text = Assert.Single(second.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;
        Assert.Equal("success", root.GetProperty("status").GetString());
        Assert.Equal(777, root.GetProperty("reviewId").GetInt64());
        Assert.Equal(2, stub.SubmitCalls);
        Assert.Equal(2, stub.TokenCalls);
        Assert.Equal(2, stub.StateCalls);
    }

    private sealed class FailOnceStub : IGitHubReviewClient
    {
        public int TokenCalls { get; private set; }
        public int StateCalls { get; private set; }
        public int SubmitCalls { get; private set; }

        public Task<string> GetInstallationTokenAsync(string owner, string repo, CancellationToken cancellationToken)
        {
            TokenCalls++;
            return Task.FromResult("token");
        }

        public Task<PullRequestState> GetPullRequestStateAsync(
            string owner,
            string repo,
            int pullNumber,
            string token,
            CancellationToken cancellationToken)
        {
            StateCalls++;
            return Task.FromResult(new PullRequestState("open", false));
        }

        public Task<SubmittedReview> CreateReviewAsync(
            string owner,
            string repo,
            int pullNumber,
            string token,
            string body,
            IReadOnlyList<ReviewComment> comments,
            CancellationToken cancellationToken)
        {
            SubmitCalls++;
            if (SubmitCalls == 1)
            {
                return Task.FromException<SubmittedReview>(GitHubRequestException.FromMapped(
                    GitHubErrorMapper.MapResponse(
                        HttpStatusCode.UnprocessableEntity,
                        """{"message":"comments not on diff"}""",
                        GitHubRequestStage.Submit)));
            }

            return Task.FromResult(new SubmittedReview(777, "https://github.com/octo/repo/pull/42#pullrequestreview-777"));
        }
    }
}
