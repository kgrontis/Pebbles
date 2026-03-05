namespace Pebbles.UI;

using Pebbles.Models;
using Pebbles.Services;

/// <summary>
/// Handles all chat UI rendering.
/// </summary>
public interface IChatRenderer
{
    /// <summary>
    /// Renders the welcome screen.
    /// </summary>
    void RenderWelcome(ChatSession session);

    /// <summary>
    /// Renders a user message.
    /// </summary>
    void RenderUserMessage(string message);

    /// <summary>
    /// Renders the thinking process with streaming.
    /// </summary>
    Task RenderThinkingAsync(IAIProvider provider, MockResponse response);

    /// <summary>
    /// Renders an assistant response with streaming (mock mode).
    /// </summary>
    Task RenderAssistantStreamAsync(IAIProvider provider, MockResponse response);

    /// <summary>
    /// Renders an assistant response with live API streaming.
    /// Returns (content, thinking, thinkingDuration, outputTokens).
    /// </summary>
    Task<(string Content, string Thinking, TimeSpan ThinkingDuration, int OutputTokens)> 
        RenderAssistantLiveStreamAsync(IAIProvider provider, string userInput, bool compactMode);

    /// <summary>
    /// Renders the result of a slash command.
    /// </summary>
    void RenderCommandResult(CommandResult result, ChatSession session);

    /// <summary>
    /// Renders token usage information and updates session totals.
    /// </summary>
    void RenderTokenInfo(int inputTokens, int outputTokens, ChatSession session);

    /// <summary>
    /// Renders the status bar above the input area.
    /// </summary>
    void RenderStatusBar(ChatSession session);
}