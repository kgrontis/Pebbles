namespace Pebbles.Tests.Models;

using Pebbles.Models;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class AIResponseTests
{
    [Fact]
    public void AIResponse_InitializesWithDefaults()
    {
        // Act
        var response = new AIResponse();

        // Assert
        Assert.Equal(string.Empty, response.Content);
        Assert.Empty(response.ToolCalls);
        Assert.Equal(0, response.InputTokens);
        Assert.Equal(0, response.OutputTokens);
        Assert.Null(response.Thinking);
    }

    [Fact]
    public void AIResponse_StoresContent()
    {
        // Act
        var response = new AIResponse
        {
            Content = "Hello, world!"
        };

        // Assert
        Assert.Equal("Hello, world!", response.Content);
    }

    [Fact]
    public void AIResponse_StoresTokenCounts()
    {
        // Act
        var response = new AIResponse
        {
            InputTokens = 100,
            OutputTokens = 50
        };

        // Assert
        Assert.Equal(100, response.InputTokens);
        Assert.Equal(50, response.OutputTokens);
    }

    [Fact]
    public void AIResponse_StoresThinking()
    {
        // Act
        var response = new AIResponse
        {
            Content = "Response",
            Thinking = "I thought about this carefully..."
        };

        // Assert
        Assert.Equal("I thought about this carefully...", response.Thinking);
    }

    [Fact]
    public void AIResponse_StoresToolCalls()
    {
        // Arrange
        var toolCall = new ToolCall
        {
            Id = "call_123",
            Type = "function",
            Function = new ToolCallFunction
            {
                Name = "read_file",
                Arguments = "{\"path\": \"test.txt\"}"
            }
        };

        // Act
        var response = new AIResponse
        {
            ToolCalls = [toolCall]
        };

        // Assert
        Assert.Single(response.ToolCalls);
        Assert.Equal("call_123", response.ToolCalls[0].Id);
        Assert.Equal("read_file", response.ToolCalls[0].Function.Name);
    }

    [Fact]
    public void AIResponse_CanHaveMultipleToolCalls()
    {
        // Arrange
        var toolCalls = new List<ToolCall>
        {
            new() { Id = "call_1", Type = "function", Function = new ToolCallFunction { Name = "tool1" } },
            new() { Id = "call_2", Type = "function", Function = new ToolCallFunction { Name = "tool2" } }
        };

        // Act
        var response = new AIResponse { ToolCalls = [.. toolCalls] };

        // Assert
        Assert.Equal(2, response.ToolCalls.Count);
    }
}

public class ToolCallTests
{
    [Fact]
    public void ToolCall_InitializesWithDefaults()
    {
        // Act
        var toolCall = new ToolCall();

        // Assert
        Assert.Equal(string.Empty, toolCall.Id);
        Assert.Equal("function", toolCall.Type); // Default type is "function"
        Assert.NotNull(toolCall.Function);
    }

    [Fact]
    public void ToolCall_StoresAllProperties()
    {
        // Act
        var toolCall = new ToolCall
        {
            Id = "call_abc",
            Type = "function",
            Function = new ToolCallFunction
            {
                Name = "execute",
                Arguments = "{\"cmd\": \"ls\"}"
            }
        };

        // Assert
        Assert.Equal("call_abc", toolCall.Id);
        Assert.Equal("function", toolCall.Type);
        Assert.Equal("execute", toolCall.Function.Name);
        Assert.Equal("{\"cmd\": \"ls\"}", toolCall.Function.Arguments);
    }
}

public class ToolCallFunctionTests
{
    [Fact]
    public void ToolCallFunction_InitializesWithDefaults()
    {
        // Act
        var func = new ToolCallFunction();

        // Assert
        Assert.Equal(string.Empty, func.Name);
        Assert.Equal(string.Empty, func.Arguments);
    }
}