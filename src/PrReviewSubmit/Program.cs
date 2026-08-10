using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.Infrastructure;
using PrReviewSubmit.MCP;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    // FR-011/CHK162：运行期除 MCP 协议与工具调用结果外不得产生其他 stdout/stderr 输出，
    // 因此清除全部日志提供程序（含默认 Console 提供程序），宿主/框架日志不再输出。
    // 启动期配置错误（FR-015）仍由下方 catch 显式输出到 stderr。
    builder.Logging.ClearProviders();

    var options = GitHubAppOptions.FromEnvironment();
    StartupConfigurationValidator.Validate(options);
    builder.Services.AddSingleton(options);
    builder.Services.AddGitHubHttpClient();
    builder.Services.AddSingleton<IGitHubReviewClient, GitHubReviewClient>();
    builder.Services.AddSingleton<ReviewSubmitTool>();
    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly(typeof(ReviewSubmitTool).Assembly);

    await builder.Build().RunAsync();
    return 0;
}
catch (GitHubConfigurationException ex)
{
    Console.Error.WriteLine($"启动配置错误: {ex.Message}");
    return 1;
}
