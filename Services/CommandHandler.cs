namespace Pebbles.Services;

using Spectre.Console;
using Pebbles.Models;
using Pebbles.Configuration;

/// <summary>
/// Handles slash command parsing and execution.
/// </summary>
public class CommandHandler : ICommandHandler
{
    private readonly Dictionary<string, SlashCommand> _commands;
    private readonly Dictionary<string, SlashCommand> _pluginCommands;
    private readonly PebblesOptions _options;
    private readonly ContextManager _contextManager;
    private readonly IFileService _fileService;
    private readonly IModelPicker _modelPicker;
    private readonly IPluginLoader _pluginLoader;

    public CommandHandler(
        PebblesOptions options,
        ContextManager contextManager,
        IFileService fileService,
        IModelPicker modelPicker,
        IPluginLoader pluginLoader)
    {
        _options = options;
        _contextManager = contextManager;
        _fileService = fileService;
        _modelPicker = modelPicker;
        _pluginLoader = pluginLoader;
        _pluginCommands = new Dictionary<string, SlashCommand>(StringComparer.OrdinalIgnoreCase);

        _commands = new Dictionary<string, SlashCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["/help"] = new SlashCommand
            {
                Name = "/help",
                Description = "Show available commands",
                Usage = "/help",
                Handler = HandleHelp
            },
            ["/clear"] = new SlashCommand
            {
                Name = "/clear",
                Description = "Clear chat history",
                Usage = "/clear",
                Handler = HandleClear
            },
            ["/model"] = new SlashCommand
            {
                Name = "/model",
                Description = "Switch AI model",
                Usage = "/model <model-name>",
                Handler = HandleModel
            },
            ["/compact"] = new SlashCommand
            {
                Name = "/compact",
                Description = "Toggle compact mode (hide thinking)",
                Usage = "/compact",
                Handler = HandleCompact
            },
            ["/history"] = new SlashCommand
            {
                Name = "/history",
                Description = "Show conversation history summary",
                Usage = "/history",
                Handler = HandleHistory
            },
            ["/cost"] = new SlashCommand
            {
                Name = "/cost",
                Description = "Show token usage and estimated cost",
                Usage = "/cost",
                Handler = HandleCost
            },
            ["/context"] = new SlashCommand
            {
                Name = "/context",
                Description = "Show loaded project context",
                Usage = "/context",
                Handler = HandleContext
            },
            ["/read"] = new SlashCommand
            {
                Name = "/read",
                Description = "Read a file into context",
                Usage = "/read <path>",
                Handler = HandleRead
            },
            ["/files"] = new SlashCommand
            {
                Name = "/files",
                Description = "List loaded files in context",
                Usage = "/files",
                Handler = HandleFiles
            },
            ["/clearfiles"] = new SlashCommand
            {
                Name = "/clearfiles",
                Description = "Clear all loaded files from context",
                Usage = "/clearfiles",
                Handler = HandleClearFiles
            },
            ["/reload"] = new SlashCommand
            {
                Name = "/reload",
                Description = "Reload plugins",
                Usage = "/reload",
                Handler = HandleReload
            },
            ["/plugins"] = new SlashCommand
            {
                Name = "/plugins",
                Description = "List loaded plugins",
                Usage = "/plugins",
                Handler = HandlePlugins
            },
            ["/exit"] = new SlashCommand
            {
                Name = "/exit",
                Description = "Exit Pebbles",
                Usage = "/exit",
                Handler = HandleExit
            }
        };

        // Load plugin commands on startup
        _pluginLoader.LoadPlugins();
        RefreshPluginCommands();
    }

    /// <summary>
    /// All commands (built-in + plugins).
    /// </summary>
    public IEnumerable<SlashCommand> Commands => _commands.Values.Concat(_pluginCommands.Values);

    /// <summary>
    /// Built-in commands only.
    /// </summary>
    public IEnumerable<SlashCommand> BuiltInCommands => _commands.Values;

    /// <summary>
    /// Plugin commands only.
    /// </summary>
    public IEnumerable<SlashCommand> PluginCommands => _pluginCommands.Values;

    public bool IsCommand(string input) =>
        input.TrimStart().StartsWith('/');

    public async Task<CommandResult> ExecuteAsync(string input, ChatSession session)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return CommandResult.Fail("Empty command.");

        var cmdName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        // Check built-in commands first
        if (_commands.TryGetValue(cmdName, out var command))
            return await command.Handler(args, session);

        // Then check plugin commands
        if (_pluginCommands.TryGetValue(cmdName, out var pluginCommand))
            return await pluginCommand.Handler(args, session);

        return CommandResult.Fail($"Unknown command: {cmdName}. Type /help for available commands.");
    }

    /// <summary>
    /// Refresh plugin commands from the plugin loader.
    /// </summary>
    public void RefreshPluginCommands()
    {
        _pluginCommands.Clear();

        foreach (var cmd in _pluginLoader.GetPluginCommands())
        {
            _pluginCommands[cmd.Name] = cmd;
        }
    }

    private Task<CommandResult> HandleHelp(string[] args, ChatSession session)
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

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleClear(string[] args, ChatSession session)
    {
        session.Messages.Clear();
        return Task.FromResult(new CommandResult { Success = true, Message = "Chat history cleared.", ShouldClear = true });
    }

    private Task<CommandResult> HandleModel(string[] args, ChatSession session)
    {
        var models = _options.AvailableModels;

        if (args.Length == 0)
        {
            // Show interactive picker
            var selected = _modelPicker.PickModel(models, session.Model);
            if (selected is not null && selected != session.Model)
            {
                session.Model = selected;
                return Task.FromResult(CommandResult.Ok($"Switched to model: {selected}"));
            }
            return Task.FromResult(CommandResult.Ok(""));
        }

        var target = args[0];
        if (models.Contains(target))
        {
            session.Model = target;
            return Task.FromResult(CommandResult.Ok($"Switched to model: {target}"));
        }

        return Task.FromResult(CommandResult.Fail($"Unknown model: {target}. Use /model to see available models."));
    }

    private Task<CommandResult> HandleCompact(string[] args, ChatSession session)
    {
        session.CompactMode = !session.CompactMode;
        return Task.FromResult(CommandResult.Ok(
            $"Compact mode: {(session.CompactMode ? "ON" : "OFF")} — thinking blocks will be {(session.CompactMode ? "hidden" : "shown")}."));
    }

    private Task<CommandResult> HandleHistory(string[] args, ChatSession session)
    {
        if (session.Messages.Count == 0)
            return Task.FromResult(CommandResult.Ok("No messages yet."));

        var lines = new List<string> { $"Session {session.Id} — {session.Messages.Count} messages\n" };
        foreach (var msg in session.Messages)
        {
            var role = msg.Role == ChatRole.User ? "You" : "Pebbles";
            var preview = msg.Content.Length > 80
                ? msg.Content[..80].Replace("\n", " ") + "..."
                : msg.Content.Replace("\n", " ");
            lines.Add($"  [{msg.Timestamp:HH:mm:ss}] {role}: {preview}");
        }

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleCost(string[] args, ChatSession session)
    {
        return Task.FromResult(CommandResult.Ok($"""
            Token Usage (Session {session.Id}):
              Input tokens:  {session.TotalInputTokens:N0}
              Output tokens: {session.TotalOutputTokens:N0}
              Total tokens:  {session.TotalInputTokens + session.TotalOutputTokens:N0}
              Est. cost:     ${session.TotalCost:F4}
            """));
    }

    private Task<CommandResult> HandleContext(string[] args, ChatSession session)
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
                lines.Add($"[dim]{Markup.Escape(preview)}[/]");
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

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleRead(string[] args, ChatSession session)
    {
        if (args.Length == 0)
        {
            return Task.FromResult(CommandResult.Fail("Usage: /read <path>\nExample: /read Program.cs"));
        }

        var path = string.Join(" ", args);
        var content = _fileService.ReadFile(path);

        if (!content.Success)
        {
            return Task.FromResult(CommandResult.Fail(content.Error ?? "Unknown error reading file"));
        }

        var lines = new List<string>
        {
            "",
            $"[bold green]✓[/] Loaded: [dim]{path}[/] ({FormatSize(content.Size)})",
            "",
            $"[dim]─── {path} ───[/]",
            Markup.Escape(content.Content),
            "[dim]───[/]",
            ""
        };

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleFiles(string[] args, ChatSession session)
    {
        var files = _fileService.LoadedFiles;

        if (files.Count == 0)
        {
            return Task.FromResult(CommandResult.OkWithMarkup("\n[dim]No files loaded. Use /read <path> or @file.cs syntax to load files.[/]\n"));
        }

        var lines = new List<string>
        {
            "",
            $"[bold]Loaded Files ({files.Count})[/]",
            ""
        };

        foreach (var (path, content) in files)
        {
            var status = content.Success ? "[green]✓[/]" : "[red]✗[/]";
            var size = content.Success ? FormatSize(content.Size) : content.Error;
            lines.Add($"  {status} [dim]{path}[/] ({size})");
        }

        lines.Add("");
        lines.Add("[dim]Files are included in AI context automatically.[/]");
        lines.Add("[dim]Use /clearfiles to remove all files from context.[/]");

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleClearFiles(string[] args, ChatSession session)
    {
        var count = _fileService.LoadedFiles.Count;
        _fileService.ClearFiles();

        return Task.FromResult(CommandResult.OkWithMarkup($"\n[dim]Cleared {count} file(s) from context.[/]\n"));
    }

    private Task<CommandResult> HandleReload(string[] args, ChatSession session)
    {
        var result = _pluginLoader.LoadPlugins();
        RefreshPluginCommands();

        var lines = new List<string>
        {
            "",
            $"[bold green]✓[/] Reloaded plugins",
            ""
        };

        if (result.Plugins.Count > 0)
        {
            lines.Add($"  Plugins: {result.TotalCommands} command(s) from {result.Plugins.Count} plugin(s)");
            foreach (var plugin in result.Plugins)
            {
                lines.Add($"    [dim]•[/] {plugin.Name} v{plugin.Version} ({plugin.Instance?.GetCommands().Count() ?? 0} commands)");
            }
        }
        else
        {
            lines.Add("  [dim]No plugins loaded.[/]");
        }

        if (result.Errors.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold yellow]Warnings:[/]");
            foreach (var (path, error) in result.Errors)
            {
                lines.Add($"    [dim]•[/] {Path.GetFileName(path)}: {error}");
            }
        }

        lines.Add("");

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandlePlugins(string[] args, ChatSession session)
    {
        var plugins = _pluginLoader.Plugins;

        if (plugins.Count == 0)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Task.FromResult(CommandResult.OkWithMarkup($"""

                [dim]No plugins loaded.[/]

                Plugin directories:
                  Global:   [dim]~/.pebbles/agent/plugins/scripts/[/]
                  Project:  [dim]./.pebbles/agent/plugins/scripts/[/]

                Create a C# script in one of these directories to add custom commands.
                Use /reload to load new plugins.
                """));
        }

        var lines = new List<string>
        {
            "",
            $"[bold]Loaded Plugins ({plugins.Count})[/]",
            ""
        };

        foreach (var plugin in plugins)
        {
            lines.Add($"  [bold]{plugin.Name}[/] [dim]v{plugin.Version}[/]");
            if (!string.IsNullOrEmpty(plugin.Description))
                lines.Add($"    [dim]{plugin.Description}[/]");
            var commands = plugin.Instance?.GetCommands().ToList() ?? [];
            lines.Add($"    [dim]Commands: {commands.Count}[/]");
            foreach (var cmd in commands)
            {
                lines.Add($"      [dim]•[/] {cmd.Name} — {cmd.Description}");
            }
            lines.Add("");
        }

        lines.Add("[dim]Use /reload to reload plugins.[/]");

        return Task.FromResult(CommandResult.OkWithMarkup(string.Join("\n", lines)));
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes / (1024 * 1024):F1} MB"
        };

    private Task<CommandResult> HandleExit(string[] args, ChatSession session)
    {
        return Task.FromResult(CommandResult.Exit("Goodbye! 👋"));
    }
}