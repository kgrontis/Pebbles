namespace Pebbles.Services;

using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pebbles.Configuration;
using Pebbles.Models;

/// <summary>
/// Alibaba Cloud DashScope AI provider with streaming support (OpenAI-compatible API).
/// </summary>
public class DashScopeProvider : IAIProvider
{
    private readonly HttpClient _httpClient;
    private readonly PebblesOptions _options;
    private readonly ContextManager _contextManager;
    private readonly IFileService _fileService;
    private readonly ISystemPromptService _systemPromptService;
    private readonly List<ChatMessage> _conversationHistory = [];
    private string _lastThinking = string.Empty;
    private TimeSpan _thinkingDuration = TimeSpan.Zero;
    private int _lastInputTokens = 0;
    private int _lastOutputTokens = 0;

    public DashScopeProvider(
        PebblesOptions options,
        ContextManager contextManager,
        IFileService fileService,
        ISystemPromptService systemPromptService)
    {
        _options = options;
        _contextManager = contextManager;
        _fileService = fileService;
        _systemPromptService = systemPromptService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(120)
        };

        // Get API key from options or environment variable
        var apiKey = _options.DashScopeApiKey
            ?? Environment.GetEnvironmentVariable("BAILIAN_CODING_PLAN_API_KEY")
            ?? throw new InvalidOperationException(
                "DashScope API key not configured. Set BAILIAN_CODING_PLAN_API_KEY environment variable " +
                "or add DashScopeApiKey to appsettings.json");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        // Add User-Agent to identify as a coding agent
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Pebbles/1.0 (Coding Agent)");
    }

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
            Model = _options.DefaultModel,
            Messages = messages,
            Stream = true
        };

        var url = $"{_options.DashScopeBaseUrl}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request, DashScopeJsonContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json");

        var responseContent = new StringBuilder();

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (string.IsNullOrWhiteSpace(line))
                continue;
                
            if (!line.StartsWith("data: "))
                continue;

            var data = line[6..].Trim();
            if (data == "[DONE]")
                break;
            
            if (string.IsNullOrEmpty(data))
                continue;

            StreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(data, DashScopeJsonContext.Default.StreamChunk);
            }
            catch (JsonException)
            {
                continue;
            }

            if (chunk?.Choices is null || chunk.Choices.Count == 0)
                continue;

            var delta = chunk.Choices[0].Delta;
            
            // Handle reasoning/thinking content separately from regular content
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
            {
                responseContent.Append(delta.ReasoningContent);
                // Prefix thinking content with marker for renderer to style
                yield return $"[THINKING]{delta.ReasoningContent}";
                continue;
            }
            
            if (!string.IsNullOrEmpty(delta?.Content))
            {
                responseContent.Append(delta.Content);
                yield return delta.Content;
            }
        }

        // Store final stats
        _lastOutputTokens = EstimateTokens(responseContent.ToString());

        // Add assistant response to history
        _conversationHistory.Add(ChatMessage.Assistant(responseContent.ToString(), _lastOutputTokens));
    }

    private List<ChatMessageItem> BuildMessages()
    {
        // Build enhanced system prompt with context
        var context = _contextManager.GetContextForPrompt();
        var files = _fileService.FormatFilesForPrompt();

        var systemPrompt = _systemPromptService.GetAgentPrompt();
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
                Role = msg.Role.ToString().ToLowerInvariant(),
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
                await Task.Delay(15);
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
                await Task.Delay(5);
            }
        }
        if (buffer.Length > 0)
            yield return buffer;
    }

    public async Task<AIResponse> GetResponseWithToolsAsync(
    string userInput,
    List<ToolDefinition> tools,
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
            Model = _options.DefaultModel,
            Messages = messages,
            Stream = false,
            Tools = tools,
            ToolChoice = "auto"
        };

        var url = $"{_options.DashScopeBaseUrl}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request, DashScopeJsonContext.Default.ChatCompletionRequest),
            Encoding.UTF8,
            "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseMessage = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
        var chatResponse = JsonSerializer.Deserialize<ChatCompletionResponse>(responseString);

        var aiResponse = new AIResponse
        {
            Content = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content ?? "",
            InputTokens = chatResponse?.Usage?.PromptTokens ?? 0,
            OutputTokens = chatResponse?.Usage?.CompletionTokens ?? 0
        };

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
}

/// <summary>
/// Streaming chunk from DashScope API.
/// </summary>
public class StreamChunk
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
}

public class StreamChoice
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

public class StreamDelta
{
    [JsonPropertyName("role")]
    public string? Role { get; set; }
    
    [JsonPropertyName("content")]
    public string? Content { get; set; }
    
    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}

public class StreamToolCall
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

public class StreamFunctionCall
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

    [JsonPropertyName("tools")]
    public List<ToolDefinition>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; } // "auto", "none", or "required"
}

/// <summary>
/// A single message in the chat completion request.
/// </summary>
public class ChatMessageItem
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
    
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
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