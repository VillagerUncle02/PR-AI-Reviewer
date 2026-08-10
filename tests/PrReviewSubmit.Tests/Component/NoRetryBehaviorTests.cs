using System.Net;
using ModelContextProtocol.Protocol;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

public class NoRetryBehaviorTests
{
    [Fact]
    public async Task FailedCall_DoesNotTriggerBackgroundRetryOrCompensation()
    {
        var stub = new CountingStub
        {
            SubmitException = GitHubRequestException.FromMapped(GitHubErrorMapper.MapNetwork(GitHubRequestStage.Submit)),
        };
        var tool = new ReviewSubmitTool(stub);

        var result = await tool.SubmitPrReviewAsync(
            "octo",
            "repo",
            42,
            "整体审查结论",
            [],
            CancellationToken.None);

        Assert.True(result.IsError);
        var (tokenCalls, stateCalls, submitCalls) = (stub.TokenCalls, stub.StateCalls, stub.SubmitCalls);

        await Task.Delay(300, TestContext.Current.CancellationToken);

        Assert.Equal(tokenCalls, stub.TokenCalls);
        Assert.Equal(stateCalls, stub.StateCalls);
        Assert.Equal(submitCalls, stub.SubmitCalls);
    }

    private sealed class CountingStub : IGitHubReviewClient
    {
        public int TokenCalls { get; private set; }
        public int StateCalls { get; private set; }
        public int SubmitCalls { get; private set; }
        public Exception? SubmitException { get; set; }
        public SubmittedReview SubmitResult { get; set; } = new(1, "https://example.test/review/1");

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
            return SubmitException is null
                ? Task.FromResult(SubmitResult)
                : Task.FromException<SubmittedReview>(SubmitException);
        }
    }
}
