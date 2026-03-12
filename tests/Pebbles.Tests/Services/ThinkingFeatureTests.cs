namespace Pebbles.Tests.Services;

using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.Services;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

/// <summary>
/// Tests for the thinking feature in AI providers.
/// </summary>
public class ThinkingFeatureTests
{
    [Fact]
    public async Task OpenAIProvider_ExtractsReasoningContent_FromStreamingResponse()
    {
        // Arrange
        using var httpResponse = CreateOpenAIStreamingResponse(
            reasoningContent: "Let me think about this...",
            content: "Here's my answer.");

        var (provider, _) = CreateOpenAIProvider(httpResponse);

        // Act
        var tokens = new List<string>();
        await foreach (var token in provider.StreamResponseAsync("Hello"))
        {
            tokens.Add(token);
        }

        // Assert
        Assert.Contains(tokens, t => t.StartsWith("[THINKING]", StringComparison.InvariantCultureIgnoreCase));
        Assert.Contains(tokens, t => t.Contains("think about this", StringComparison.InvariantCultureIgnoreCase));
        Assert.Equal("Let me think about this...", provider.GetLastThinking());
    }

    [Fact]
    public async Task OpenAIProvider_TracksThinkingDuration()
    {
        // Arrange
        using var httpResponse = CreateOpenAIStreamingResponse(
            reasoningContent: "Thinking...",
            content: "Answer");

        var (provider, _) = CreateOpenAIProvider(httpResponse);

        // Act
        await foreach (var _ in provider.StreamResponseAsync("Hello")) { }

        // Assert
        Assert.True(provider.GetLastThinkingDuration() > TimeSpan.Zero);
    }

    [Fact]
    public async Task OpenAIProvider_HandlesNoThinking()
    {
        // Arrange
        using var httpResponse = CreateOpenAIStreamingResponse(
            reasoningContent: null,
            content: "Direct answer.");

        var (provider, _) = CreateOpenAIProvider(httpResponse);

        // Act
        await foreach (var _ in provider.StreamResponseAsync("Hello")) { }

        // Assert
        Assert.Equal(string.Empty, provider.GetLastThinking());
    }

    [Fact]
    public async Task OpenAIProvider_ExtractsThinkingFromNonStreamingResponse()
    {
        // Arrange
        var jsonResponse = """
        {
            "choices": [{
                "message": {
                    "content": "My response",
                    "reasoning_content": "My reasoning process"
                }
            }],
            "usage": { "prompt_tokens": 10, "completion_tokens": 20 }
        }
        """;

        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var (provider, _) = CreateOpenAIProvider(httpResponse);

        // Act
        var response = await provider.GetResponseWithToolsAsync("Hello", []);

        // Assert
        Assert.Equal("My reasoning process", response.Thinking);
        Assert.Equal("My reasoning process", provider.GetLastThinking());
    }

    [Fact]
    public async Task AnthropicProvider_ExtractsThinking_FromStreamingResponse()
    {
        // Arrange
        using var httpResponse = CreateAnthropicStreamingResponse(
            thinkingContent: "Analyzing the question...",
            textContent: "Here's my response.");

        var (provider, _) = CreateAnthropicProvider(httpResponse);

        // Act
        var tokens = new List<string>();
        await foreach (var token in provider.StreamResponseAsync("Hello"))
        {
            tokens.Add(token);
        }

        // Assert
        Assert.Contains(tokens, t => t.StartsWith("[THINKING]", StringComparison.InvariantCultureIgnoreCase));
        Assert.Contains("Analyzing", provider.GetLastThinking(), StringComparison.InvariantCultureIgnoreCase);
    }

    [Fact]
    public async Task AnthropicProvider_TracksThinkingDuration()
    {
        // Arrange
        using var httpResponse = CreateAnthropicStreamingResponse(
            thinkingContent: "Thinking...",
            textContent: "Answer");

        var (provider, _) = CreateAnthropicProvider(httpResponse);

        // Act
        await foreach (var _ in provider.StreamResponseAsync("Hello")) { }

        // Assert
        Assert.True(provider.GetLastThinkingDuration() >= TimeSpan.Zero);
    }

    [Fact]
    public async Task AnthropicProvider_ExtractsThinkingFromNonStreamingResponse()
    {
        // Arrange
        var jsonResponse = """
        {
            "content": [
                { "type": "thinking", "thinking": "My thought process" },
                { "type": "text", "text": "My response" }
            ],
            "usage": { "input_tokens": 10, "output_tokens": 20 }
        }
        """;

        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var (provider, _) = CreateAnthropicProvider(httpResponse);

        // Act
        var response = await provider.GetResponseWithToolsAsync("Hello", []);

        // Assert
        Assert.Equal("My thought process", response.Thinking);
    }

    [Fact]
    public async Task AnthropicProvider_HandlesMultipleThinkingBlocks()
    {
        // Arrange
        var jsonResponse = """
        {
            "content": [
                { "type": "thinking", "thinking": "First thought" },
                { "type": "thinking", "thinking": "Second thought" },
                { "type": "text", "text": "Final answer" }
            ],
            "usage": { "input_tokens": 10, "output_tokens": 20 }
        }
        """;

        using var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
        };

        var (provider, _) = CreateAnthropicProvider(httpResponse);

        // Act
        var response = await provider.GetResponseWithToolsAsync("Hello", []);

        // Assert
        Assert.Contains("First thought", response.Thinking, StringComparison.InvariantCultureIgnoreCase);
        Assert.Contains("Second thought", response.Thinking, StringComparison.InvariantCultureIgnoreCase);
    }

    #region Helper Methods

    private static (OpenAIProvider provider, HttpClient client) CreateOpenAIProvider(HttpResponseMessage response)
    {
        using var handler = new MockHttpMessageHandler(response);
        var client = new HttpClient(handler);
        var options = new PebblesOptions { DefaultModel = "gpt-4" };
        return (new OpenAIProvider(client, options), client);
    }

    private static (AnthropicProvider provider, HttpClient client) CreateAnthropicProvider(HttpResponseMessage response)
    {
        using var handler = new MockHttpMessageHandler(response);
        var client = new HttpClient(handler);
        var options = new PebblesOptions { DefaultModel = "claude-3-opus" };
        return (new AnthropicProvider(client, options), client);
    }

    private static HttpResponseMessage CreateOpenAIStreamingResponse(string? reasoningContent, string content)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(reasoningContent))
        {
            var reasoningChunk = new
            {
                choices = new[]
                {
                    new { delta = new { reasoning_content = reasoningContent } }
                }
            };
            sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(reasoningChunk)}");
        }

        var contentChunk = new
        {
            choices = new[]
            {
                new { delta = new { content } }
            }
        };
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(contentChunk)}");
        sb.AppendLine("data: [DONE]");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/event-stream")
        };
    }

    private static HttpResponseMessage CreateAnthropicStreamingResponse(string thinkingContent, string textContent)
    {
        var sb = new StringBuilder();

        // Content block start - thinking
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_start", content_block = new { type = "thinking" } })}");

        // Thinking delta
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_delta", delta = new { thinking = thinkingContent } })}");

        // Content block stop
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_stop" })}");

        // Content block start - text
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_start", content_block = new { type = "text" } })}");

        // Text delta
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_delta", delta = new { text = textContent } })}");

        // Content block stop
        sb.AppendLine(CultureInfo.InvariantCulture, $"data: {JsonSerializer.Serialize(new { type = "content_block_stop" })}");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sb.ToString(), Encoding.UTF8, "text/event-stream")
        };
    }

    #endregion
}

/// <summary>
/// Mock HTTP message handler for testing.
/// </summary>
internal sealed class MockHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(response);
    }
}