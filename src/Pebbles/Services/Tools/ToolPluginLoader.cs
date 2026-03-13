namespace Pebbles.Services.Tools;

using Pebbles.Models;

/// <summary>
/// Discovers and loads tool plugins from global and project directories.
/// </summary>
public sealed class ToolPluginLoader : IToolPluginLoader
{
    private readonly RoslynPluginService _roslynService;
    private readonly string _globalPluginsPath;
    private readonly string _projectPluginsPath;

    private List<LoadedToolPlugin> _plugins = [];

    public ToolPluginLoader(RoslynPluginService roslynService)
    {
        _roslynService = roslynService;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalPluginsPath = Path.Combine(home, ".pebbles", "agent", "plugins", "scripts");
        _projectPluginsPath = Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "agent", "plugins", "scripts");
    }

    /// <inheritdoc />
    public IReadOnlyList<LoadedToolPlugin> Plugins => _plugins.AsReadOnly();

    /// <inheritdoc />
    public ToolPluginLoadResult LoadPlugins()
    {
        var result = new ToolPluginLoadResult();
        _plugins = [];

        var scriptPaths = new List<string>();

        // Discover scripts from global path
        if (Directory.Exists(_globalPluginsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_globalPluginsPath, "*.cs"));
        }

        // Discover scripts from project path
        if (Directory.Exists(_projectPluginsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_projectPluginsPath, "*.cs"));
        }

        // Load each script
        foreach (var scriptPath in scriptPaths.Distinct())
        {
            var (plugin, error) = RoslynPluginService.LoadToolPlugin(scriptPath);

            if (plugin is not null)
            {
                result.Plugins.Add(plugin);
                _plugins.Add(plugin);
            }
            else if (error is not null)
            {
                result.Errors.Add((scriptPath, error));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<ITool> GetToolInstances()
    {
        foreach (var plugin in _plugins)
        {
            if (plugin.Instance is null) continue;

            yield return new ToolPluginAdapter(plugin);
        }
    }
}

/// <summary>
/// Adapter that wraps a tool plugin to implement ITool.
/// </summary>
public sealed class ToolPluginAdapter(LoadedToolPlugin plugin) : ITool
{
    public string Name => plugin.Instance?.Name ?? plugin.Name;

    public string Description => plugin.Instance?.Description ?? string.Empty;

    public ToolDefinition GetDefinition() => plugin.Instance?.GetDefinition() ?? new ToolDefinition();

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        if (plugin.Instance is null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Plugin instance is null"
            };
        }

        try
        {
            return await plugin.Instance.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Tool execution failed: {ex.Message}"
            };
        }
    }
}