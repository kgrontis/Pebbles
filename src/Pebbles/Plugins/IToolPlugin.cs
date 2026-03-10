namespace Pebbles.Plugins;

using Pebbles.Models;

/// <summary>
/// Interface for tool plugins. Implement this to create custom tools.
/// </summary>
public interface IToolPlugin
{
    /// <summary>
    /// Tool identifier (e.g., "my_custom_tool").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Tool version (e.g., "1.0.0").
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Short description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Returns the tool definition (schema) for the AI model.
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