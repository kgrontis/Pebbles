
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pebbles.Configuration;
using Pebbles.Models;

namespace Pebbles.Services;
/// <summary>
/// OpenAI API provider with streaming support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the OpenAIProvider.
/// </remarks>
/// <param name="httpClient">The HttpClient configured with appropriate headers and timeout.</param>
/// <param name="options">The Pebbles configuration options.</param>
internal sealed class OpenAIProvider(HttpClient httpClient, PebblesOptions options) : IAIProvider
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
        // OpenAI doesn't have thinking blocks - yield empty
        await Task.CompletedTask.ConfigureAwait(false);
        yield break;
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
        var messages = BuildMessages();

        var request = new OpenAiChatRequest
        {
            Model = options.DefaultModel,
            Messages = messages,
            Stream = true
        };

        var url = $"{GetBaseUrl()}/chat/completions";
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
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ", StringComparison.InvariantCultureIgnoreCase)) continue;

            var data = line[6..].Trim();
            if (data == "[DONE]") break;
            if (string.IsNullOrEmpty(data)) continue;

            var chunk = JsonSerializer.Deserialize<OpenAiChatChunk>(data);
            if (chunk?.Choices is null || chunk.Choices.Count == 0) continue;

            var delta = chunk.Choices[0].Delta;

            // Handle reasoning/thinking content (for models like o1, o3, etc.)
            if (!string.IsNullOrEmpty(delta?.ReasoningContent))
            {
                if (!isThinking)
                {
                    isThinking = true;
                    _thinkingStopwatch.Restart();
                }
                thinkingContent.Append(delta.ReasoningContent);
                yield return $"[THINKING]{delta.ReasoningContent}";
                continue;
            }

            // Regular content
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

        // Store thinking content if we received any
        _lastThinking = thinkingContent.ToString();
        if (isThinking)
        {
            _thinkingStopwatch.Stop();
            _thinkingDuration = _thinkingStopwatch.Elapsed;
        }

        _lastOutputTokens = EstimateTokens(responseContent.ToString());
        _conversationHistory.Add(ChatMessage.Assistant(responseContent.ToString(), _lastOutputTokens));
    }

    public async Task<AIResponse> GetResponseWithToolsAsync(
        string userInput,
        IReadOnlyList<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        CancellationToken cancellationToken = default)
    {
        _conversationHistory.Add(ChatMessage.User(userInput, 0));

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

        var request = new OpenAiChatRequest
        {
            Model = options.DefaultModel,
            Messages = messages,
            Tools = [.. tools.Select(t => new OpenAiTool { Function = t.Function })],
            ToolChoice = "auto"
        };

        var url = $"{GetBaseUrl()}/chat/completions";
        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
        var responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(responseString);

        var message = chatResponse?.Choices?.FirstOrDefault()?.Message;
        var thinkingContent = message?.ReasoningContent;

        var aiResponse = new AIResponse
        {
            Content = message?.Content ?? "",
            InputTokens = chatResponse?.Usage?.PromptTokens ?? 0,
            OutputTokens = chatResponse?.Usage?.CompletionTokens ?? 0,
            Thinking = !string.IsNullOrEmpty(thinkingContent) ? thinkingContent : null
        };

        // Store thinking for GetLastThinking()
        _lastThinking = thinkingContent ?? string.Empty;

        _lastInputTokens = aiResponse.InputTokens;
        _lastOutputTokens = aiResponse.OutputTokens;

        return aiResponse;
    }

    private List<OpenAiMessage> BuildMessages()
    {
#pragma warning disable CA1308 // API requires lowercase role names
        return [.. _conversationHistory.Select(m => new OpenAiMessage
        {
            Role = m.Role.ToString().ToLowerInvariant(),
            Content = m.Content
        })];
#pragma warning restore CA1308
    }

    private Uri GetBaseUrl()
    {
        return options.OpenAiBaseUrl ?? new Uri("https://api.openai.com/v1");
    }

    private static int EstimateTokens(string text)
    {
        return (int)Math.Ceiling(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length * 1.3);
    }
}

#region OpenAI DTOs

internal class OpenAiChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OpenAiMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("tools")]
    public List<OpenAiTool>? Tools { get; set; }

    [JsonPropertyName("tool_choice")]
    public string? ToolChoice { get; set; }
}

internal class OpenAiMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("reasoning_content")]
    public string? ReasoningContent { get; set; }
}

internal class OpenAiTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition? Function { get; set; }
}

internal class OpenAiChatResponse
{
    [JsonPropertyName("choices")]
    public List<OpenAiChoice>? Choices { get; set; }

    [JsonPropertyName("usage")]
    public OpenAiUsage? Usage { get; set; }
}

internal class OpenAiChatChunk
{
    [JsonPropertyName("choices")]
    public List<OpenAiChoice>? Choices { get; set; }
}

internal class OpenAiChoice
{
    [JsonPropertyName("message")]
    public OpenAiMessage? Message { get; set; }

    [JsonPropertyName("delta")]
    public OpenAiMessage? Delta { get; set; }
}

internal class OpenAiUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; set; }
}

#endregion
