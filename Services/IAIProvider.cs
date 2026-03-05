namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Provides AI responses with streaming support.
/// </summary>
public interface IAIProvider
{
    /// <summary>
    /// Gets a response for the given user input.
    /// </summary>
    MockResponse GetResponse(string userInput);

    /// <summary>
    /// Streams thinking content word by word.
    /// </summary>
    IAsyncEnumerable<string> StreamThinkingAsync(MockResponse response);

    /// <summary>
    /// Streams response content character by character.
    /// </summary>
    IAsyncEnumerable<string> StreamContentAsync(MockResponse response);
}