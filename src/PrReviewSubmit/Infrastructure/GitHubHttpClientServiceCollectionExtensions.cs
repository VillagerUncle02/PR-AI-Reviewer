using Microsoft.Extensions.DependencyInjection;
using PrReviewSubmit.Configuration;

namespace PrReviewSubmit.Infrastructure;

/// <summary>
/// GitHub API HttpClient 注册：BaseAddress 固定生产环境 api.github.com（FR-016），
/// 超时 10 秒；TLS 证书校验使用系统默认（开启），不提供禁用入口。
/// </summary>
public static class GitHubHttpClientServiceCollectionExtensions
{
    public static IServiceCollection AddGitHubHttpClient(this IServiceCollection services)
    {
        services.AddSingleton(_ => new SocketsHttpHandler());
        services.AddSingleton(sp => new HttpClient(sp.GetRequiredService<SocketsHttpHandler>())
        {
            BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
            Timeout = TimeSpan.FromSeconds(10),
        });
        return services;
    }
}
