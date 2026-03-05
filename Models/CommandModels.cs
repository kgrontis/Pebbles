namespace Pebbles.Models;

/// <summary>
/// Represents a slash command definition.
/// </summary>
public record SlashCommand
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
    public Func<string[], ChatSession, Task<CommandResult>> Handler { get; init; } = null!;
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
    /// Creates a successful result with an optional message.
    /// </summary>
    public static CommandResult Ok(string? message = null) => new() { Success = true, Message = message };

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static CommandResult Fail(string message) => new() { Success = false, Message = message };

    /// <summary>
    /// Creates a result that exits the application.
    /// </summary>
    public static CommandResult Exit(string? message = null) => new() { Success = true, Message = message, ShouldExit = true };

    /// <summary>
    /// Creates a result that clears the screen.
    /// </summary>
    public static CommandResult Clear() => new() { Success = true, ShouldClear = true };
}