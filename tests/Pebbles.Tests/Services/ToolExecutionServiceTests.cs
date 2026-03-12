namespace Pebbles.Tests.Services;

using Pebbles.Models;
using Pebbles.Services;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class ToolExecutionServiceTests
{
    [Fact]
    public async Task ExecuteToolLoopAsync_ReturnsResponse_WhenNoToolCalls()
    {
        // Arrange
        var provider = new MockAIProviderWithTools();
        provider.SetNextResponse(new AIResponse { Content = "Hello, I can help you!" });
        var registry = new ToolRegistry(null);
        var service = new ToolExecutionService(provider, registry);

        // Act
        var result = await service.ExecuteToolLoopAsync("Hello");

        // Assert
        Assert.Equal("Hello, I can help you!", result.Content);
        // Thinking may be empty but not null
        Assert.True(string.IsNullOrEmpty(result.Thinking?.Content));
    }

    [Fact]
    public async Task ExecuteToolLoopAsync_ExecutesTool_WhenToolCallPresent()
    {
        // Arrange
        var provider = new MockAIProviderWithTools();
        provider.SetNextResponse(new AIResponse
        {
            Content = "",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = "echo",
                        Arguments = "{\"message\": \"test\"}"
                    }
                }
            ]
        });
        provider.SetNextResponse(new AIResponse { Content = "Done!" });

        var registry = new ToolRegistry(null);
        registry.RegisterTool(new EchoTool());
        var service = new ToolExecutionService(provider, registry);

        // Act
        var result = await service.ExecuteToolLoopAsync("Echo test");

        // Assert
        Assert.Equal("Done!", result.Content);
    }

    [Fact]
    public async Task ExecuteToolLoopAsync_StopsAfterMaxIterations()
    {
        // Arrange
        var provider = new MockAIProviderWithTools();
        // Always return a tool call to test max iterations
        provider.SetNextResponse(new AIResponse
        {
            Content = "",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = "echo",
                        Arguments = "{\"message\": \"loop\"}"
                    }
                }
            ]
        }, infiniteToolCalls: true);

        var registry = new ToolRegistry(null);
        registry.RegisterTool(new EchoTool());
        var service = new ToolExecutionService(provider, registry);

        // Act
        var result = await service.ExecuteToolLoopAsync("Test");

        // Assert - Should complete without hanging (max 5 iterations)
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteToolLoopAsync_IncludesThinking_WhenPresent()
    {
        // Arrange
        var provider = new MockAIProviderWithTools();
        provider.SetNextResponse(new AIResponse
        {
            Content = "Response with thinking",
            Thinking = "I am thinking about this..."
        });
        provider.SetLastThinking("I am thinking about this...", TimeSpan.FromMilliseconds(100));

        var registry = new ToolRegistry(null);
        var service = new ToolExecutionService(provider, registry);

        // Act
        var result = await service.ExecuteToolLoopAsync("Hello");

        // Assert
        Assert.Equal("Response with thinking", result.Content);
        Assert.NotNull(result.Thinking);
        Assert.Equal("I am thinking about this...", result.Thinking.Content);
    }

    [Fact]
    public async Task ExecuteToolLoopAsync_HandlesToolError()
    {
        // Arrange
        var provider = new MockAIProviderWithTools();
        provider.SetNextResponse(new AIResponse
        {
            Content = "",
            ToolCalls =
            [
                new ToolCall
                {
                    Id = "call_1",
                    Type = "function",
                    Function = new ToolCallFunction
                    {
                        Name = "fail",
                        Arguments = "{}"
                    }
                }
            ]
        });
        provider.SetNextResponse(new AIResponse { Content = "Handled the error" });

        var registry = new ToolRegistry(null);
        registry.RegisterTool(new FailingTool());
        var service = new ToolExecutionService(provider, registry);

        // Act
        var result = await service.ExecuteToolLoopAsync("Test");

        // Assert
        Assert.Equal("Handled the error", result.Content);
    }
}

/// <summary>
/// Mock AI provider that supports tool calls.
/// </summary>
internal sealed class MockAIProviderWithTools : IAIProvider
{
    private readonly List<ChatMessage> _history = [];
    private AIResponse? _nextResponse;
    private string? _lastThinking;
    private TimeSpan _lastThinkingDuration;
    private bool _infiniteToolCalls;

    public void SetNextResponse(AIResponse response, bool infiniteToolCalls = false)
    {
        _nextResponse = response;
        _infiniteToolCalls = infiniteToolCalls;
    }

    public void SetLastThinking(string thinking, TimeSpan duration)
    {
        _lastThinking = thinking;
        _lastThinkingDuration = duration;
    }

    public void AddToHistory(ChatMessage message) => _history.Add(message);
    public void ClearHistory() => _history.Clear();
    public string GetLastThinking() => _lastThinking ?? string.Empty;
    public TimeSpan GetLastThinkingDuration() => _lastThinkingDuration;

    public MockResponse GetResponse(string userInput) => new() { Content = _nextResponse?.Content ?? "" };

    public IAsyncEnumerable<string> StreamResponseAsync(string userInput, CancellationToken cancellationToken = default)
    {
        return AsyncEnumerable.Empty<string>();
    }

    public IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response) =>
        AsyncEnumerable.Empty<string>();

    public IAsyncEnumerable<string> StreamContentAsync(MockResponse response) =>
        AsyncEnumerable.Empty<string>();

    public Task<AIResponse> GetResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        CancellationToken cancellationToken = default)
    {
        var response = _nextResponse ?? new AIResponse { Content = "Default response" };

        // If not infinite tool calls, clear after first use
        if (!_infiniteToolCalls)
        {
            _nextResponse = new AIResponse { Content = "Follow-up response" };
        }

        return Task.FromResult(response);
    }
}

/// <summary>
/// Simple echo tool for testing.
/// </summary>
internal sealed class EchoTool : ITool
{
    public string Name => "echo";
    public string Description => "Echoes a message back";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters
            {
                Type = "object",
                Properties = new Dictionary<string, ToolParameterProperty>
                {
                    ["message"] = new() { Type = "string", Description = "Message to echo" }
                },
                Required = ["message"]
            }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = true,
            Content = arguments
        });
    }
}

/// <summary>
/// Tool that always fails for testing error handling.
/// </summary>
internal sealed class FailingTool : ITool
{
    public string Name => "fail";
    public string Description => "Always fails";

    public ToolDefinition GetDefinition() => new()
    {
        Function = new FunctionDefinition
        {
            Name = Name,
            Description = Description,
            Parameters = new ToolParameters { Type = "object" }
        }
    };

    public Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ToolExecutionResult
        {
            Success = false,
            Error = "This tool always fails"
        });
    }
}