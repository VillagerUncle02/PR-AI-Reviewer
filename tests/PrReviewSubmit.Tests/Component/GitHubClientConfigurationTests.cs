using Microsoft.Extensions.DependencyInjection;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Infrastructure;

namespace PrReviewSubmit.Tests.Component;

public class GitHubClientConfigurationTests
{
    [Fact]
    public void GitHubHttpClient_IsFixedToApiGithubCom_WithDefaultTlsValidation()
    {
        var services = new ServiceCollection();
        services.AddGitHubHttpClient();
        using var provider = services.BuildServiceProvider();
        var http = provider.GetRequiredService<HttpClient>();
        var handler = provider.GetRequiredService<SocketsHttpHandler>();

        Assert.Equal(new Uri(GitHubAppOptions.BaseUrl).AbsoluteUri, http.BaseAddress!.AbsoluteUri);
        Assert.Equal(TimeSpan.FromSeconds(10), http.Timeout);
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
    }
}
