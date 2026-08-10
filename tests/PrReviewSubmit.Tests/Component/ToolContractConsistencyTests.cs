using System.IO.Pipelines;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using NSubstitute;
using PrReviewSubmit.Configuration;
using PrReviewSubmit.GitHub;
using PrReviewSubmit.MCP;

namespace PrReviewSubmit.Tests.Component;

/// <summary>
/// 工具契约一致性（FR-001 / CHK142）：MCP 层暴露的工具集合必须恰为
/// {submit_pr_review}，且参数名/类型/必填与 contracts/submit-review.schema.json 一致。
/// 不依赖描述文本，只断言协议层的 Name 与 InputSchema。
/// </summary>
public class ToolContractConsistencyTests
{
    [Fact]
    public async Task Server_ExposesExactlySubmitPrReview_WithInputSchemaMatchingContract()
    {
        var stub = Substitute.For<IGitHubReviewClient>();

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

        var tools = await client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var tool = Assert.Single(tools);

        Assert.Equal("submit_pr_review", tool.Name);

        // SDK 2.1.0：McpClientTool.ProtocolTool 为协议层 Tool，
        // InputSchema 类型为 System.Text.Json.JsonElement（经反射/XML 文档确认）。
        var schema = tool.ProtocolTool.InputSchema;
        Assert.Equal(JsonValueKind.Object, schema.ValueKind);
        Assert.Equal("object", schema.GetProperty("type").GetString());

        var required = schema
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(
            new[] { "owner", "repo", "pullNumber", "body", "comments" }
                .OrderBy(name => name, StringComparer.Ordinal),
            required);

        var properties = schema.GetProperty("properties");
        AssertStringProperty(properties, "owner");
        AssertStringProperty(properties, "repo");
        Assert.Equal("integer", properties.GetProperty("pullNumber").GetProperty("type").GetString());
        AssertStringProperty(properties, "body");

        var comments = properties.GetProperty("comments");
        Assert.Equal("array", comments.GetProperty("type").GetString());
        var commentItems = comments.GetProperty("items");
        Assert.Equal("object", commentItems.GetProperty("type").GetString());

        var commentRequired = commentItems
            .GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(
            new[] { "path", "line", "side", "body" }
                .OrderBy(name => name, StringComparer.Ordinal),
            commentRequired);

        var commentProperties = commentItems.GetProperty("properties");
        AssertStringProperty(commentProperties, "path");
        Assert.Equal("integer", commentProperties.GetProperty("line").GetProperty("type").GetString());
        AssertStringProperty(commentProperties, "side");
        AssertStringProperty(commentProperties, "body");

        await client.DisposeAsync();
        clientToServer.Writer.Complete();
        await serverTask.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
    }

    private static void AssertStringProperty(JsonElement properties, string name)
        => Assert.Equal("string", properties.GetProperty(name).GetProperty("type").GetString());
}
