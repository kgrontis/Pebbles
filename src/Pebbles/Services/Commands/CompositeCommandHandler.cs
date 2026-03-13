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
    private readonly SessionCommands _sessionCommands;
    private readonly SkillCommands _skillCommands;
    private readonly IModelPicker _modelPicker;
    private readonly PebblesOptions _options;
    private readonly ContextManager _contextManager;
    private readonly IPluginLoader _pluginLoader;
    private readonly IUserSettingsService _userSettingsService;

    public CompositeCommandHandler(
        CompressionCommands compressionCommands,
        MemoryCommands memoryCommands,
        FileCommands fileCommands,
        SessionCommands sessionCommands,
        SkillCommands skillCommands,
        IModelPicker modelPicker,
        PebblesOptions options,
        ContextManager contextManager,
        IPluginLoader pluginLoader,
        IToolPluginLoader toolPluginLoader,
        IUserSettingsService userSettingsService)
    {
        _compressionCommands = compressionCommands;
        _memoryCommands = memoryCommands;
        _fileCommands = fileCommands;
        _sessionCommands = sessionCommands;
        _skillCommands = skillCommands;
        _modelPicker = modelPicker;
        _options = options;
        _contextManager = contextManager;
        _pluginLoader = pluginLoader;
        _userSettingsService = userSettingsService;

        // Create PluginCommands with refresh callback to avoid circular dependency
        _pluginCommandsHandler = new PluginCommands(pluginLoader, toolPluginLoader, RefreshPluginCommands);

        RegisterBuiltInCommands();
        RefreshPluginCommands();
    }

    public IEnumerable<SlashCommand> Commands => _commands.Values.Concat(_pluginCommands.Values);
    public IEnumerable<SlashCommand> BuiltInCommands => _commands.Values;
    public IEnumerable<SlashCommand> PluginCommands => _pluginCommands.Values;

    public bool IsCommand(string input) => input.TrimStart().StartsWith('/');

    public async Task<CommandResult> ExecuteAsync(string input, ChatSession session)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return CommandResult.Fail("Empty command.");

        var cmdName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        if (_commands.TryGetValue(cmdName, out var command) && command.Handler is not null)
            return await command.Handler(args, session).ConfigureAwait(false);

        if (_pluginCommands.TryGetValue(cmdName, out var pluginCommand) && pluginCommand.Handler is not null)
            return await pluginCommand.Handler(args, session).ConfigureAwait(false);

        return CommandResult.Fail($"Unknown command: {cmdName}. Type /help for available commands.");
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
            Handler = (args, session) => Task.FromResult(HandleHelp())
        };

        _commands["/clear"] = new SlashCommand
        {
            Name = "/clear",
            Description = "Clear chat history",
            Usage = "/clear",
            Handler = static (_, session) => Task.FromResult(ChatCommands.HandleClear(session))
        };

        _commands["/model"] = new SlashCommand
        {
            Name = "/model",
            Description = "Switch AI model",
            Usage = "/model <model-name>",
            Handler = (args, session) => Task.FromResult(HandleModel(args, session))
        };

        _commands["/compress"] = new SlashCommand
        {
            Name = "/compress",
            Description = "Compress conversation history to save tokens",
            Usage = "/compress",
            Handler = async (args, session) => await _compressionCommands.HandleCompress(session).ConfigureAwait(false)
        };

        _commands["/autocompress"] = new SlashCommand
        {
            Name = "/autocompress",
            Description = "Toggle auto-compression on/off",
            Usage = "/autocompress",
            Handler = static (_, session) => Task.FromResult(CompressionCommands.HandleAutoCompress(session))
        };

        _commands["/remember"] = new SlashCommand
        {
            Name = "/remember",
            Description = "Save something to memory for future sessions",
            Usage = "/remember <text>",
            Handler = (args, _) => Task.FromResult(_memoryCommands.HandleRemember(args))
        };

        _commands["/memory"] = new SlashCommand
        {
            Name = "/memory",
            Description = "View or manage saved memories",
            Usage = "/memory [[clear]]",
            Handler = (args, _) => Task.FromResult(_memoryCommands.HandleMemory(args))
        };

        _commands["/history"] = new SlashCommand
        {
            Name = "/history",
            Description = "Show conversation history summary",
            Usage = "/history",
            Handler = static (_, session) => Task.FromResult(ChatCommands.HandleHistory(session))
        };

        _commands["/cost"] = new SlashCommand
        {
            Name = "/cost",
            Description = "Show token usage and estimated cost",
            Usage = "/cost",
            Handler = static (_, session) => Task.FromResult(ChatCommands.HandleCost(session))
        };

        _commands["/context"] = new SlashCommand
        {
            Name = "/context",
            Description = "Show loaded project context",
            Usage = "/context",
            Handler = (args, session) => Task.FromResult(HandleContext())
        };

        _commands["/read"] = new SlashCommand
        {
            Name = "/read",
            Description = "Read a file into context",
            Usage = "/read <path>",
            Handler = (args, _) => Task.FromResult(_fileCommands.HandleRead(args))
        };

        _commands["/files"] = new SlashCommand
        {
            Name = "/files",
            Description = "List loaded files in context",
            Usage = "/files",
            Handler = (_, _) => Task.FromResult(_fileCommands.HandleFiles())
        };

        _commands["/clearfiles"] = new SlashCommand
        {
            Name = "/clearfiles",
            Description = "Clear all loaded files from context",
            Usage = "/clearfiles",
            Handler = (_, _) => Task.FromResult(_fileCommands.HandleClearFiles())
        };

        _commands["/reload"] = new SlashCommand
        {
            Name = "/reload",
            Description = "Reload plugins",
            Usage = "/reload",
            Handler = (_, _) => Task.FromResult(_pluginCommandsHandler.HandleReload())
        };

        _commands["/plugins"] = new SlashCommand
        {
            Name = "/plugins",
            Description = "List loaded plugins",
            Usage = "/plugins",
            Handler = (_, _) => Task.FromResult(_pluginCommandsHandler.HandlePlugins())
        };

        _commands["/exit"] = new SlashCommand
        {
            Name = "/exit",
            Description = "Exit Pebbles",
            Usage = "/exit",
            Handler = static (_, _) => Task.FromResult(ChatCommands.HandleExit())
        };

        _commands["/tools"] = new SlashCommand
        {
            Name = "/tools",
            Description = "List available tools (built-in + plugins)",
            Usage = "/tools",
            Handler = (_, _) => Task.FromResult(_pluginCommandsHandler.HandleTools())
        };

        _commands["/skill"] = new SlashCommand
        {
            Name = "/skill",
            Description = "List or activate skills",
            Usage = "/skill [[name|off]]",
            Handler = (args, _) => Task.FromResult(_skillCommands.HandleSkill(args))
        };

        _commands["/save"] = new SlashCommand
        {
            Name = "/save",
            Description = "Save current session",
            Usage = "/save",
            Handler = async (args, session) => await _sessionCommands.HandleSave(session).ConfigureAwait(false)
        };

        _commands["/load"] = new SlashCommand
        {
            Name = "/load",
            Description = "Load session by ID",
            Usage = "/load <id>",
            Handler = async (args, session) => args.Length > 0 
                ? await _sessionCommands.HandleLoad(args[0], session).ConfigureAwait(false)
                : CommandResult.Fail("Usage: /load <session-id>")
        };

        _commands["/sessions"] = new SlashCommand
        {
            Name = "/sessions",
            Description = "List all saved sessions",
            Usage = "/sessions",
            Handler = async (args, session) => await _sessionCommands.HandleSessions().ConfigureAwait(false)
        };

        _commands["/delete"] = new SlashCommand
        {
            Name = "/delete",
            Description = "Delete a session",
            Usage = "/delete <id>",
            Handler = async (args, session) => args.Length > 0
                ? await _sessionCommands.HandleDelete(args[0]).ConfigureAwait(false)
                : CommandResult.Fail("Usage: /delete <session-id>")
        };

        _commands["/provider"] = new SlashCommand
        {
            Name = "/provider",
            Description = "Switch AI provider",
            Usage = "/provider",
            Handler = (_, _) => Task.FromResult(HandleProvider())
        };
    }

    private CommandResult HandleProvider()
    {
        var currentProvider = _userSettingsService.Settings.Provider;
        var lines = new List<string>
        {
            "",
            $"[bold]Current provider:[/] [cyan]{currentProvider}[/]",
            "",
            "[bold]Available providers:[/]",
            "  [dim]alibabacloud[/] - Alibaba Cloud (Qwen, GLM, MiniMax)",
            "  [dim]openai[/]       - OpenAI",
            "  [dim]anthropic[/]    - Anthropic",
            "",
            "[dim]To switch providers, restart Pebbles and select a different provider.[/]",
            "[dim]Your API keys are stored as environment variables.[/]",
            ""
        };

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    private CommandResult HandleHelp()
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
        var provider = _userSettingsService.Settings.Provider;
        List<string> modelIds = [];

        // Get models from user settings ModelProviders configuration
        if (_userSettingsService.Settings.ModelProviders.TryGetValue(provider, out var providerModels))
        {
            modelIds = providerModels.Select(m => m.Id).ToList();
        }

        // Fallback to PebblesOptions if no configured models
        if (modelIds.Count == 0)
        {
            modelIds = _options.AvailableModels.ToList();
        }

        if (args.Length == 0)
        {
            var selected = _modelPicker.PickModel(modelIds, session.Model);
            if (selected is not null && selected != session.Model)
            {
                session.Model = selected;
                return CommandResult.Ok($"Switched to model: {selected}");
            }
            return CommandResult.Ok("");
        }

        var target = args[0];
        if (modelIds.Contains(target))
        {
            session.Model = target;
            return CommandResult.Ok($"Switched to model: {target}");
        }

        return CommandResult.Fail($"Unknown model: {target}. Use /model to see available models.");
    }

    private CommandResult HandleContext()
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