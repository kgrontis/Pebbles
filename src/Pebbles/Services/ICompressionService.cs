namespace Pebbles.Services;

using Pebbles.Models;
using System.Collections.ObjectModel;

/// <summary>
/// Provides context compaction services for managing conversation history.
/// </summary>
public interface ICompressionService
{
    /// <summary>
    /// Determines whether compaction should be triggered based on current token usage.
    /// </summary>
    /// <param name="currentTokens">Current token count in the conversation.</param>
    /// <param name="contextWindow">Maximum context window size for the model.</param>
    /// <param name="threshold">Compaction threshold (0.0-1.0).</param>
    /// <returns>True if compaction should be triggered.</returns>
    bool ShouldCompact(int currentTokens, int contextWindow, double threshold = 0.7);

    /// <summary>
    /// Performs context compaction on the given messages.
    /// </summary>
    /// <param name="messages">The conversation messages to compact.</param>
    /// <param name="keepRecentCount">Number of recent messages to keep verbatim.</param>
    /// <param name="previousSummary">Optional previous summary for iterative compaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the compaction operation.</returns>
    Task<CompressionResult> CompactAsync(
        Collection<ChatMessage> messages,
        int keepRecentCount = 6,
        string? previousSummary = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimates the total token count for a list of messages.
    /// </summary>
    /// <param name="messages">The messages to count tokens for.</param>
    /// <returns>Estimated token count.</returns>
    int EstimateTotalTokens(IEnumerable<ChatMessage> messages);
}