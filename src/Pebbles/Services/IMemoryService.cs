namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Provides memory management services for persisting user preferences.
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Gets the current user memories.
    /// </summary>
    /// <returns>The current memory content.</returns>
    string GetMemories();

    /// <summary>
    /// Saves new memories, merging with existing ones.
    /// </summary>
    /// <param name="newMemories">The new memories to add.</param>
    /// <returns>True if successful.</returns>
    bool SaveMemories(string newMemories);

    /// <summary>
    /// Explicitly saves a single memory item.
    /// </summary>
    /// <param name="memory">The memory to save.</param>
    /// <returns>True if successful.</returns>
    bool Remember(string memory);

    /// <summary>
    /// Clears all memories.
    /// </summary>
    /// <returns>True if successful.</returns>
    bool ClearMemories();

    /// <summary>
    /// Extracts memories from a conversation automatically.
    /// </summary>
    /// <param name="messages">The conversation messages to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The extracted memories, or null if none found.</returns>
    Task<string?> ExtractMemoriesAsync(IEnumerable<ChatMessage> messages, CancellationToken cancellationToken = default);
}