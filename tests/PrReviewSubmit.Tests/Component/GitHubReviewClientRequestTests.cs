using System.Net;
using System.Text.Json;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;

namespace PrReviewSubmit.Tests.Component;

public class GitHubReviewClientRequestTests
{
    [Fact]
    public async Task CreateReviewAsync_SerializesGitHubContractPayload()
    {
        var handler = new StubHandler(request =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":123456789,"html_url":"https://github.com/octo/repo/pull/42#pullrequestreview-123456789"}"""),
            });
        using var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
        };
        var client = new GitHubReviewClient(http, new GitHubAppOptions
        {
            AppId = 111111,
            InstallationId = 222222,
            PrivateKeyPath = "private-key/test.pem",
        });

        var review = await client.CreateReviewAsync(
            "octo",
            "repo",
            42,
            "installation-token",
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

        Assert.Equal(123456789, review.Id);
        Assert.Equal(
            "https://github.com/octo/repo/pull/42#pullrequestreview-123456789",
            review.HtmlUrl);

        _ = Assert.Single(handler.Requests);
        var body = Assert.Single(handler.Bodies)!;
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal("COMMENT", root.GetProperty("event").GetString());
        Assert.Equal("整体审查结论", root.GetProperty("body").GetString());

        var comment = root.GetProperty("comments")[0];
        Assert.Equal("README.md", comment.GetProperty("path").GetString());
        Assert.Equal(3, comment.GetProperty("line").GetInt32());
        Assert.Equal("RIGHT", comment.GetProperty("side").GetString());
        Assert.Equal("这里需要修改", comment.GetProperty("body").GetString());
        Assert.False(comment.TryGetProperty("Path", out _));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));
            return _responder(request);
        }
    }
}
