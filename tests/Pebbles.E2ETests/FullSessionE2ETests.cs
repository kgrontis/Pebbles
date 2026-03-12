namespace Pebbles.E2ETests;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Services.Tools;
using System.Collections.ObjectModel;

public class FullSessionE2ETests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PebblesOptions _options;
    private bool isDisposed;

    public FullSessionE2ETests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDirectory);
        Environment.CurrentDirectory = _testDirectory;

        _options = new PebblesOptions
        {
            DefaultModel = "test-model",
            Provider = "mock",
            AutoCompressionEnabled = false
        };
    }

    [Fact]
    public async Task E2E_FileOperations_CreateReadModify()
    {
        // Arrange
        var fileService = new FileService();
        var writeTool = new WriteFileTool();
        var readTool = new ReadFileTool(fileService);
        var testFile = "e2e_test.txt";

        // Act - Create file
        var writeArgs = System.Text.Json.JsonSerializer.Serialize(new
        {
            path = testFile,
            content = "Initial content"
        });
        var writeResult = await writeTool.ExecuteAsync(writeArgs);

        // Act - Read file
        var readArgs = System.Text.Json.JsonSerializer.Serialize(new { path = testFile });
        var readResult = await readTool.ExecuteAsync(readArgs);

        // Act - Modify file
        var modifyArgs = System.Text.Json.JsonSerializer.Serialize(new
        {
            path = testFile,
            content = "Modified content"
        });
        var modifyResult = await writeTool.ExecuteAsync(modifyArgs);

        // Clear file cache to ensure we read the modified content, not cached
        fileService.ClearFiles();

        // Act - Read again to verify
        var verifyResult = await readTool.ExecuteAsync(readArgs);

        // Assert
        Assert.True(writeResult.Success);
        Assert.True(readResult.Success);
        Assert.True(modifyResult.Success);
        Assert.True(verifyResult.Success);
        Assert.Contains("Initial content", readResult.Content, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Modified content", verifyResult.Content, StringComparison.InvariantCultureIgnoreCase);
        Assert.DoesNotContain("Initial content", verifyResult.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task E2E_ToolExecution_FullLoop()
    {
        // Arrange
        var toolRegistry = new ToolRegistry(null);
        var fileService = new FileService();
        toolRegistry.RegisterTool(new WriteFileTool());
        toolRegistry.RegisterTool(new ReadFileTool(fileService));
        toolRegistry.RegisterTool(new ListDirectoryTool(fileService));

        // Act - Write a file
        var writeArgs = System.Text.Json.JsonSerializer.Serialize(new
        {
            path = "tool_test.cs",
            content = "public class Test {}"
        });
        var writeResult = await toolRegistry.ExecuteToolAsync("write_file", writeArgs);

        // Act - List directory
        var listArgs = System.Text.Json.JsonSerializer.Serialize(new { path = _testDirectory });
        var listResult = await toolRegistry.ExecuteToolAsync("list_directory", listArgs);

        // Act - Read the file
        var readArgs = System.Text.Json.JsonSerializer.Serialize(new { path = "tool_test.cs" });
        var readResult = await toolRegistry.ExecuteToolAsync("read_file", readArgs);

        // Assert
        Assert.True(writeResult.Success);
        Assert.True(listResult.Success);
        Assert.True(readResult.Success);
        Assert.Contains("tool_test.cs", listResult.Content, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("public class Test", readResult.Content, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task E2E_ChatService_MockProvider()
    {
        // Arrange
        var mockProvider = new MockAIProvider();

        // Act - Get a response (should not use tools since mock doesn't call them)
        var response = await mockProvider.GetResponseWithToolsAsync(
            "Hello, what can you do?",
            []
        );

        // Assert
        Assert.NotNull(response);
        Assert.NotEmpty(response.Content);
        Assert.Empty(response.ToolCalls);
    }

    [Fact]
    public void E2E_Configuration_OptionsValidation()
    {
        // Arrange
        var validOptions = new PebblesOptions
        {
            DefaultModel = "qwen3.5-plus",
            Provider = "mock",
            CompressionThreshold = 0.7,
            KeepRecentMessages = 6
        };

        var validator = new PebblesOptionsValidator();

        // Act
        var result = validator.Validate(null, validOptions);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task E2E_Compression_ServiceIntegration()
    {
        // Arrange
        var aiProvider = new MockAIProvider();
        var promptService = new SystemPromptService();
        var compressionService = new CompressionService(aiProvider, promptService, _options);
        var messages = new Collection<ChatMessage>();

        // Add 10 messages to trigger compression
        for (var i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 100));
            messages.Add(ChatMessage.Assistant($"Response {i}", 200));
        }

        // Act - Compress (should summarize old messages)
        var result = await compressionService.CompactAsync(messages, 4, null);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success || result.MessagesSummarized > 0);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (isDisposed) return;
        if (disposing)
        {
#pragma warning disable CA1031 // Do not catch general exception types
            try
            {
                Environment.CurrentDirectory = Path.GetTempPath();
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
                isDisposed = true;
            }
            catch
            {
                // Ignore cleanup errors
            }
#pragma warning restore CA1031 // Do not catch general exception types
        }
    }
}
