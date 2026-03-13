namespace Pebbles.Models;

/// <summary>
/// Represents a slash command definition.
/// </summary>
public record SlashCommand
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
    public Func<string[], ChatSession, Task<CommandResult>>? Handler { get; init; }
}

/// <summary>
/// Result of executing a slash command.
/// </summary>
public record CommandResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public bool ShouldExit { get; init; }
    public bool ShouldClear { get; init; }

    /// <summary>
    /// If true, the message contains Spectre.Console markup and should not be escaped.
    /// </summary>
    public bool AllowMarkup { get; init; }

    /// <summary>
    /// If true, render the message on its own line without the status indicator.
    /// </summary>
    public bool RawOutput { get; init; }

    /// <summary>
    /// Creates a successful result with an optional message.
    /// </summary>
    public static CommandResult Ok(string? message = null) => new() { Success = true, Message = message };

    /// <summary>
    /// Creates a successful result with Spectre markup (not escaped).
    /// </summary>
    public static CommandResult OkWithMarkup(string message) => new() { Success = true, Message = message, AllowMarkup = true };

    /// <summary>
    /// Creates a successful result with raw output (not escaped, on separate line).
    /// </summary>
    public static CommandResult Raw(string message, bool allowMarkup = false) => new()
    {
        Success = true,
        Message = message,
        RawOutput = true,
        AllowMarkup = allowMarkup
    };

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static CommandResult Fail(string message) => new() { Success = false, Message = message };

    /// <summary>
    /// Creates a result that exits the application.
    /// </summary>
    public static CommandResult Exit(string? message = null) => new() { Success = true, Message = message };

    /// <summary>
    /// Creates a result that clears the screen.
    /// </summary>
    public static CommandResult Clear() => new() { Success = true, ShouldClear = true };
}