using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Logging.AddConsole(consoleLogOptions =>
    {
        // MCP stdio 协议独占 stdout：所有日志一律走 stderr。
        consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    builder.Services.AddSingleton(GitHubAppOptions.FromEnvironment());
    builder.Services.AddSingleton(_ => new HttpClient
    {
        BaseAddress = new Uri(GitHubAppOptions.BaseUrl),
        Timeout = TimeSpan.FromSeconds(10),
    });
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
