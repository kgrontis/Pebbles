using Pebbles.Models;

namespace Pebbles.Services;

/// <summary>
/// Interface for managing context compression and memory extraction.
/// </summary>
public interface IContextManagementService
{
    /// <summary>
    /// Checks if auto-compression should be triggered and performs it if needed.
    /// </summary>
    Task CheckAutoCompressionAsync(ChatSession session);

    /// <summary>
    /// Checks if automatic memory extraction should be triggered.
    /// </summary>
    Task CheckMemoryExtractionAsync(ChatSession session);
}