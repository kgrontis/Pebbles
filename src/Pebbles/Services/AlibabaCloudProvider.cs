namespace Pebbles.Services;

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.Services.Commands;

/// <summary>
/// Alibaba Cloud AI provider with streaming support (OpenAI-compatible API).
/// </summary>
/// <remarks>
/// Initializes a new instance of the AlibabaCloudProvider.
/// </remarks>
/// <param name="httpClient">The HttpClient configured with appropriate headers and timeout.</param>
/// <param name="options">The Pebbles configuration options.</param>
/// <param name="contextManager">The context manager for managing conversation context.</param>
/// <param name="fileService">The file service for file operations.</param>
/// <param name="systemPromptService">The system prompt service for generating system prompts.</param>
/// <param name="skillCommands">The skill commands for accessing the active skill.</param>
public sealed class AlibabaCloudProvider(
    HttpClient httpClient,
    PebblesOptions options,
    ContextManager contextManager,
    IFileService fileService,
    ISystemPromptService systemPromptService,
    SkillCommands skillCommands) : IAIProvider
{
    private readonly List<ChatMessage> _conversationHistory = [];
    private string _lastThinking = string.Empty;
    private TimeSpan _thinkingDuration = TimeSpan.Zero;
    private readonly Stopwatch _thinkingStopwatch = new();
    private int _lastInputTokens;
    private int _lastOutputTokens;
    private int _lastReasoningTokens;
    private int _lastCachedTokens;

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    public void AddToHistory(ChatMessage message)
    {
        _conversationHistory.Add(message);
    }

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
    }

    /// <summary>
    /// Gets the last thinking content from a streaming response.
    /// </summary>
    public string GetLastThinking() => _lastThinking;

    /// <summary>
    /// Gets the last thinking duration.
    /// </summary>
    public TimeSpan GetLastThinkingDuration() => _thinkingDuration;

    /// <summary>
    /// Gets the last token usage.
    /// </summary>
    public (int Input, int Output) GetLastTokenUsage() => (_lastInputTokens, _lastOutputTokens);

    /// <summary>
    /// Streams response tokens from the API in real-time.
    /// </summary>
    public async IAsyncEnumerable<string> StreamResponseAsync(string userInput, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message to history
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

        var messages = BuildMessages();

        var request = new ChatCompletionRequest
        {
            Model = options.DefaultModel,
            Messages = messages,
            Stream = true,
            StreamOptions = new StreamOptions { IncludeUsage = true },
            EnableThinking = true
        };

        var url = $"{options.AlibabaCloudBaseUrl}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request, AlibabaCloudJsonContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json");

        var responseContent = new StringBuilder();
        var thinkingContent = new StringBuilder();
        var isThinking = false;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        var policy = RetryPolicies.GetApiPolicy();
        using var response = await policy.ExecuteAsync(
            async ct => await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        var responseMessage = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: ", StringComparison.OrdinalIgnoreCase))
                continue;

            var data = line[6..].Trim();
            if (data == "[DONE]")
                break;

            if (string.IsNullOrEmpty(data))
                continue;

            StreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(data, AlibabaCloudJsonContext.Default.StreamChunk);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk?.Choices is null || chunk.Choices.Count == 0)
            {
                // Check for usage in final chunk (choices may be empty)
                if (chunk?.Usage is not null)
                {
                    _lastInputTokens = chunk.Usage.PromptTokens;
                    _lastOutputTokens = chunk.Usage.CompletionTokens;
                    _lastReasoningTokens = chunk.Usage.CompletionTokensDetails?.ReasoningTokens ?? 0;
                    _lastCachedTokens = chunk.Usage.PromptTokensDetails?.CachedTokens ?? 0;
                }
                continue;
            }

            var delta = chunk.Choices[0].Delta;

            // Handle reasoning/thinking content separately from regular content
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
            {
                if (!isThinking)
                {
                    isThinking = true;
                    _thinkingStopwatch.Restart();
                }
                thinkingContent.Append(delta.ReasoningContent);
                // Prefix thinking content with marker for renderer to style
                yield return $"[THINKING]{delta.ReasoningContent}";
                continue;
            }

            if (!string.IsNullOrEmpty(delta?.Content))
            {
                if (isThinking)
                {
                    isThinking = false;
                    _thinkingStopwatch.Stop();
                    _thinkingDuration = _thinkingStopwatch.Elapsed;
                }
                responseContent.Append(delta.Content);
                yield return delta.Content;
            }
        }

        // Store thinking content and duration
        _lastThinking = thinkingContent.ToString();
        if (isThinking)
        {
            _thinkingStopwatch.Stop();
            _thinkingDuration = _thinkingStopwatch.Elapsed;
        }

        // Store final stats
        _lastOutputTokens = EstimateTokens(responseContent.ToString());

        // Add assistant response to history
        _conversationHistory.Add(ChatMessage.Assistant(responseContent.ToString(), _lastOutputTokens));
    }

    private List<ChatMessageItem> BuildMessages()
    {
        // Build enhanced system prompt with context
        var context = contextManager.GetContextForPrompt();
        var files = fileService.FormatFilesForPrompt();

        var systemPrompt = systemPromptService.GetAgentPrompt(skillCommands.ActiveSkill);
        if (!string.IsNullOrEmpty(context))
            systemPrompt += $"\n\n{context}";
        if (!string.IsNullOrEmpty(files))
            systemPrompt += $"\n\n{files}";

        var messages = new List<ChatMessageItem>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var msg in _conversationHistory)
        {
            messages.Add(new ChatMessageItem
            {
#pragma warning disable CA1308 // API requires lowercase role names
                Role = msg.Role.ToString().ToLowerInvariant(),
#pragma warning restore CA1308
                Content = msg.Content
            });
        }

        return messages;
    }

    private static int EstimateTokens(string text) =>
        (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.3);

    // Non-streaming interface (for compatibility)
    public MockResponse GetResponse(string userInput)
    {
        var content = new StringBuilder();
        var thinking = new StringBuilder();

        foreach (var token in StreamResponseAsync(userInput).ToEnumerable())
        {
            // This is a simplified version - in practice we'd track thinking separately
            content.Append(token);
        }

        _lastThinking = thinking.ToString();

        return new MockResponse
        {
            Content = content.ToString(),
            Thinking = _lastThinking,
            ThinkingDuration = _thinkingDuration
        };
    }

    public async IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response)
    {
        // For streaming, thinking is handled during StreamResponseAsync
        // This method is for displaying cached thinking
        if (!string.IsNullOrEmpty(response.Thinking))
        {
            var words = response.Thinking.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            foreach (var word in words)
            {
                yield return word + " ";
                await Task.Delay(15).ConfigureAwait(false);
            }
        }
    }

    public async IAsyncEnumerable<string> StreamContentAsync(MockResponse response)
    {
        // For non-streaming compatibility
        var chars = response.Content;
        var buffer = "";
        foreach (var c in chars)
        {
            buffer += c;
            if (c is ' ' or '\n' or '.' or ',' or ';' or ':' or '|' or '`' or '#' or '-' or '*')
            {
                yield return buffer;
                buffer = "";
                await Task.Delay(5).ConfigureAwait(false);
            }
        }
        if (buffer.Length > 0)
            yield return buffer;
    }

    public async Task<AIResponse> GetResponseWithToolsAsync(
    string userInput,
    IReadOnlyList<ToolDefinition> tools,
    List<ToolResult>? toolResults = null,
    CancellationToken cancellationToken = default)
    {
        // Add user message to history
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

        // Add tool results as separate messages if provided
        if (toolResults is not null && toolResults.Count > 0)
        {
            foreach (var result in toolResults)
            {
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.Tool,
                    Content = result.Content,
                    TokenCount = 0
                });
            }
        }

        var messages = BuildMessages();

        var request = new ChatCompletionRequest
        {
            Model = options.DefaultModel,
            Messages = messages,
            Stream = false,
            Tools = tools,
            ToolChoice = "auto"
        };

        var url = $"{options.AlibabaCloudBaseUrl}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request, AlibabaCloudJsonContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        var policy = RetryPolicies.GetApiPolicy();
        using var response = await policy.ExecuteAsync(
            async ct => await httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        var responseMessage = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseString);

        var message = chatResponse?.Choices?.FirstOrDefault()?.Message;
        var thinkingContent = message?.ReasoningContent;

        var aiResponse = new AIResponse
        {
            Content = message?.Content ?? "",
            InputTokens = chatResponse?.Usage?.PromptTokens ?? 0,
            OutputTokens = chatResponse?.Usage?.CompletionTokens ?? 0,
            Thinking = !string.IsNullOrEmpty(thinkingContent) ? thinkingContent : null,
            ReasoningTokens = chatResponse?.Usage?.CompletionTokensDetails?.ReasoningTokens ?? 0,
            CachedTokens = chatResponse?.Usage?.PromptTokensDetails?.CachedTokens ?? 0
        };

        // Store thinking for GetLastThinking()
        _lastThinking = thinkingContent ?? string.Empty;

        // Extract tool calls if present
        if (chatResponse?.Choices?.FirstOrDefault()?.Message?.ToolCalls is not null)
        {
            foreach (var toolCall in chatResponse.Choices.First()?.Message?.ToolCalls ?? Enumerable.Empty<ResponseToolCall>())
            {
                aiResponse.ToolCalls.Add(new ToolCall
                {
                    Id = toolCall.Id ?? "",
                    Type = toolCall.Type ?? "function",
                    Function = new ToolCallFunction
                    {
                        Name = toolCall.Function?.Name ?? "",
                        Arguments = toolCall.Function?.Arguments ?? "{}"
                    }
                });
            }
        }

        // Update stats
        _lastInputTokens = aiResponse.InputTokens;
        _lastOutputTokens = aiResponse.OutputTokens;

        // Add assistant response to history (only if no tool calls - tool calls will be handled separately)
        if (aiResponse.ToolCalls.Count == 0)
        {
            _conversationHistory.Add(ChatMessage.Assistant(aiResponse.Content, aiResponse.OutputTokens));
        }

        return aiResponse;
    }

    /// <summary>
    /// Streams response with tool calling support, showing thinking in real-time.
    /// </summary>
    public async IAsyncEnumerable<StreamingToolResponse> StreamResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Add user message to history
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

        // Add tool results as separate messages if provided
        if (toolResults is not null && toolResults.Count > 0)
        {
            foreach (var result in toolResults)
            {
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.Tool,
                    Content = result.Content,
                    TokenCount = 0
                });
            }
        }

        var messages = BuildMessages();

        var request = new ChatCompletionRequest
        {
            Model = options.DefaultModel,
            Messages = messages,
            Stream = true,
            StreamOptions = new StreamOptions { IncludeUsage = true },
            EnableThinking = true,
            Tools = tools,
            ToolChoice = "auto"
        };

        var url = $"{options.AlibabaCloudBaseUrl}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request, AlibabaCloudJsonContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json");

        var responseContent = new StringBuilder();
        var thinkingContent = new StringBuilder();
        var isThinking = false;
        var toolCalls = new Dictionary<int, ToolCall>();
        var currentToolCallId = new Dictionary<int, string>();
        var currentToolCallName = new Dictionary<int, StringBuilder>();
        var currentToolCallArgs = new Dictionary<int, StringBuilder>();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        var policy = RetryPolicies.GetApiPolicy();
        using var response = await policy.ExecuteAsync(
            async ct => await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: ", StringComparison.OrdinalIgnoreCase))
                continue;

            var data = line[6..].Trim();
            if (data == "[DONE]")
                break;

            if (string.IsNullOrEmpty(data))
                continue;

            StreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(data, AlibabaCloudJsonContext.Default.StreamChunk);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk?.Choices is null || chunk.Choices.Count == 0)
            {
                // Check for usage in final chunk (choices may be empty)
                if (chunk?.Usage is not null)
                {
                    _lastInputTokens = chunk.Usage.PromptTokens;
                    _lastOutputTokens = chunk.Usage.CompletionTokens;
                    _lastReasoningTokens = chunk.Usage.CompletionTokensDetails?.ReasoningTokens ?? 0;
                    _lastCachedTokens = chunk.Usage.PromptTokensDetails?.CachedTokens ?? 0;
                }
                continue;
            }

            var choice = chunk.Choices[0];
            var delta = choice.Delta;

            // Handle reasoning/thinking content separately from regular content
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
            {
                if (!isThinking)
                {
                    isThinking = true;
                    _thinkingStopwatch.Restart();
                }
                thinkingContent.Append(delta.ReasoningContent);
                yield return StreamingToolResponse.FromToken($"[THINKING]{delta.ReasoningContent}");
                continue;
            }

            // Handle regular content
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                if (isThinking)
                {
                    isThinking = false;
                    _thinkingStopwatch.Stop();
                    _thinkingDuration = _thinkingStopwatch.Elapsed;
                }
                responseContent.Append(delta.Content);
                yield return StreamingToolResponse.FromToken(delta.Content);
            }

            // Handle streaming tool calls
            if (choice.ToolCalls is not null)
            {
                foreach (var streamToolCall in choice.ToolCalls)
                {
                    var index = streamToolCall.Index;

                    // Initialize tool call on first appearance
                    if (!toolCalls.ContainsKey(index))
                    {
                        var id = streamToolCall.Id ?? $"call_{index}";
                        currentToolCallId[index] = id;
                        currentToolCallName[index] = new StringBuilder();
                        currentToolCallArgs[index] = new StringBuilder();
                        toolCalls[index] = new ToolCall
                        {
                            Id = id,
                            Type = streamToolCall.Type ?? "function",
                            Function = new ToolCallFunction()
                        };
                    }

                    // Accumulate function name
                    if (streamToolCall.Function?.Name is not null)
                    {
                        currentToolCallName[index].Append(streamToolCall.Function.Name);
                    }

                    // Accumulate arguments
                    if (streamToolCall.Function?.Arguments is not null)
                    {
                        currentToolCallArgs[index].Append(streamToolCall.Function.Arguments);
                    }
                }
            }
        }

        // Store thinking content and duration
        _lastThinking = thinkingContent.ToString();
        if (isThinking)
        {
            _thinkingStopwatch.Stop();
            _thinkingDuration = _thinkingStopwatch.Elapsed;
        }

        // Finalize tool calls
        var finalToolCalls = new Collection<ToolCall>();
        foreach (var kvp in toolCalls.OrderBy(x => x.Key))
        {
            var toolCall = kvp.Value;
            var finalizedToolCall = new ToolCall
            {
                Id = toolCall.Id,
                Type = toolCall.Type,
                Function = new ToolCallFunction
                {
                    Name = currentToolCallName.TryGetValue(kvp.Key, out var nameBuilder) ? nameBuilder.ToString() : "",
                    Arguments = currentToolCallArgs.TryGetValue(kvp.Key, out var argsBuilder) ? argsBuilder.ToString() : "{}"
                }
            };
            finalToolCalls.Add(finalizedToolCall);
        }

        // Create final response
        var aiResponse = new AIResponse
        {
            Content = responseContent.ToString(),
            ToolCalls = finalToolCalls,
            InputTokens = _lastInputTokens,
            OutputTokens = _lastOutputTokens,
            Thinking = thinkingContent.Length > 0 ? thinkingContent.ToString() : null,
            ReasoningTokens = _lastReasoningTokens,
            CachedTokens = _lastCachedTokens
        };

        // Add assistant response to history (only if no tool calls)
        if (aiResponse.ToolCalls.Count == 0)
        {
            _conversationHistory.Add(ChatMessage.Assistant(aiResponse.Content, aiResponse.OutputTokens));
        }

        yield return StreamingToolResponse.FromResponse(aiResponse);
    }
}

/// <summary>
/// Streaming chunk from Alibaba Cloud API.
/// </summary>
public sealed class StreamChunk
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<StreamChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public ChatResponseUsage? Usage { get; set; }
}

public sealed class StreamChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("delta")]
    public StreamDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
    [JsonPropertyName("tool_calls")]
    public List<StreamToolCall>? ToolCalls { get; set; }
}

public sealed class StreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}

public sealed class StreamToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public StreamFunctionCall? Function { get; set; }
}

public sealed class StreamFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

/// <summary>
/// Request body for chat completion API.
/// </summary>
public class ChatCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<ChatMessageItem> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("stream_options")]
    public StreamOptions? StreamOptions { get; set; }

    [JsonPropertyName("enable_thinking")]
    public bool? EnableThinking { get; set; }

    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; } // "auto", "none", or "required"
}

/// <summary>
/// Streaming options for chat completion requests.
/// </summary>
public class StreamOptions
{
    [JsonPropertyName("include_usage")]
    public bool IncludeUsage { get; set; }
}

/// <summary>
/// A single message in the chat completion request.
/// Supports both string content and multi-part content (for images).
/// </summary>
public class ChatMessageItem
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public object Content { get; set; } = string.Empty;

    /// <summary>
    /// Creates a message with text content.
    /// </summary>
    public static ChatMessageItem Text(string role, string content) => new()
    {
        Role = role,
        Content = content
    };

    /// <summary>
    /// Creates a message with multi-part content (text and images).
    /// </summary>
    public static ChatMessageItem MultiPart(string role, List<ContentPart> parts) => new()
    {
        Role = role,
        Content = parts
    };
}

/// <summary>
/// A part of multi-part message content.
/// </summary>
public class ContentPart
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "text"; // "text" or "image_url"

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("image_url")]
    public ImageUrl? ImageUrl { get; set; }

    /// <summary>
    /// Creates a text content part.
    /// </summary>
    public static ContentPart FromText(string text) => new()
    {
        Type = "text",
        Text = text
    };

    /// <summary>
    /// Creates an image content part from a URL.
    /// </summary>
    public static ContentPart FromImageUrl(string url) => new()
    {
        Type = "image_url",
        ImageUrl = new ImageUrl { Url = url }
    };

    /// <summary>
    /// Creates an image content part from base64 data.
    /// </summary>
    public static ContentPart FromBase64Image(string base64Data, string mimeType = "image/jpeg") => new()
    {
        Type = "image_url",
        ImageUrl = new ImageUrl { Url = $"data:{mimeType};base64,{base64Data}" }
    };
}

/// <summary>
/// Image URL container for image content parts.
/// </summary>
public class ImageUrl
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class ChatCompletionResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created")]
    public long Created { get; set; }

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("choices")]
    public List<ChatResponseChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public ChatResponseUsage? Usage { get; set; }
}

public class ChatResponseChoice
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("message")]
    public ChatResponseMessage? Message { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class ChatResponseMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<ResponseToolCall>? ToolCalls { get; set; }
}

public class ResponseToolCall
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("function")]
    public ResponseFunctionCall? Function { get; set; }
}

public class ResponseFunctionCall
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

public class ChatResponseUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }

    [JsonPropertyName("prompt_tokens_details")]
    public PromptTokensDetails? PromptTokensDetails { get; set; }

    [JsonPropertyName("completion_tokens_details")]
    public CompletionTokensDetails? CompletionTokensDetails { get; set; }
}

/// <summary>
/// Detailed breakdown of prompt tokens.
/// </summary>
public class PromptTokensDetails
{
    [JsonPropertyName("cached_tokens")]
    public int CachedTokens { get; set; }
}

/// <summary>
/// Detailed breakdown of completion tokens.
/// </summary>
public class CompletionTokensDetails
{
    [JsonPropertyName("reasoning_tokens")]
    public int ReasoningTokens { get; set; }
}

// Extension method for synchronous enumeration
file static class AsyncEnumerableExtensions
{
    public static IEnumerable<T> ToEnumerable<T>(this IAsyncEnumerable<T> asyncEnumerable)
    {
        var enumerator = asyncEnumerable.GetAsyncEnumerator();
        try
        {
            while (enumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                yield return enumerator.Current;
            }
        }
        finally
        {
            enumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}