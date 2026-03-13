namespace Pebbles.Tests.Services;

using Pebbles.Models;
using Pebbles.Services;
using System.Globalization;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class MemoryServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly MockSystemPromptServiceForMemory _promptService;
    private readonly MockAIProviderForMemory _aiProvider;
    private readonly MemoryService _memoryService;
    private bool _disposed;

    public MemoryServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pebbles_memory_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _promptService = new MockSystemPromptServiceForMemory();
        _aiProvider = new MockAIProviderForMemory();
        _memoryService = new MemoryService(_promptService, _aiProvider, _testDirectory);
    }

    [Fact]
    public void GetMemories_ReturnsEmpty_WhenNoMemories()
    {
        // Act
        var memories = _memoryService.GetMemories();

        // Assert
        Assert.Equal(string.Empty, memories);
    }

    [Fact]
    public void SaveMemories_SavesContent()
    {
        // Arrange
        var content = "# User Memory\n\n- Prefers dark mode";

        // Act
        var result = _memoryService.SaveMemories(content);

        // Assert
        Assert.True(result);
        Assert.Contains("Prefers dark mode", _memoryService.GetMemories(), StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void Remember_AddsNewMemory()
    {
        // Arrange
        var memory = "User likes TypeScript";

        // Act
        var result = _memoryService.Remember(memory);

        // Assert
        Assert.True(result);
        var memories = _memoryService.GetMemories();
        Assert.Contains("TypeScript", memories, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void Remember_AddsTimestamp()
    {
        // Arrange
        var memory = "User prefers spaces over tabs";

        // Act
        _memoryService.Remember(memory);

        // Assert
        var memories = _memoryService.GetMemories();
        Assert.Contains(DateTime.Now.Year.ToString(CultureInfo.InvariantCulture), memories, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public void ClearMemories_ResetsToDefault()
    {
        // Arrange
        _memoryService.Remember("Some memory");

        // Act
        var result = _memoryService.ClearMemories();

        // Assert
        Assert.True(result);
        var memories = _memoryService.GetMemories();
        Assert.Contains("User Memory", memories, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ExtractMemoriesAsync_ReturnsNull_WhenNoMessages()
    {
        // Act
        var result = await _memoryService.ExtractMemoriesAsync([]);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractMemoriesAsync_ExtractsFromConversation()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("I prefer using tabs for indentation", 10),
            ChatMessage.Assistant("I'll remember that preference.", 10)
        };
        _aiProvider.SetNextResponse("<memories>- User prefers tabs for indentation</memories>");

        // Act
        var result = await _memoryService.ExtractMemoriesAsync(messages);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("tabs", result, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task ExtractMemoriesAsync_ReturnsNull_WhenNoNewMemories()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("Hello", 5),
            ChatMessage.Assistant("Hi there!", 5)
        };
        _aiProvider.SetNextResponse("No new memories to extract.");

        // Act
        var result = await _memoryService.ExtractMemoriesAsync(messages);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ExtractMemoriesAsync_LimitsToLastTenMessages()
    {
        // Arrange
        var messages = new List<ChatMessage>();
        for (var i = 0; i < 15; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 5));
        }
        _aiProvider.SetNextResponse("<memories>Some memory</memories>");

        // Act
        await _memoryService.ExtractMemoriesAsync(messages);

        // Assert - Verify only last 10 messages were processed
        // The mock provider captures the input, we can verify it contains "Message 5" but not "Message 0"
        Assert.Contains("Message 5", _aiProvider.LastInput, StringComparison.InvariantCultureIgnoreCase);
        Assert.DoesNotContain("Message 0", _aiProvider.LastInput, StringComparison.InvariantCultureIgnoreCase);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }

        _disposed = true;
    }
}

internal sealed class MockSystemPromptServiceForMemory() : ISystemPromptService
{
    private string _userMemory = string.Empty;

    public string GetAgentPrompt(Skill? activeSkill = null) => "You are a helpful assistant.";

    public string GetCompressionPrompt() => "Summarize the conversation.";

    public string GetProjectSummaryPrompt() => "Summarize the project.";

    public string GetMemoryExtractionPrompt() => "Extract memories.";

    public string GetUserMemory() => _userMemory;

    public void SaveUserMemory(string memory) => _userMemory = memory;
}

internal sealed class MockAIProviderForMemory : IAIProvider
{
    private string _nextResponse = string.Empty;
    public string LastInput { get; private set; } = string.Empty;

    public void SetNextResponse(string response) => _nextResponse = response;

    public void AddToHistory(ChatMessage message) { }
    public void ClearHistory() { }
    public string GetLastThinking() => string.Empty;
    public TimeSpan GetLastThinkingDuration() => TimeSpan.Zero;
    public MockResponse GetResponse(string userInput) => new() { Content = _nextResponse };

    public async IAsyncEnumerable<string> StreamResponseAsync(string userInput, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        LastInput = userInput;
        await Task.Yield();
        yield return _nextResponse;
    }

    public IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response) => AsyncEnumerable.Empty<string>();
    public IAsyncEnumerable<string> StreamContentAsync(MockResponse response) => AsyncEnumerable.Empty<string>();

    public Task<AIResponse> GetResponseWithToolsAsync(string userInput, IReadOnlyList<ToolDefinition> tools, List<ToolResult>? toolResults = null, CancellationToken cancellationToken = default)
        => Task.FromResult(new AIResponse { Content = _nextResponse });

    public async IAsyncEnumerable<StreamingToolResponse> StreamResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseWithToolsAsync(userInput, tools, toolResults, cancellationToken).ConfigureAwait(false);
        yield return StreamingToolResponse.FromResponse(response);
    }
}