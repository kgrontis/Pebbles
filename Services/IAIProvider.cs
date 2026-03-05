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
}