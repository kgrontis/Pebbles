namespace Pebbles.Services;

using System.Diagnostics;
using MoonSharp.Interpreter;
using Pebbles.Models;

/// <summary>
/// Discovers and loads Lua plugins from global and project directories.
/// </summary>
public sealed class PluginLoader : IPluginLoader
{
    private readonly LuaPluginService _luaService;
    private readonly string _globalPluginsPath;
    private readonly string _projectPluginsPath;

    private List<LuaPlugin> _plugins = [];
    private Script? _script;

    public PluginLoader(LuaPluginService luaService)
    {
        _luaService = luaService;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalPluginsPath = Path.Combine(home, ".pebbles", "agent", "plugins", "scripts");
        _projectPluginsPath = Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "agent", "plugins", "scripts");
    }

    /// <inheritdoc />
    public IReadOnlyList<LuaPlugin> Plugins => _plugins.AsReadOnly();

    /// <inheritdoc />
    public PluginLoadResult LoadPlugins()
    {
        var result = new PluginLoadResult();
        _plugins = [];
        _script = _luaService.CreateScript();

        var scriptPaths = new List<string>();

        // Discover scripts from global path
        if (Directory.Exists(_globalPluginsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_globalPluginsPath, "*.lua"));
        }

        // Discover scripts from project path
        if (Directory.Exists(_projectPluginsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_projectPluginsPath, "*.lua"));
        }

        // Load each script
        foreach (var scriptPath in scriptPaths.Distinct())
        {
            try
            {
                var plugin = LoadPlugin(scriptPath);
                if (plugin is not null)
                {
                    result.Plugins.Add(plugin);
                    _plugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                result.Errors.Add((scriptPath, ex.Message));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IEnumerable<SlashCommand> GetPluginCommands()
    {
        foreach (var plugin in _plugins)
        {
            foreach (var cmd in plugin.Commands)
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
    /// Load a single Lua plugin file.
    /// </summary>
    private LuaPlugin? LoadPlugin(string scriptPath)
    {
        if (_script is null)
            return null;

        var code = File.ReadAllText(scriptPath);
        _script.DoString(code);

        var plugin = new LuaPlugin
        {
            SourcePath = scriptPath
        };

        // Extract plugin metadata
        var pluginTable = _script.Globals.Get("plugin");
        if (pluginTable.Type == DataType.Table)
        {
            plugin.Name = pluginTable.Table?.Get("name")?.String ?? Path.GetFileNameWithoutExtension(scriptPath);
            plugin.Version = pluginTable.Table?.Get("version")?.String ?? "1.0.0";
            plugin.Description = pluginTable.Table?.Get("description")?.String ?? string.Empty;
        }
        else
        {
            plugin.Name = Path.GetFileNameWithoutExtension(scriptPath);
        }

        // Extract commands
        var commandsTable = _script.Globals.Get("commands");
        if (commandsTable.Type == DataType.Table)
        {
            foreach (var entry in commandsTable.Table.Pairs)
            {
                if (entry.Value.Type == DataType.Table)
                {
                    var cmdTable = entry.Value.Table;
                    var name = cmdTable.Get("name")?.String;
                    var handler = cmdTable.Get("handler");

                    if (!string.IsNullOrEmpty(name) && handler.Type == DataType.Function)
                    {
                        plugin.Commands.Add(new PluginCommand
                        {
                            Name = name,
                            Description = cmdTable.Get("description")?.String ?? string.Empty,
                            Usage = cmdTable.Get("usage")?.String ?? name,
                            Handler = handler
                        });
                    }
                }
            }
        }

        // Extract hooks
        var hooksTable = _script.Globals.Get("hooks");
        if (hooksTable.Type == DataType.Table)
        {
            var hookTypes = new[] { "on_start", "on_before_send", "on_after_receive", "on_command" };
            foreach (var hookType in hookTypes)
            {
                var hook = hooksTable.Table.Get(hookType);
                if (hook.Type == DataType.Function)
                {
                    plugin.Hooks.Add(new PluginHook
                    {
                        Type = hookType,
                        Handler = hook
                    });
                }
            }
        }

        return plugin;
    }

    /// <summary>
    /// Create a command handler that invokes the Lua function.
    /// </summary>
    private Func<string[], ChatSession, Task<CommandResult>> CreateCommandHandler(PluginCommand cmd)
    {
        return (args, session) =>
        {
            try
            {
                if (_script is null || cmd.Handler is not DynValue handler)
                    return Task.FromResult(CommandResult.Fail("Plugin not loaded properly."));

                // Create args table
                var argsTable = DynValue.NewTable(_script);
                for (int i = 0; i < args.Length; i++)
                {
                    argsTable.Table.Set(i + 1, DynValue.NewString(args[i]));
                }

                // Create session table (read-only snapshot)
                var sessionTable = DynValue.NewTable(_script);
                sessionTable.Table.Set("model", DynValue.NewString(session.Model));
                sessionTable.Table.Set("total_input_tokens", DynValue.NewNumber(session.TotalInputTokens));
                sessionTable.Table.Set("total_output_tokens", DynValue.NewNumber(session.TotalOutputTokens));
                sessionTable.Table.Set("total_cost", DynValue.NewNumber((double)session.TotalCost));

                // Call the Lua handler
                var result = _script.Call(handler, argsTable, sessionTable);

                var output = result.Type switch
                {
                    DataType.String => result.String,
                    DataType.Number => result.Number.ToString(),
                    DataType.Boolean => result.Boolean ? "true" : "false",
                    DataType.Nil => string.Empty,
                    _ => result.ToString()
                };

                // Return raw output so it appears on its own line without the ● prefix
                // Allow markup so plugins can use Spectre formatting
                return Task.FromResult(CommandResult.Raw(output, allowMarkup: true));
            }
            catch (InterpreterException ex)
            {
                return Task.FromResult(CommandResult.Fail($"Lua error: {ex.Message}"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Fail($"Error: {ex.Message}"));
            }
        };
    }
}