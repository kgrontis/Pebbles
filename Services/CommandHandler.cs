namespace Pebbles.Services;

using Pebbles.Models;
using Pebbles.Configuration;

/// <summary>
/// Handles slash command parsing and execution.
/// </summary>
public class CommandHandler : ICommandHandler
{
    private readonly Dictionary<string, SlashCommand> _commands;
    private readonly PebblesOptions _options;

    public CommandHandler(PebblesOptions options)
    {
        _options = options;
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
            ["/exit"] = new SlashCommand
            {
                Name = "/exit",
                Description = "Exit Pebbles",
                Usage = "/exit",
                Handler = HandleExit
            }
        };
    }

    public IEnumerable<SlashCommand> Commands => _commands.Values;

    public bool IsCommand(string input) =>
        input.TrimStart().StartsWith('/');

    public async Task<CommandResult> ExecuteAsync(string input, ChatSession session)
    {
        var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return CommandResult.Fail("Empty command.");

        var cmdName = parts[0];
        var args = parts.Length > 1 ? parts[1..] : [];

        if (_commands.TryGetValue(cmdName, out var command))
            return await command.Handler(args, session);

        return CommandResult.Fail($"Unknown command: {cmdName}. Type /help for available commands.");
    }

    private Task<CommandResult> HandleHelp(string[] args, ChatSession session)
    {
        var lines = new List<string> { "" };
        foreach (var cmd in _commands.Values.OrderBy(c => c.Name))
            lines.Add($"  {cmd.Usage,-25} {cmd.Description}");
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
            var list = string.Join("\n", models.Select(m =>
                $"  {(m == session.Model ? "● " : "  ")}{m}{(m == session.Model ? " (current)" : "")}"));
            return Task.FromResult(CommandResult.Ok($"Available models:\n{list}\n\nUsage: /model <name>"));
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

    private Task<CommandResult> HandleExit(string[] args, ChatSession session)
    {
        return Task.FromResult(CommandResult.Exit("Goodbye! 👋"));
    }
}