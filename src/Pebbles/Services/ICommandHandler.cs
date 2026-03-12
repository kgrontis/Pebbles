namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Handles slash command parsing and execution.
/// </summary>
internal interface ICommandHandler
{
    /// <summary>
    /// Available commands for display.
    /// </summary>
    IEnumerable<SlashCommand> Commands { get; }

    /// <summary>
    /// Built-in commands only.
    /// </summary>
    IEnumerable<SlashCommand> BuiltInCommands { get; }

    /// <summary>
    /// Plugin commands only.
    /// </summary>
    IEnumerable<SlashCommand> PluginCommands { get; }

    /// <summary>
    /// Checks if the input is a slash command.
    /// </summary>
    bool IsCommand(string input);

    /// <summary>
    /// Executes a slash command.
    /// </summary>
    Task<CommandResult> ExecuteAsync(string input, ChatSession session);

    /// <summary>
    /// Refreshes plugin commands from the plugin loader.
    /// </summary>
    void RefreshPluginCommands();
}