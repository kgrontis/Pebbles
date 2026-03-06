namespace Pebbles.Services;

using System.Diagnostics;
using MoonSharp.Interpreter;
using Pebbles.Models;

/// <summary>
/// Discovers and loads Lua extensions from global and project directories.
/// </summary>
public sealed class ExtensionLoader : IExtensionLoader
{
    private readonly LuaExtensionService _luaService;
    private readonly string _globalExtensionsPath;
    private readonly string _projectExtensionsPath;

    private List<LuaExtension> _extensions = [];
    private Script? _script;

    public ExtensionLoader(LuaExtensionService luaService)
    {
        _luaService = luaService;

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _globalExtensionsPath = Path.Combine(home, ".pebbles", "agent", "extensions", "scripts");
        _projectExtensionsPath = Path.Combine(Directory.GetCurrentDirectory(), ".pebbles", "agent", "extensions", "scripts");
    }

    /// <inheritdoc />
    public IReadOnlyList<LuaExtension> Extensions => _extensions.AsReadOnly();

    /// <inheritdoc />
    public ExtensionLoadResult LoadExtensions()
    {
        var result = new ExtensionLoadResult();
        _extensions = [];
        _script = _luaService.CreateScript();

        var scriptPaths = new List<string>();

        // Discover scripts from global path
        if (Directory.Exists(_globalExtensionsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_globalExtensionsPath, "*.lua"));
        }

        // Discover scripts from project path
        if (Directory.Exists(_projectExtensionsPath))
        {
            scriptPaths.AddRange(Directory.GetFiles(_projectExtensionsPath, "*.lua"));
        }

        // Load each script
        foreach (var scriptPath in scriptPaths.Distinct())
        {
            try
            {
                var extension = LoadExtension(scriptPath);
                if (extension is not null)
                {
                    result.Extensions.Add(extension);
                    _extensions.Add(extension);
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
    public IEnumerable<SlashCommand> GetExtensionCommands()
    {
        foreach (var extension in _extensions)
        {
            foreach (var cmd in extension.Commands)
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
    /// Load a single Lua extension file.
    /// </summary>
    private LuaExtension? LoadExtension(string scriptPath)
    {
        if (_script is null)
            return null;

        var code = File.ReadAllText(scriptPath);
        _script.DoString(code);

        var extension = new LuaExtension
        {
            SourcePath = scriptPath
        };

        // Extract extension metadata
        var extTable = _script.Globals.Get("extension");
        if (extTable.Type == DataType.Table)
        {
            extension.Name = extTable.Table?.Get("name")?.String ?? Path.GetFileNameWithoutExtension(scriptPath);
            extension.Version = extTable.Table?.Get("version")?.String ?? "1.0.0";
            extension.Description = extTable.Table?.Get("description")?.String ?? string.Empty;
        }
        else
        {
            extension.Name = Path.GetFileNameWithoutExtension(scriptPath);
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
                        extension.Commands.Add(new ExtensionCommand
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
                    extension.Hooks.Add(new ExtensionHook
                    {
                        Type = hookType,
                        Handler = hook
                    });
                }
            }
        }

        return extension;
    }

    /// <summary>
    /// Create a command handler that invokes the Lua function.
    /// </summary>
    private Func<string[], ChatSession, Task<CommandResult>> CreateCommandHandler(ExtensionCommand cmd)
    {
        return (args, session) =>
        {
            try
            {
                if (_script is null || cmd.Handler is not DynValue handler)
                    return Task.FromResult(CommandResult.Fail("Extension not loaded properly."));

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
                // Allow markup so extensions can use Spectre formatting
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