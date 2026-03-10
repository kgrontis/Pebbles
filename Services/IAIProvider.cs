namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Provides AI responses with streaming support.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Gets a response for the given user input (non-streaming).
    /// </summary>
    MockResponse GetResponse(string userInput);

    /// <summary>
    /// Gets a streaming response for the given user input.
    /// </summary>
    IAsyncEnumerable<string> StreamResponseAsync(string userInput, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams thinking content word by word.
    /// </summary>
    IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response);

    /// <summary>
    /// Streams response content character by character.
    /// </summary>
    IAsyncEnumerable<string> StreamContentAsync(MockResponse response);

    /// <summary>
    /// Adds a message to the conversation history.
    /// </summary>
    void AddToHistory(ChatMessage message);

    /// <summary>
    /// Clears the conversation history.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Gets a response with tool calling support.
    /// </summary>
    /// <param name="userInput">User message</param>
    /// <param name="tools">List of tools available for this request</param>
    /// <param name="toolResults">Results from previous tool calls (if continuing a conversation)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response that may include tool calls</returns>
    Task<AIResponse> GetResponseWithToolsAsync(
        string userInput,
        List<ToolDefinition> tools,
        List<ToolResult>? toolResults = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last thinking content from a response.
    /// </summary>
    string GetLastThinking();

    /// <summary>
    /// Gets the last thinking duration.
    /// </summary>
    TimeSpan GetLastThinkingDuration();
}

/// <summary>
/// Response from AI that may include tool calls.
/// </summary>
public record AIResponse
{
    public string Content { get; init; } = string.Empty;
    public List<ToolCall> ToolCalls { get; init; } = new();
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }

    // Optional: Include thinking if the model supports it
    public string? Thinking { get; init; }
}