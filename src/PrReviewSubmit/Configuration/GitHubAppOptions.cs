namespace PrReviewSubmit.Configuration;

/// <summary>
/// GitHub App 必需配置（环境变量注入，启动时固化）。
/// API 地址固定生产环境 api.github.com（FR-016），不支持配置覆盖。
/// </summary>
public sealed class GitHubAppOptions
{
    public const string BaseUrl = "https://api.github.com";
    public const string ApiVersionHeader = "X-GitHub-Api-Version";
    public const string ApiVersion = "2022-11-28";
    public const string DefaultPrivateKeyPath = "private-key/github-app.pem";

    public required long AppId { get; init; }
    public required long InstallationId { get; init; }
    public required string PrivateKeyPath { get; init; }

    /// <summary>
    /// 从环境变量读取配置并解析；App ID / 安装 ID 必须为正整数（FR-015）。
    /// </summary>
    public static GitHubAppOptions FromEnvironment()
    {
        var appIdText = Environment.GetEnvironmentVariable("GITHUB_APP_ID");
        var installationIdText = Environment.GetEnvironmentVariable("GITHUB_APP_INSTALLATION_ID");
        var privateKeyPath = Environment.GetEnvironmentVariable("GITHUB_PRIVATE_KEY_PATH") ?? DefaultPrivateKeyPath;

        if (!long.TryParse(appIdText, out var appId) || appId <= 0)
            throw new GitHubConfigurationException("GITHUB_APP_ID 缺失或不是正整数");
        if (!long.TryParse(installationIdText, out var installationId) || installationId <= 0)
            throw new GitHubConfigurationException("GITHUB_APP_INSTALLATION_ID 缺失或不是正整数");
        if (string.IsNullOrWhiteSpace(privateKeyPath))
            throw new GitHubConfigurationException("GITHUB_PRIVATE_KEY_PATH 缺失或无效");

        return new GitHubAppOptions
        {
            AppId = appId,
            InstallationId = installationId,
            PrivateKeyPath = privateKeyPath,
        };
    }
}

/// <summary>启动期配置错误（FR-015：启动即失败，非零退出码 + stderr 明确错误）。</summary>
public sealed class GitHubConfigurationException(string message) : Exception(message);
