using Pebbles.Models;

namespace Pebbles.Services;

/// <summary>
/// Interface for executing tool calls from AI responses.
/// </summary>
public interface IToolExecutionService
{
    /// <summary>
    /// Executes the tool calling loop for an AI response.
    /// Handles iterative tool calls (max 5 iterations to prevent infinite loops).
    /// </summary>
    /// <param name="input">The initial input or tool results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The final assistant message after all tool executions</returns>
    Task<ChatMessage> ExecuteToolLoopAsync(string input, CancellationToken cancellationToken = default);
}