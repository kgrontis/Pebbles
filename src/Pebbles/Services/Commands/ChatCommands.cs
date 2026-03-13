namespace Pebbles.Services.Commands;

using Pebbles.Models;

/// <summary>
/// Handles chat-related commands: /clear, /history, /cost, /exit.
/// </summary>
public sealed class ChatCommands
{
    public static CommandResult HandleClear(ChatSession session)
    {
        session.Messages.Clear();
        return new CommandResult { Success = true, Message = "Chat history cleared.", ShouldClear = true };
    }

    public static CommandResult HandleHistory(ChatSession session)
    {
        if (session.Messages.Count == 0)
            return CommandResult.Ok("No messages yet.");

        var lines = new List<string> { $"Session {session.Id} — {session.Messages.Count} messages\n" };
        foreach (var msg in session.Messages)
        {
            var role = msg.Role == ChatRole.User ? "You" : "Pebbles";
            var preview = msg.Content.Length > 80
                ? msg.Content[..80].Replace("\n", " ", StringComparison.InvariantCultureIgnoreCase) + "..."
                : msg.Content.Replace("\n", " ", StringComparison.InvariantCultureIgnoreCase);
            lines.Add($"  [{msg.Timestamp:HH:mm:ss}] {role}: {preview}");
        }

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    public static CommandResult HandleCost(ChatSession session)
    {
        return CommandResult.Ok($"""
            Token Usage (Session {session.Id}):
              Input tokens:  {session.TotalInputTokens:N0}
              Output tokens: {session.TotalOutputTokens:N0}
              Total tokens:  {session.TotalInputTokens + session.TotalOutputTokens:N0}
              Est. cost:     ${session.TotalCost:F4}
            """);
    }

    public static CommandResult HandleExit() => CommandResult.Exit("Goodbye! 👋");
}