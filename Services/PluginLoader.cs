namespace Pebbles.Services;

using Pebbles.Models;
using Pebbles.Plugins;

/// <summary>
/// Discovers and loads C# plugins from global and project directories.
/// </summary>
public sealed class PluginLoader : IPluginLoader
{
    private readonly RoslynPluginService _roslynService;
    private readonly string _globalPluginsPath;
    private readonly string _projectPluginsPath;

    private List<CSharpPlugin> _plugins = [];

    public PluginLoader(RoslynPluginService roslynService)
    {
        _roslynService = roslynService;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalPluginsPath = Path.Combine(home, ".pebbles", "agent", "plugins", "scripts");
        _projectPluginsPath = Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "agent", "plugins", "scripts");
    }

    /// <inheritdoc />
    public IReadOnlyList<CSharpPlugin> Plugins => _plugins.AsReadOnly();

    /// <inheritdoc />
    public PluginLoadResult LoadPlugins()
    {
        var result = new PluginLoadResult();
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
            var (plugin, error) = _roslynService.LoadPlugin(scriptPath);
            
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
    public IEnumerable<SlashCommand> GetPluginCommands()
    {
        foreach (var plugin in _plugins)
        {
            if (plugin.Instance is null) continue;

            foreach (var cmd in plugin.Instance.GetCommands())
            {
                yield return new SlashCommand
                {
                    Name = cmd.Name,
                    Description = cmd.Description,
                    Usage = string.IsNullOrEmpty(cmd.Usage) ? cmd.Name : cmd.Usage,
                    Handler = CreateCommandHandler(cmd)
                };
            }
        }
    }

    /// <summary>
    /// Create a command handler that invokes the C# plugin method.
    /// </summary>
    private Func<string[], ChatSession, Task<CommandResult>> CreateCommandHandler(PluginCommand cmd)
    {
        return (args, session) =>
        {
            try
            {
                var pluginSession = new PluginSession
                {
                    Model = session.Model,
                    TotalInputTokens = session.TotalInputTokens,
                    TotalOutputTokens = session.TotalOutputTokens,
                    TotalCost = (decimal)session.TotalCost
                };

                var result = cmd.Handler(args, pluginSession);
                
                // Return raw output so it appears on its own line without the ● prefix
                // Allow markup so plugins can use Spectre formatting
                return Task.FromResult(CommandResult.Raw(result, allowMarkup: true));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Fail($"Error: {ex.Message}"));
            }
        };
    }
}