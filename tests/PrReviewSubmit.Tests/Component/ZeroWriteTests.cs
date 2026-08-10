using System.Diagnostics;
using System.IO.Pipelines;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.Domain;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

/// <summary>让零写入测试独占执行，避免其他测试并发写入临时目录造成快照抖动。</summary>
[CollectionDefinition("ZeroWrite", DisableParallelization = true)]
public sealed class ZeroWriteCollectionDefinition;

/// <summary>
/// SC-007：一次成功的工具调用前后，工作目录与系统临时目录无新增文件；
/// 协议层调用结果只含单个文本 JSON 内容块（无额外 stdout/stderr 输出，FR-011/CHK162）。
/// </summary>
[Collection("ZeroWrite")]
public class ZeroWriteTests
{
    private const string InitializeRequest =
        """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"pr-review-submit-tests","version":"1.0.0"}}}""";

    private const string InitializedNotification =
        """{"jsonrpc":"2.0","method":"notifications/initialized","params":{}}""";

    private const string InvalidPayloadToolCall =
        """{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"submit_pr_review","arguments":{"owner":"octo","repo":"repo","pullNumber":42,"body":"","comments":[]}}}""";

    [Fact]
    public async Task SuccessfulToolCall_CreatesNoFiles_AndReturnsSingleJsonTextBlock()
    {
        var stub = Substitute.For<IGitHubReviewClient>();
        stub.GetInstallationTokenAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("test-installation-token");
        stub.GetPullRequestStateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PullRequestState("open", false));
        stub.CreateReviewAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<int>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ReviewComment>>(),
                Arg.Any<CancellationToken>())
            .Returns(new SubmittedReview(123456789, "https://github.com/octo/repo/pull/42#pullrequestreview-123456789"));

        var workingDirectory = Directory.GetCurrentDirectory();
        var tempDirectory = Path.GetTempPath();
        var beforeWorkingDirectory = Snapshot(workingDirectory);
        var beforeTempDirectory = Snapshot(tempDirectory);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var services = new ServiceCollection();
        services.AddSingleton(new GitHubAppOptions
        {
            AppId = 111111,
            InstallationId = 222222,
            PrivateKeyPath = "private-key/test.pem",
        });
        services.AddSingleton(stub);
        services.AddSingleton<ReviewSubmitTool>();
        services
            .AddMcpServer()
            .WithStreamServerTransport(
                clientToServer.Reader.AsStream(),
                serverToClient.Writer.AsStream())
            .WithToolsFromAssembly(typeof(ReviewSubmitTool).Assembly);

        await using var provider = services.BuildServiceProvider();
        await using var server = provider.GetRequiredService<McpServer>();
        var serverTask = server.RunAsync(TestContext.Current.CancellationToken);

        await using var client = await McpClient.CreateAsync(
            new StreamClientTransport(
                clientToServer.Writer.AsStream(),
                serverToClient.Reader.AsStream()),
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await client.CallToolAsync(
            "submit_pr_review",
            new Dictionary<string, object?>
            {
                ["owner"] = "octo",
                ["repo"] = "repo",
                ["pullNumber"] = 42,
                ["body"] = "整体审查结论",
                ["comments"] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["path"] = "README.md",
                        ["line"] = 3,
                        ["side"] = "RIGHT",
                        ["body"] = "这里需要修改",
                    },
                },
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsError);

        // 协议层只返回一个文本 JSON 内容块：无日志文本、无额外 stdout/stderr 内容块（FR-011/CHK162）。
        var textBlock = Assert.IsType<TextContentBlock>(Assert.Single(result.Content));
        using var json = JsonDocument.Parse(textBlock.Text);
        Assert.Equal("success", json.RootElement.GetProperty("status").GetString());

        var afterWorkingDirectory = Snapshot(workingDirectory);
        var afterTempDirectory = Snapshot(tempDirectory);
        Assert.Empty(afterWorkingDirectory.Except(beforeWorkingDirectory));
        Assert.Empty(afterTempDirectory.Except(beforeTempDirectory));

        await client.DisposeAsync();
        clientToServer.Writer.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static HashSet<string> Snapshot(string directory)
        => Directory
            .EnumerateFileSystemEntries(
                directory,
                "*",
                new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                })
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// SC-007（零持久化/零写入）与 FR-011/CHK162（运行期零 stdout/stderr）的进程级验证：
    /// 以真实构建产物 + 有效 RSA 私钥启动完整 MCP stdio 会话（initialize → initialized →
    /// tools/call 空白 body 本地返回 INVALID_PAYLOAD，不发任何 GitHub 网络请求），
    /// 关闭 stdin 等待退出后断言 stderr 为空、stdout 每行均为合法 JSON-RPC。
    /// </summary>
    [Fact]
    public async Task ServerProcess_McpSession_ProducesNoStderr_AndOnlyJsonRpcOnStdout()
    {
        var keyPath = Path.Combine(Path.GetTempPath(), $"valid-key-{Guid.NewGuid():N}.pem");
        using (var rsa = System.Security.Cryptography.RSA.Create(2048))
        {
            await File.WriteAllTextAsync(
                keyPath,
                rsa.ExportRSAPrivateKeyPem(),
                TestContext.Current.CancellationToken);
        }

        try
        {
            var result = await RunMcpSessionAsync(
                new Dictionary<string, string?>
                {
                    ["GITHUB_APP_ID"] = "111111",
                    ["GITHUB_APP_INSTALLATION_ID"] = "222222",
                    ["GITHUB_PRIVATE_KEY_PATH"] = keyPath,
                });

            // 关闭 stdin 后服务端应正常退出（非启动配置错误路径）。
            Assert.Equal(0, result.ExitCode);

            // FR-011/CHK162：运行期除 MCP 协议输出与工具调用结果外，stderr 必须为空。
            Assert.Equal("", result.Stderr.Trim());

            // 会话真实完成：initialize 与 tools/call 各有一条 JSON-RPC 响应。
            Assert.Contains(result.StdoutLines, line => line.Contains("\"id\":1", StringComparison.Ordinal));
            var callResponse = Assert.Single(
                result.StdoutLines,
                line => line.Contains("\"id\":2", StringComparison.Ordinal));

            // stdout 每行均为合法 JSON-RPC：以 { 开头且可解析为 jsonrpc 2.0 对象。
            Assert.NotEmpty(result.StdoutLines);
            Assert.All(result.StdoutLines, line =>
            {
                Assert.StartsWith("{", line);
                using var document = JsonDocument.Parse(line);
                Assert.Equal("2.0", document.RootElement.GetProperty("jsonrpc").GetString());
            });

            // 空白 body 触发本地 INVALID_PAYLOAD（FR-003），不产生任何 GitHub 请求。
            using var callDocument = JsonDocument.Parse(callResponse);
            var content = Assert.Single(callDocument.RootElement
                .GetProperty("result")
                .GetProperty("content")
                .EnumerateArray());
            Assert.Equal("text", content.GetProperty("type").GetString());
            using var payloadDocument = JsonDocument.Parse(content.GetProperty("text").GetString()!);
            Assert.Equal("INVALID_PAYLOAD", payloadDocument.RootElement.GetProperty("code").GetString());
        }
        finally
        {
            File.Delete(keyPath);
        }
    }

    /// <summary>
    /// SC-008（单次上传从调用到返回明确结果在 30 秒内完成）：由代码审查与冒烟场景 A 覆盖——
    /// ReviewSubmitTool 仅经 IGitHubReviewClient 顺序执行令牌交换/PR 状态读取/create review
    /// 三次 REST 请求，各请求 HttpClient 超时 10 秒且无重试，总预算 ≤30 秒（CHK079/CHK127）；
    /// 真实网络计时以 GitHub 可用为前提，故不在此进程内测试断言。
    /// </summary>
    private static async Task<ProcessSessionResult> RunMcpSessionAsync(
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
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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
        var stdoutLines = new List<string>();
        var stdoutTask = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) is not null)
            {
                lock (stdoutLines)
                    stdoutLines.Add(line);
            }
        });
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMs));

        var sessionCompleted = false;
        try
        {
            await SendAsync(process, InitializeRequest);
            await WaitForResponseAsync(stdoutLines, "\"id\":1", timeoutCts.Token);
            await SendAsync(process, InitializedNotification);
            await SendAsync(process, InvalidPayloadToolCall);
            await WaitForResponseAsync(stdoutLines, "\"id\":2", timeoutCts.Token);
            CloseStdin(process);
            sessionCompleted = true;
        }
        catch (OperationCanceledException ex)
        {
            KillProcess(process);
            throw new TimeoutException(
                $"MCP 会话未在 {timeoutMs}ms 内完成；已捕获 stdout：{string.Join(" | ", stdoutLines)}",
                ex);
        }
        catch
        {
            KillProcess(process);
            throw;
        }
        finally
        {
            if (!sessionCompleted)
                KillProcess(process);
        }

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            KillProcess(process);
            string stderr;
            try
            {
                stderr = await stderrTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                stderr = "<stderr 读取失败>";
            }

            throw new TimeoutException($"关闭 stdin 后服务端未在 {timeoutMs}ms 内退出；stderr：{stderr}");
        }

        try
        {
            await stdoutTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // stdout 读取线程异常不影响已收集的行。
        }

        var capturedStderr = await stderrTask.WaitAsync(TimeSpan.FromSeconds(5));
        lock (stdoutLines)
            return new ProcessSessionResult(process.ExitCode, stdoutLines.ToList(), capturedStderr);
    }

    private static async Task SendAsync(Process process, string json)
    {
        await process.StandardInput.WriteLineAsync(json);
        await process.StandardInput.FlushAsync();
    }

    private static async Task WaitForResponseAsync(
        List<string> stdoutLines,
        string idNeedle,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            lock (stdoutLines)
            {
                if (stdoutLines.Any(line => line.Contains(idNeedle, StringComparison.Ordinal)))
                    return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), cancellationToken);
        }
    }

    private static void CloseStdin(Process process)
    {
        try
        {
            process.StandardInput.Close();
        }
        catch (InvalidOperationException)
        {
            // 进程已退出时 stdin 已关闭。
        }
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // 进程已退出。
        }
    }

    private sealed record ProcessSessionResult(int ExitCode, IReadOnlyList<string> StdoutLines, string Stderr);
}
