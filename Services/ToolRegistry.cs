using Pebbles.Models;
using Pebbles.Services.Tools;

namespace Pebbles.Services;

/// <summary>
/// Registry for managing and executing tools.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);
    private readonly IToolPluginLoader? _pluginLoader;

    public ToolRegistry(IToolPluginLoader? pluginLoader)
    {
        _pluginLoader = pluginLoader;
    }

    /// <summary>
    /// Registers a tool to be available for AI calls.
    /// </summary>
    /// <param name="tool">The tool to register</param>
    public void RegisterTool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        if (_tools.ContainsKey(tool.Name))
            throw new InvalidOperationException($"A tool with the name '{tool.Name}' is already registered.");
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// Retrieves a registered tool by name.
    /// </summary>
    /// <param name="name">The name of the tool</param>
    /// <returns>The registered tool, or null if not found</returns>
    public ITool? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tool name cannot be null or whitespace.", nameof(name));
        return _tools.GetValueOrDefault(name);
    }

    /// <summary>
    /// Gets all registered tool definitions (for sending to AI).
    /// </summary>
    public IReadOnlyList<ToolDefinition> GetAllToolDefinitions() => [.. _tools.Values.Select(t => t.GetDefinition())];

    /// <summary>
    /// Executes a tool by name.
    /// </summary>
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string name,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        var tool = GetTool(name);
        if (tool is null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Unknown tool: {name}"
            };
        }

        try
        {
            return await tool.ExecuteAsync(arguments, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    // Add this method:
    /// <summary>
    /// Loads tool plugins and registers them.
    /// </summary>
    public void LoadToolPlugins()
    {
        if (_pluginLoader is null)
            return;

        var result = _pluginLoader.LoadPlugins();

        foreach (var plugin in result.Plugins)
        {
            if (plugin.Instance is not null)
            {
                RegisterTool(new ToolPluginAdapter(plugin));
            }
        }
    }
}
