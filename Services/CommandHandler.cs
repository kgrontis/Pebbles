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
    private readonly Dictionary<string, SlashCommand> _extensionCommands;
    private readonly PebblesOptions _options;
    private readonly ContextManager _contextManager;
    private readonly IFileService _fileService;
    private readonly IModelPicker _modelPicker;
    private readonly IExtensionLoader _extensionLoader;

    public CommandHandler(
        PebblesOptions options,
        ContextManager contextManager,
        IFileService fileService,
        IModelPicker modelPicker,
        IExtensionLoader extensionLoader)
    {
        _options = options;
        _contextManager = contextManager;
        _fileService = fileService;
        _modelPicker = modelPicker;
        _extensionLoader = extensionLoader;
        _extensionCommands = new Dictionary<string, SlashCommand>(StringComparer.OrdinalIgnoreCase);

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
                Description = "Reload extensions",
                Usage = "/reload",
                Handler = HandleReload
            },
            ["/extensions"] = new SlashCommand
            {
                Name = "/extensions",
                Description = "List loaded extensions",
                Usage = "/extensions",
                Handler = HandleExtensions
            },
            ["/exit"] = new SlashCommand
            {
                Name = "/exit",
                Description = "Exit Pebbles",
                Usage = "/exit",
                Handler = HandleExit
            }
        };

        // Load extension commands on startup
        RefreshExtensionCommands();
    }

    /// <summary>
    /// All commands (built-in + extensions).
    /// </summary>
    public IEnumerable<SlashCommand> Commands => _commands.Values.Concat(_extensionCommands.Values);

    /// <summary>
    /// Built-in commands only.
    /// </summary>
    public IEnumerable<SlashCommand> BuiltInCommands => _commands.Values;

    /// <summary>
    /// Extension commands only.
    /// </summary>
    public IEnumerable<SlashCommand> ExtensionCommands => _extensionCommands.Values;

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

        // Then check extension commands
        if (_extensionCommands.TryGetValue(cmdName, out var extCommand))
            return await extCommand.Handler(args, session);

        return CommandResult.Fail($"Unknown command: {cmdName}. Type /help for available commands.");
    }

    /// <summary>
    /// Refresh extension commands from the extension loader.
    /// </summary>
    public void RefreshExtensionCommands()
    {
        _extensionCommands.Clear();

        foreach (var cmd in _extensionLoader.GetExtensionCommands())
        {
            _extensionCommands[cmd.Name] = cmd;
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

        if (_extensionCommands.Count > 0)
        {
            lines.Add("");
            lines.Add("[bold]Extension Commands[/]");
            lines.Add("");

            foreach (var cmd in _extensionCommands.Values.OrderBy(c => c.Name))
                lines.Add($"  {cmd.Usage,-25} {cmd.Description}");
        }

        lines.Add("");

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
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

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
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

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
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

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleFiles(string[] args, ChatSession session)
    {
        var files = _fileService.LoadedFiles;

        if (files.Count == 0)
        {
            return Task.FromResult(CommandResult.Ok("\n[dim]No files loaded. Use /read <path> or @file.cs syntax to load files.[/]\n"));
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

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleClearFiles(string[] args, ChatSession session)
    {
        var count = _fileService.LoadedFiles.Count;
        _fileService.ClearFiles();

        return Task.FromResult(CommandResult.Ok($"\n[dim]Cleared {count} file(s) from context.[/]\n"));
    }

    private Task<CommandResult> HandleReload(string[] args, ChatSession session)
    {
        var result = _extensionLoader.LoadExtensions();
        RefreshExtensionCommands();

        var lines = new List<string>
        {
            "",
            $"[bold green]✓[/] Reloaded extensions",
            ""
        };

        if (result.Extensions.Count > 0)
        {
            lines.Add($"  Extensions: {result.TotalCommands} command(s) from {result.Extensions.Count} extension(s)");
            foreach (var ext in result.Extensions)
            {
                lines.Add($"    [dim]•[/] {ext.Name} v{ext.Version} ({ext.Commands.Count} commands)");
            }
        }
        else
        {
            lines.Add("  [dim]No extensions loaded.[/]");
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

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
    }

    private Task<CommandResult> HandleExtensions(string[] args, ChatSession session)
    {
        var extensions = _extensionLoader.Extensions;

        if (extensions.Count == 0)
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Task.FromResult(CommandResult.Ok($"""

                [dim]No extensions loaded.[/]

                Extension directories:
                  Global:   [dim]~/.pebbles/agent/extensions/scripts/[/]
                  Project:  [dim]./.pebbles/agent/extensions/scripts/[/]

                Create a Lua script in one of these directories to add custom commands.
                Use /reload to load new extensions.
                """));
        }

        var lines = new List<string>
        {
            "",
            $"[bold]Loaded Extensions ({extensions.Count})[/]",
            ""
        };

        foreach (var ext in extensions)
        {
            lines.Add($"  [bold]{ext.Name}[/] [dim]v{ext.Version}[/]");
            if (!string.IsNullOrEmpty(ext.Description))
                lines.Add($"    [dim]{ext.Description}[/]");
            lines.Add($"    [dim]Commands: {ext.Commands.Count}[/]");
            foreach (var cmd in ext.Commands)
            {
                lines.Add($"      [dim]•[/] {cmd.Name} — {cmd.Description}");
            }
            lines.Add("");
        }

        lines.Add("[dim]Use /reload to reload extensions.[/]");

        return Task.FromResult(CommandResult.Ok(string.Join("\n", lines)));
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