
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pebbles.Configuration;
using Pebbles.Models;

namespace Pebbles.Services;
/// <summary>
/// Anthropic Claude API provider with streaming support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the AnthropicProvider.
/// </remarks>
/// <param name="httpClient">The HttpClient configured with appropriate headers and timeout.</param>
/// <param name="options">The Pebbles configuration options.</param>
internal sealed class AnthropicProvider(HttpClient httpClient, PebblesOptions options) : IAIProvider
{
    private readonly List<ChatMessage> _conversationHistory = [];
    private string _lastThinking = string.Empty;
    private TimeSpan _thinkingDuration = TimeSpan.Zero;
    private readonly Stopwatch _thinkingStopwatch = new();
    private int _lastInputTokens;
    private int _lastOutputTokens;

    public void AddToHistory(ChatMessage message)
    {
        _conversationHistory.Add(message);
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    public string GetLastThinking() => _lastThinking;

    public TimeSpan GetLastThinkingDuration() => _thinkingDuration;

    public MockResponse GetResponse(string userInput)
    {
        throw new NotImplementedException("Use streaming methods instead");
    }

    public async IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response)
    {
        // Anthropic thinking blocks - stream if available
        if (!string.IsNullOrEmpty(response.Thinking))
        {
            foreach (var word in response.Thinking.Split(' '))
            {
                yield return word + " ";
                await Task.Delay(10).ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<string> StreamContentAsync(MockResponse response)
    {
        foreach (var word in response.Content.Split(' '))
        {
            yield return word + " ";
            await Task.Delay(10).ConfigureAwait(false);
        }
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        string userInput,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

        var request = new AnthropicRequest
        {
            Model = options.DefaultModel,
            MaxTokens = 4096,
            Stream = true,
            Messages = BuildAnthropicMessages()
        };

        var url = $"{GetBaseUrl()}/v1/messages";
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        var responseContent = new StringBuilder();
        var thinkingContent = new StringBuilder();
        var isThinking = false;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.InvariantCultureIgnoreCase)) continue;

            var data = line[5..].Trim();
            if (string.IsNullOrEmpty(data)) continue;

            var chunk = JsonSerializer.Deserialize<AnthropicStreamChunk>(data);
            if (chunk is null) continue;

            // Handle content block start events
            if (chunk.Type == "content_block_start")
            {
                if (chunk.ContentBlock?.Type == "thinking")
                {
                    isThinking = true;
                    _thinkingStopwatch.Restart();
                }
            }
            // Handle content block stop events
            else if (chunk.Type == "content_block_stop")
            {
                if (isThinking)
                {
                    isThinking = false;
                    _thinkingStopwatch.Stop();
                    _thinkingDuration = _thinkingStopwatch.Elapsed;
                }
            }
            // Handle delta events
            else if (chunk.Type == "content_block_delta")
            {
                // Thinking delta
                if (!string.IsNullOrEmpty(chunk.Delta?.Thinking))
                {
                    thinkingContent.Append(chunk.Delta.Thinking);
                    yield return $"[THINKING]{chunk.Delta.Thinking}";
                }
                // Text delta
                else if (!string.IsNullOrEmpty(chunk.Delta?.Text))
                {
                    responseContent.Append(chunk.Delta.Text);
                    yield return chunk.Delta.Text;
                }
            }
            // Handle message_delta for usage info
            else if (chunk.Type == "message_delta" && chunk.Usage is not null)
            {
                _lastOutputTokens = chunk.Usage.OutputTokens;
            }
            // Handle message_start for input tokens
            else if (chunk.Type == "message_start" && chunk.Message?.Usage is not null)
            {
                _lastInputTokens = chunk.Message.Usage.InputTokens;
            }
        }

        // Store thinking content
        _lastThinking = thinkingContent.ToString();
        if (isThinking)
        {
            _thinkingStopwatch.Stop();
            _thinkingDuration = _thinkingStopwatch.Elapsed;
        }

        if (_lastOutputTokens == 0)
        {
            _lastOutputTokens = EstimateTokens(responseContent.ToString());
        }
        _conversationHistory.Add(ChatMessage.Assistant(responseContent.ToString(), _lastOutputTokens));
    }

    public async Task<AIResponse> GetResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        CancellationToken cancellationToken = default)
    {
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

        var request = new AnthropicRequest
        {
            Model = options.DefaultModel,
            MaxTokens = 4096,
            Messages = BuildAnthropicMessages(),
            Tools = [.. tools.Select(t => new AnthropicTool
            {
                Name = t.Function?.Name ?? "",
                Description = t.Function?.Description ?? "",
                InputSchema = t.Function?.Parameters
            })]
        };

        var url = $"{GetBaseUrl()}/v1/messages";
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var anthropicResponse = JsonSerializer.Deserialize<AnthropicResponse>(responseString);

        // Extract thinking content from thinking blocks
        var thinkingContent = new StringBuilder();
        var textContent = new StringBuilder();

        if (anthropicResponse?.Content is not null)
        {
            foreach (var contentBlock in anthropicResponse.Content)
            {
                if (contentBlock.Type == "thinking")
                {
                    thinkingContent.Append(contentBlock.Thinking ?? "");
                }
                else if (contentBlock.Type == "text")
                {
                    textContent.Append(contentBlock.Text ?? "");
                }
            }
        }

        var aiResponse = new AIResponse
        {
            Content = textContent.Length > 0 ? textContent.ToString() : anthropicResponse?.Content?.FirstOrDefault()?.Text ?? "",
            InputTokens = anthropicResponse?.Usage?.InputTokens ?? 0,
            OutputTokens = anthropicResponse?.Usage?.OutputTokens ?? 0,
            Thinking = thinkingContent.Length > 0 ? thinkingContent.ToString() : null
        };

        // Store thinking for GetLastThinking()
        _lastThinking = thinkingContent.ToString();

        // Handle tool calls from Anthropic response
        if (anthropicResponse?.Content is not null)
        {
            foreach (var contentBlock in anthropicResponse.Content)
            {
                if (contentBlock.Type == "tool_use" && contentBlock.ToolUse is not null)
                {
                    aiResponse.ToolCalls.Add(new ToolCall
                    {
                        Id = contentBlock.ToolUse.Id ?? "",
                        Type = "function",
                        Function = new ToolCallFunction
                        {
                            Name = contentBlock.ToolUse.Name ?? "",
                            Arguments = JsonSerializer.Serialize(contentBlock.ToolUse.Input)
                        }
                    });
                }
            }
        }

        _lastInputTokens = aiResponse.InputTokens;
        _lastOutputTokens = aiResponse.OutputTokens;

        return aiResponse;
    }

    private List<AnthropicMessage> BuildAnthropicMessages()
    {
        return [.. _conversationHistory
            .Where(m => m.Role != ChatRole.Tool)
            .Select(m => new AnthropicMessage
            {
                Role = m.Role == ChatRole.User ? "user" : "assistant",
                Content = m.Content
            })];
    }

    private Uri GetBaseUrl()
    {
        return options.AnthropicBaseUrl ?? new Uri("https://api.anthropic.com");
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.3);
    }
}

#region Anthropic DTOs

internal class AnthropicRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; set; }

    [JsonPropertyName("messages")]
    public List<AnthropicMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("tools")]
    public List<AnthropicTool>? Tools { get; set; }
}

internal class AnthropicMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class AnthropicTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("input_schema")]
    public ToolParameters? InputSchema { get; set; }
}

internal class AnthropicResponse
{
    [JsonPropertyName("content")]
    public List<AnthropicContentBlock>? Content { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

internal class AnthropicContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("input")]
    public JsonElement? Input { get; set; }

    [JsonIgnore]
    public AnthropicToolUse? ToolUse => Type == "tool_use" ? new AnthropicToolUse
    {
        Id = Id,
        Name = Name,
        Input = Input
    } : null;
}

internal class AnthropicToolUse
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public JsonElement? Input { get; set; }
}

internal class AnthropicStreamChunk
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("delta")]
    public AnthropicDelta? Delta { get; set; }

    [JsonPropertyName("content_block")]
    public AnthropicContentBlock? ContentBlock { get; set; }

    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }

    [JsonPropertyName("message")]
    public AnthropicMessageStart? Message { get; set; }
}

internal class AnthropicMessageStart
{
    [JsonPropertyName("usage")]
    public AnthropicUsage? Usage { get; set; }
}

internal class AnthropicDelta
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("thinking")]
    public string? Thinking { get; set; }
}

internal class AnthropicUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

#endregion
