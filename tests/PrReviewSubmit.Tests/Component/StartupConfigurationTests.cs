using System.Diagnostics;
using System.Text;

namespace PrReviewSubmit.Tests.Component;

public class StartupConfigurationTests
{
    [Fact]
    public async Task MissingAppId_ExitsNonZeroWithClearStderr()
    {
        var result = await RunServerAsync(new Dictionary<string, string?>
        {
            ["GITHUB_APP_INSTALLATION_ID"] = "222222",
            ["GITHUB_PRIVATE_KEY_PATH"] = WriteValidKey(),
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("GITHUB_APP_ID", result.Stderr);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public async Task InvalidAppId_ExitsNonZeroWithClearStderr(string appId)
    {
        var result = await RunServerAsync(new Dictionary<string, string?>
        {
            ["GITHUB_APP_ID"] = appId,
            ["GITHUB_APP_INSTALLATION_ID"] = "222222",
            ["GITHUB_PRIVATE_KEY_PATH"] = WriteValidKey(),
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("GITHUB_APP_ID", result.Stderr);
    }

    [Fact]
    public async Task MissingInstallationId_ExitsNonZeroWithClearStderr()
    {
        var result = await RunServerAsync(new Dictionary<string, string?>
        {
            ["GITHUB_APP_ID"] = "111111",
            ["GITHUB_PRIVATE_KEY_PATH"] = WriteValidKey(),
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("GITHUB_APP_INSTALLATION_ID", result.Stderr);
    }

    [Fact]
    public async Task MissingPrivateKeyFile_ExitsNonZeroWithClearStderr()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"missing-key-{Guid.NewGuid():N}.pem");

        var result = await RunServerAsync(new Dictionary<string, string?>
        {
            ["GITHUB_APP_ID"] = "111111",
            ["GITHUB_APP_INSTALLATION_ID"] = "222222",
            ["GITHUB_PRIVATE_KEY_PATH"] = missingPath,
        });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("GITHUB_PRIVATE_KEY_PATH", result.Stderr);
    }

    [Fact]
    public async Task InvalidPrivateKeyContent_ExitsNonZeroWithClearStderr()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"garbage-key-{Guid.NewGuid():N}.pem");
        await File.WriteAllTextAsync(keyPath, "not a pem key", TestContext.Current.CancellationToken);
        try
        {
            var result = await RunServerAsync(new Dictionary<string, string?>
            {
                ["GITHUB_APP_ID"] = "111111",
                ["GITHUB_APP_INSTALLATION_ID"] = "222222",
                ["GITHUB_PRIVATE_KEY_PATH"] = keyPath,
            });

            Assert.NotEqual(0, result.ExitCode);
            Assert.Contains("GITHUB_PRIVATE_KEY_PATH", result.Stderr);
            Assert.Contains("RSA", result.Stderr);
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    private static string WriteValidKey()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"valid-key-{Guid.NewGuid():N}.pem");
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        File.WriteAllText(keyPath, rsa.ExportRSAPrivateKeyPem());
        return keyPath;
    }

    private static async Task<ProcessResult> RunServerAsync(
        IReadOnlyDictionary<string, string?> environment,
        int timeoutMs = 15000)
    {
        var exeName = OperatingSystem.IsWindows() ? "PrReviewSubmit.exe" : "PrReviewSubmit";
        var exePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/PrReviewSubmit/bin/Debug/net10.0",
            exeName));
        Assert.True(File.Exists(exePath), $"服务端可执行文件不存在：{exePath}");

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.Environment.Remove("GITHUB_APP_ID");
        psi.Environment.Remove("GITHUB_APP_INSTALLATION_ID");
        psi.Environment.Remove("GITHUB_PRIVATE_KEY_PATH");
        foreach (var (key, value) in environment)
            psi.Environment[key] = value;

        using var process = Process.Start(psi)!;
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("启动校验未在超时内退出（疑似尝试网络请求）");
        }

        var stderr = await stderrTask;
        return new ProcessResult(process.ExitCode, stderr);
    }

    private sealed record ProcessResult(int ExitCode, string Stderr);
}
