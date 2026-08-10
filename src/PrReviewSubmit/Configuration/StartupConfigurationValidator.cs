using System.Security.Cryptography;

namespace PrReviewSubmit.Configuration;

/// <summary>
/// FR-015 启动期配置校验：App ID / 安装 ID 的正整数校验由
/// <see cref="GitHubAppOptions.FromEnvironment"/> 完成，本校验补充
/// 私钥文件存在、可读且可解析为 RSA。失败即进程启动失败（非零退出码 + stderr）。
/// </summary>
public static class StartupConfigurationValidator
{
    public static void Validate(GitHubAppOptions options)
    {
        if (!File.Exists(options.PrivateKeyPath))
            throw new GitHubConfigurationException($"GITHUB_PRIVATE_KEY_PATH 私钥文件不存在：{options.PrivateKeyPath}");

        string pem;
        try
        {
            pem = File.ReadAllText(options.PrivateKeyPath);
        }
        catch (Exception ex)
        {
            throw new GitHubConfigurationException($"GITHUB_PRIVATE_KEY_PATH 私钥文件不可读：{options.PrivateKeyPath}", ex);
        }

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
        }
        catch (Exception ex)
        {
            throw new GitHubConfigurationException($"GITHUB_PRIVATE_KEY_PATH 私钥文件无法解析为 RSA：{options.PrivateKeyPath}", ex);
        }
    }
}
