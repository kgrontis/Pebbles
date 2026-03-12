using Pebbles.Models;

namespace Pebbles.Services;

/// <summary>
/// Interface for tools that can be executed by the AI assistant.
/// </summary>
internal interface ITool
{
    /// <summary>
    /// The tool's name (used in API calls).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Human-reeadable description of the tool's functionality.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// The tool's parameter schema
    /// </summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// Executes the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">JSON string of arguments from the AI model</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Execution result</returns>
    Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);
}
