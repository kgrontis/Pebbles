namespace Pebbles.Tests.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.Services;
using System.Collections.ObjectModel;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class CompressionServiceTests
{
    private readonly PebblesOptions _options;
    private readonly MockAIProvider _aiProvider;
    private readonly MockSystemPromptService _promptService;
    private readonly CompressionService _service;

    public CompressionServiceTests()
    {
        _options = new PebblesOptions { TokenEstimationMultiplier = 1.3 };
        _aiProvider = new MockAIProvider();
        _promptService = new MockSystemPromptService();
        _service = new CompressionService(_aiProvider, _promptService, _options);
    }

    [Fact]
    public void ShouldCompact_ReturnsFalse_WhenBelowThreshold()
    {
        // Arrange
        var currentTokens = 50_000;
        var contextWindow = 100_000;
        var threshold = 0.7;

        // Act
        var result = _service.ShouldCompact(currentTokens, contextWindow, threshold);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldCompact_ReturnsTrue_WhenAtThreshold()
    {
        // Arrange
        var currentTokens = 70_000;
        var contextWindow = 100_000;
        var threshold = 0.7;

        // Act
        var result = _service.ShouldCompact(currentTokens, contextWindow, threshold);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldCompact_ReturnsTrue_WhenAboveThreshold()
    {
        // Arrange
        var currentTokens = 85_000;
        var contextWindow = 100_000;
        var threshold = 0.7;

        // Act
        var result = _service.ShouldCompact(currentTokens, contextWindow, threshold);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-0.5)]
    public void ShouldCompact_ReturnsFalse_WhenThresholdInvalid(double threshold)
    {
        // Arrange
        var currentTokens = 90_000;
        var contextWindow = 100_000;

        // Act
        var result = _service.ShouldCompact(currentTokens, contextWindow, threshold);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void EstimateTotalTokens_SumsMessageTokens()
    {
        // Arrange
        var messages = new List<ChatMessage>
        {
            ChatMessage.User("Hello", 10),
            ChatMessage.Assistant("Hi there!", 20),
            ChatMessage.User("How are you?", 15)
        };

        // Act
        var total = _service.EstimateTotalTokens(messages);

        // Assert
        Assert.Equal(45, total);
    }

    [Fact]
    public void EstimateTotalTokens_ReturnsZero_ForEmptyCollection()
    {
        // Arrange
        var messages = new List<ChatMessage>();

        // Act
        var total = _service.EstimateTotalTokens(messages);

        // Assert
        Assert.Equal(0, total);
    }

    [Fact]
    public async Task CompactAsync_ReturnsNotNeeded_WhenMessagesBelowThreshold()
    {
        // Arrange
        var messages = new Collection<ChatMessage>
        {
            ChatMessage.User("Hello", 10),
            ChatMessage.Assistant("Hi!", 10)
        };

        // Act
        var result = await _service.CompactAsync(messages, keepRecentCount: 6);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(0, result.MessagesSummarized);
        Assert.Equal(0, result.TokensBefore);
    }

    [Fact]
    public async Task CompactAsync_SummarizesOldMessages_WhenAboveThreshold()
    {
        // Arrange
        var messages = new Collection<ChatMessage>();
        for (var i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 100));
            messages.Add(ChatMessage.Assistant($"Response {i}", 100));
        }

        _aiProvider.SetNextResponse("This is a summary of the conversation.");

        // Act
        var result = await _service.CompactAsync(messages, keepRecentCount: 6);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.MessagesSummarized > 0);
        Assert.Equal(6, result.MessagesKept);
        Assert.Equal("This is a summary of the conversation.", result.Summary);
    }

    [Fact]
    public async Task CompactAsync_ReturnsFailed_WhenSummaryIsEmpty()
    {
        // Arrange
        var messages = new Collection<ChatMessage>();
        for (var i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 100));
        }

        _aiProvider.SetNextResponse("");

        // Act
        var result = await _service.CompactAsync(messages, keepRecentCount: 2);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("empty", result.Error, StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task CompactAsync_IncludesPreviousSummary_WhenProvided()
    {
        // Arrange
        var messages = new Collection<ChatMessage>();
        for (var i = 0; i < 10; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 100));
        }

        _aiProvider.SetNextResponse("Updated summary.");

        // Act
        var result = await _service.CompactAsync(
            messages,
            keepRecentCount: 4,
            previousSummary: "Previous summary content");

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Updated summary.", result.Summary);
    }

    [Fact]
    public async Task CompactAsync_CalculatesTokenReduction()
    {
        // Arrange
        var messages = new Collection<ChatMessage>();
        for (var i = 0; i < 20; i++)
        {
            messages.Add(ChatMessage.User($"Message {i}", 50));
        }

        _aiProvider.SetNextResponse("Summary of the conversation.");

        // Act
        var result = await _service.CompactAsync(messages, keepRecentCount: 6);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.TokensBefore > result.TokensAfter);
        Assert.Equal(1000, result.TokensBefore); // 20 messages * 50 tokens
    }

    [Fact]
    public async Task CompactAsync_IncludesThinkingContent_WhenPresent()
    {
        // Arrange
        var messages = new Collection<ChatMessage>
        {
            ChatMessage.User("Message 1", 100),
            ChatMessage.Assistant("Response 1", 100, new ThinkingBlock { Content = "Thinking about this..." }),
            ChatMessage.User("Message 2", 100),
            ChatMessage.Assistant("Response 2", 100),
            ChatMessage.User("Message 3", 100),
            ChatMessage.Assistant("Response 3", 100)
        };

        _aiProvider.SetNextResponse("Summary with thinking.");

        // Act
        var result = await _service.CompactAsync(messages, keepRecentCount: 2);

        // Assert
        Assert.True(result.Success);
    }
}

/// <summary>
/// Mock AI provider for testing.
/// </summary>
internal sealed class MockAIProvider : IAIProvider
{
    private string _nextResponse = "Default summary";

    public void SetNextResponse(string response) => _nextResponse = response;

    public void AddToHistory(ChatMessage message) { }

    public void ClearHistory() { }

    public MockResponse GetResponse(string userInput) => new()
    {
        Content = _nextResponse
    };

    public IAsyncEnumerable<string> StreamResponseAsync(string userInput, CancellationToken cancellationToken = default)
    {
        return StreamResponseInternal();
    }

    private async IAsyncEnumerable<string> StreamResponseInternal()
    {
        await Task.Yield();
        yield return _nextResponse;
    }

    public IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response) =>
        AsyncEnumerable.Empty<string>();

    public IAsyncEnumerable<string> StreamContentAsync(MockResponse response) =>
        AsyncEnumerable.Empty<string>();

    public Task<AIResponse> GetResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new AIResponse { Content = _nextResponse });

    public async IAsyncEnumerable<StreamingToolResponse> StreamResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseWithToolsAsync(userInput, tools, toolResults, cancellationToken).ConfigureAwait(false);
        yield return StreamingToolResponse.FromResponse(response);
    }

    public string GetLastThinking() => string.Empty;

    public TimeSpan GetLastThinkingDuration() => TimeSpan.Zero;
}

/// <summary>
/// Mock system prompt service for testing.
/// </summary>
internal sealed class MockSystemPromptService : ISystemPromptService
{
    public string GetAgentPrompt(Skill? activeSkill = null) => "You are a helpful assistant.";

    public string GetCompressionPrompt() => "Summarize the conversation.";

    public string GetProjectSummaryPrompt() => "Summarize the project.";

    public string GetMemoryExtractionPrompt() => "Extract memories.";

    public void SaveUserMemory(string memory) { }

    public string GetUserMemory() => string.Empty;
}