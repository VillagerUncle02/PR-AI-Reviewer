using System.Net;
using System.Security.Cryptography;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.GitHub;

namespace PrReviewSubmit.Tests.Component;

public class StatelessnessTests
{
    [Fact]
    public async Task EachCall_RequestsFreshInstallationToken_WithoutCaching()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var counter = 0;
        var handler = new StubHandler(request =>
        {
            var current = counter;
            counter++;
            return Task.FromResult(JsonResponse(
                HttpStatusCode.Created,
                $$"""{"token":"token-{{current}}","expires_at":"2026-08-10T00:00:00Z"}"""));
        });
        var client = CreateClient(keyPath, handler);

        var first = await client.GetInstallationTokenAsync("octo", "octo-repo", CancellationToken.None);
        var second = await client.GetInstallationTokenAsync("octo", "octo-repo", CancellationToken.None);

        Assert.Equal("token-0", first);
        Assert.Equal("token-1", second);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ConcurrentCalls_AreIndependent()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var counter = 0;
        var handler = new StubHandler(async request =>
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
            var current = counter;
            counter++;
            return JsonResponse(HttpStatusCode.Created, $$"""{"token":"token-{{current}}","expires_at":"2026-08-10T00:00:00Z"}""");
        });
        var client = CreateClient(keyPath, handler);

        var results = await Task.WhenAll(
            client.GetInstallationTokenAsync("octo", "octo-repo", CancellationToken.None),
            client.GetInstallationTokenAsync("octo", "octo-repo", CancellationToken.None));

        Assert.Equal(2, results.Distinct().Count());
        Assert.Equal(2, handler.Requests.Count);
    }

    private static GitHubReviewClient CreateClient(string keyPath, StubHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
        };
        return new GitHubReviewClient(http, new GitHubAppOptions
        {
            AppId = 111111,
            InstallationId = 222222,
            PrivateKeyPath = keyPath,
        });
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body) };

    private static IDisposable WriteTempKey(out string path)
    {
        path = Path.Combine(Path.GetTempPath(), $"pr-review-submit-key-{Guid.NewGuid():N}.pem");
        using var rsa = RSA.Create(2048);
        File.WriteAllText(path, rsa.ExportRSAPrivateKeyPem());
        return new TempKeyCleanup(path);
    }

    private sealed class TempKeyCleanup(string path) : IDisposable
    {
        public void Dispose()
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

        public StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return await _responder(request);
        }
    }
}
