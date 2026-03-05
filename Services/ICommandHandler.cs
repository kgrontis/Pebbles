namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Handles slash command parsing and execution.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Available commands for display.
    /// </summary>
    IEnumerable<SlashCommand> Commands { get; }

    /// <summary>
    /// Checks if the input is a slash command.
    /// </summary>
    bool IsCommand(string input);

    /// <summary>
    /// Executes a slash command.
    /// </summary>
    Task<CommandResult> ExecuteAsync(string input, ChatSession session);
}