namespace Pebbles.Services.Commands;

using Pebbles.Configuration;
using Pebbles.Models;

/// <summary>
/// Composite command handler that aggregates all specialized command handlers.
/// </summary>
public sealed class CompositeCommandHandler : ICommandHandler
{
    private readonly Dictionary<string, SlashCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SlashCommand> _pluginCommands = new(StringComparer.OrdinalIgnoreCase);
    private readonly CompressionCommands _compressionCommands;
    private readonly MemoryCommands _memoryCommands;
    private readonly PluginCommands _pluginCommandsHandler;
    private readonly FileCommands _fileCommands;
    private readonly IModelPicker _modelPicker;
    private readonly PebblesOptions _options;
    private readonly ContextManager _contextManager;
    private readonly IPluginLoader _pluginLoader;

    public CompositeCommandHandler(
        CompressionCommands compressionCommands,
        MemoryCommands memoryCommands,
        FileCommands fileCommands,
        IModelPicker modelPicker,
        PebblesOptions options,
        ContextManager contextManager,
        IPluginLoader pluginLoader,
        IToolPluginLoader toolPluginLoader)
    {
        _compressionCommands = compressionCommands;
        _memoryCommands = memoryCommands;
        _fileCommands = fileCommands;
        _modelPicker = modelPicker;
        _options = options;
        _contextManager = contextManager;
        _pluginLoader = pluginLoader;

        // Create PluginCommands with refresh callback to avoid circular dependency
        _pluginCommandsHandler = new PluginCommands(pluginLoader, toolPluginLoader, RefreshPluginCommands);

        RegisterBuiltInCommands();
        RefreshPluginCommands();
    }

    public IEnumerable<SlashCommand> Commands => _commands.Values.Concat(_pluginCommands.Values);
    public IEnumerable<SlashCommand> BuiltInCommands => _commands.Values;
    public IEnumerable<SlashCommand> PluginCommands => _pluginCommands.Values;

    public bool IsCommand(string input) => input.TrimStart().StartsWith('/');

    public Task<CommandResult> ExecuteAsync(string input, ChatSession session)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return Task.FromResult(CommandResult.Fail("Empty command."));

        var cmdName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        if (_commands.TryGetValue(cmdName, out var command) && command.Handler is not null)
            return Task.FromResult(command.Handler(args, session));

        if (_pluginCommands.TryGetValue(cmdName, out var pluginCommand) && pluginCommand.Handler is not null)
            return Task.FromResult(pluginCommand.Handler(args, session));

        return Task.FromResult(CommandResult.Fail($"Unknown command: {cmdName}. Type /help for available commands."));
    }

    public void RefreshPluginCommands()
    {
        _pluginCommands.Clear();

        foreach (var cmd in _pluginLoader.GetPluginCommands())
        {
            _pluginCommands[cmd.Name] = cmd;
        }
    }

    private void RegisterBuiltInCommands()
    {
        _commands["/help"] = new SlashCommand
        {
            Name = "/help",
            Description = "Show available commands",
            Usage = "/help",
            Handler = HandleHelp
        };

        _commands["/clear"] = new SlashCommand
        {
            Name = "/clear",
            Description = "Clear chat history",
            Usage = "/clear",
            Handler = static (_, session) => ChatCommands.HandleClear(session)
        };

        _commands["/model"] = new SlashCommand
        {
            Name = "/model",
            Description = "Switch AI model",
            Usage = "/model <model-name>",
            Handler = HandleModel
        };

        _commands["/compress"] = new SlashCommand
        {
            Name = "/compress",
            Description = "Compress conversation history to save tokens",
            Usage = "/compress",
            Handler = (args, session) =>
            {
                // HandleCompress is async, so we need to wrap it
                var task = _compressionCommands.HandleCompress(session);
                task.Wait();
                return task.Result;
            }
        };

        _commands["/autocompress"] = new SlashCommand
        {
            Name = "/autocompress",
            Description = "Toggle auto-compression on/off",
            Usage = "/autocompress",
            Handler = static (_, session) => CompressionCommands.HandleAutoCompress(session)
        };

        _commands["/remember"] = new SlashCommand
        {
            Name = "/remember",
            Description = "Save something to memory for future sessions",
            Usage = "/remember <text>",
            Handler = (args, _) => _memoryCommands.HandleRemember(args)
        };

        _commands["/memory"] = new SlashCommand
        {
            Name = "/memory",
            Description = "View or manage saved memories",
            Usage = "/memory [clear]",
            Handler = (args, _) => _memoryCommands.HandleMemory(args)
        };

        _commands["/history"] = new SlashCommand
        {
            Name = "/history",
            Description = "Show conversation history summary",
            Usage = "/history",
            Handler = static (_, session) => ChatCommands.HandleHistory(session)
        };

        _commands["/cost"] = new SlashCommand
        {
            Name = "/cost",
            Description = "Show token usage and estimated cost",
            Usage = "/cost",
            Handler = static (_, session) => ChatCommands.HandleCost(session)
        };

        _commands["/context"] = new SlashCommand
        {
            Name = "/context",
            Description = "Show loaded project context",
            Usage = "/context",
            Handler = HandleContext
        };

        _commands["/read"] = new SlashCommand
        {
            Name = "/read",
            Description = "Read a file into context",
            Usage = "/read <path>",
            Handler = (args, _) => _fileCommands.HandleRead(args)
        };

        _commands["/files"] = new SlashCommand
        {
            Name = "/files",
            Description = "List loaded files in context",
            Usage = "/files",
            Handler = (_, _) => _fileCommands.HandleFiles()
        };

        _commands["/clearfiles"] = new SlashCommand
        {
            Name = "/clearfiles",
            Description = "Clear all loaded files from context",
            Usage = "/clearfiles",
            Handler = (_, _) => _fileCommands.HandleClearFiles()
        };

        _commands["/reload"] = new SlashCommand
        {
            Name = "/reload",
            Description = "Reload plugins",
            Usage = "/reload",
            Handler = (_, _) => _pluginCommandsHandler.HandleReload()
        };

        _commands["/plugins"] = new SlashCommand
        {
            Name = "/plugins",
            Description = "List loaded plugins",
            Usage = "/plugins",
            Handler = (_, _) => _pluginCommandsHandler.HandlePlugins()
        };

        _commands["/exit"] = new SlashCommand
        {
            Name = "/exit",
            Description = "Exit Pebbles",
            Usage = "/exit",
            Handler = static (_, _) => ChatCommands.HandleExit()
        };

        _commands["/tools"] = new SlashCommand
        {
            Name = "/tools",
            Description = "List available tools (built-in + plugins)",
            Usage = "/tools",
            Handler = (_, _) => _pluginCommandsHandler.HandleTools()
        };
    }

    private CommandResult HandleHelp(string[] args, ChatSession session)
    {
        var lines = new List<string>
        {
            "",
            "[bold]Built-in Commands[/]",
            ""
        };

        foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
            lines.Add($"  {cmd.Usage,-25} {cmd.Description}");

        if (_pluginCommands.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Plugin Commands[/]");
            lines.Add("");

            foreach (var cmd in _pluginCommands.Values.OrderBy(c => c.Name))
                lines.Add($"  {cmd.Usage,-25} {cmd.Description}");
        }

        lines.Add("");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    private CommandResult HandleModel(string[] args, ChatSession session)
    {
        var models = _options.AvailableModels;

        if (args.Length == 0)
        {
            var selected = _modelPicker.PickModel(models, session.Model);
            if (selected is not null && selected != session.Model)
            {
                session.Model = selected;
                return CommandResult.Ok($"Switched to model: {selected}");
            }
            return CommandResult.Ok("");
        }

        var target = args[0];
        if (models.Contains(target))
        {
            session.Model = target;
            return CommandResult.Ok($"Switched to model: {target}");
        }

        return CommandResult.Fail($"Unknown model: {target}. Use /model to see available models.");
    }

    private CommandResult HandleContext(string[] args, ChatSession session)
    {
        var (global, project) = _contextManager.CheckContextFiles();
        var lines = new List<string> { "" };

        if (project)
        {
            lines.Add($"[bold green]✓[/] Project context: [dim].pebbles/agent/AGENTS.md[/]");
            var context = _contextManager.GetProjectContext();
            if (!string.IsNullOrEmpty(context))
            {
                var preview = context.Length > 500 ? context[..500] + "..." : context;
                lines.Add($"[dim]{Spectre.Console.Markup.Escape(preview)}[/]");
            }
        }
        else
        {
            lines.Add($"[bold red]✗[/] Project context: [dim]Not found (.pebbles/agent/AGENTS.md)[/]");
        }

        lines.Add("");

        if (global)
        {
            lines.Add($"[bold green]✓[/] Global context: [dim]~/.pebbles/agent/AGENTS.md[/]");
        }
        else
        {
            lines.Add($"[bold red]✗[/] Global context: [dim]Not found (~/.pebbles/agent/AGENTS.md)[/]");
        }

        lines.Add("");
        lines.Add("[dim]Context is automatically included in AI prompts.[/]");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }
}