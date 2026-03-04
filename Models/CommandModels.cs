namespace Pebbles.Models;

public class SlashCommand
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Usage { get; init; } = string.Empty;
    public Func<string[], ChatSession, Task<CommandResult>> Handler { get; init; } = null!;
}

public class CommandResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public bool ShouldExit { get; init; }
    public bool ShouldClear { get; init; }
}
