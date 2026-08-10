using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Cryptography;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;

namespace PrReviewSubmit.Tests.Unit;

public class AppAuthClientTests
{
    private const string SuccessBody =
        """{"token":"installation-token-abc","expires_at":"2026-08-10T00:00:00Z"}""";

    [Fact]
    public void CreateJwt_ContainsExpectedClaims()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var client = CreateClient(keyPath, CreateHandler(_ => JsonResponse(HttpStatusCode.Created, SuccessBody)));

        var jwt = client.CreateJwt();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(jwt);
        var now = DateTime.UtcNow;

        Assert.Equal("111111", token.Issuer);
        Assert.True(token.IssuedAt <= now.AddSeconds(1));
        Assert.True(token.ValidTo >= now.AddSeconds(-1));
        Assert.True(token.ValidTo - token.IssuedAt <= TimeSpan.FromMinutes(11));
        Assert.True(token.ValidTo - token.IssuedAt >= TimeSpan.FromMinutes(9));
    }

    [Fact]
    public async Task GetInstallationTokenAsync_Success_SendsScopedRequestAndReturnsToken()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var handler = CreateHandler(_ => JsonResponse(HttpStatusCode.Created, SuccessBody));
        var client = CreateClient(keyPath, handler);

        var token = await client.GetInstallationTokenAsync("octo-repo", CancellationToken.None);

        Assert.Equal("installation-token-abc", token);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.EndsWith("/app/installations/222222/access_tokens", request.RequestUri!.AbsolutePath);
        Assert.StartsWith("Bearer eyJ", request.Headers.Authorization!.ToString());
        Assert.Contains("application/vnd.github+json", request.Headers.Accept.ToString());
        Assert.Equal("2022-11-28", request.Headers.GetValues("X-GitHub-Api-Version").Single());
        Assert.Contains("\"repositories\":[\"octo-repo\"]", handler.Bodies[0]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, ReviewSubmitErrorCode.CREDENTIALS_INVALID)]
    [InlineData(HttpStatusCode.Forbidden, ReviewSubmitErrorCode.APP_NOT_INSTALLED)]
    [InlineData(HttpStatusCode.NotFound, ReviewSubmitErrorCode.APP_NOT_INSTALLED)]
    [InlineData(HttpStatusCode.TooManyRequests, ReviewSubmitErrorCode.RATE_LIMITED)]
    public async Task GetInstallationTokenAsync_MapsHttpErrors(
        HttpStatusCode status,
        ReviewSubmitErrorCode expected)
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var handler = CreateHandler(_ => JsonResponse(status, """{"message":"error"}"""));
        var client = CreateClient(keyPath, handler);

        var ex = await Assert.ThrowsAsync<GitHubRequestException>(
            () => client.GetInstallationTokenAsync("octo-repo", CancellationToken.None));

        Assert.Equal(expected, ex.Code);
        Assert.Equal((int)status, ex.HttpStatus);
    }

    [Theory]
    [InlineData("""{"expires_at":"2026-08-10T00:00:00Z"}""")]
    [InlineData("""{"token":"installation-token-abc"}""")]
    [InlineData("""{"token":"","expires_at":"2026-08-10T00:00:00Z"}""")]
    [InlineData("not-json")]
    public async Task GetInstallationTokenAsync_MalformedCreatedResponse_MapsUnexpected(string body)
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var handler = CreateHandler(_ => JsonResponse(HttpStatusCode.Created, body));
        var client = CreateClient(keyPath, handler);

        var ex = await Assert.ThrowsAsync<GitHubRequestException>(
            () => client.GetInstallationTokenAsync("octo-repo", CancellationToken.None));

        Assert.Equal(ReviewSubmitErrorCode.UNEXPECTED_ERROR, ex.Code);
    }

    [Fact]
    public async Task GetInstallationTokenAsync_KeyDeletedBetweenCalls_FailsWithoutGitHubRequest()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var handler = CreateHandler(_ => JsonResponse(HttpStatusCode.Created, SuccessBody));
        var client = CreateClient(keyPath, handler);
        await client.GetInstallationTokenAsync("octo-repo", CancellationToken.None);

        File.Delete(keyPath);

        var ex = await Assert.ThrowsAsync<GitHubRequestException>(
            () => client.GetInstallationTokenAsync("octo-repo", CancellationToken.None));

        Assert.Equal(ReviewSubmitErrorCode.CREDENTIALS_INVALID, ex.Code);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task GetInstallationTokenAsync_KeyReplaced_NextCallUsesNewKeyWithoutRestart()
    {
        using var cleanup = WriteTempKey(out var keyPath);
        var handler = CreateHandler(_ => JsonResponse(HttpStatusCode.Created, SuccessBody));
        var client = CreateClient(keyPath, handler);
        await client.GetInstallationTokenAsync("octo-repo", CancellationToken.None);
        var firstJwt = handler.Requests[0].Headers.Authorization!.Parameter;

        using var replacement = RSA.Create(2048);
        File.WriteAllText(keyPath, replacement.ExportRSAPrivateKeyPem());

        var token = await client.GetInstallationTokenAsync("octo-repo", CancellationToken.None);

        Assert.Equal("installation-token-abc", token);
        Assert.Equal(2, handler.Requests.Count);
        Assert.NotEqual(firstJwt, handler.Requests[1].Headers.Authorization!.Parameter);
    }

    private static GitHubAppAuthClient CreateClient(string keyPath, StubHandler handler)
    {
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
        };
        return new GitHubAppAuthClient(http, new GitHubAppOptions
        {
            AppId = 111111,
            InstallationId = 222222,
            PrivateKeyPath = keyPath,
        });
    }

    private static StubHandler CreateHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        => new(responder);

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
