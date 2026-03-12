using Pebbles.Models;

namespace Pebbles.Services;

/// <summary>
/// Interface for managing and executing tools.
/// </summary>
internal interface IToolRegistry
{
    /// <summary>
    /// Registers a tool to be available for AI calls.
    /// </summary>
    /// <param name="tool">The tool to register</param>
    void RegisterTool(ITool tool);

    /// <summary>
    /// Retrieves a registered tool by name.
    /// </summary>
    /// <param name="name">The name of the tool</param>
    /// <returns>The registered tool, or null if not found</returns>
    ITool? GetTool(string name);

    /// <summary>
    /// Gets all registered tool definitions (for sending to AI).
    /// </summary>
    IReadOnlyList<ToolDefinition> GetAllToolDefinitions();

    /// <summary>
    /// Executes a tool by name.
    /// </summary>
    Task<ToolExecutionResult> ExecuteToolAsync(
        string name,
        string arguments,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads tool plugins and registers them.
    /// </summary>
    void LoadToolPlugins();
}