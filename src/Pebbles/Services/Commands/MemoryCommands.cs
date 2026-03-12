namespace Pebbles.Services.Commands;

using Pebbles.Models;

/// <summary>
/// Handles memory-related commands: /remember, /memory.
/// </summary>
internal sealed class MemoryCommands(IMemoryService? memoryService)
{
    public CommandResult HandleRemember(string[] args)
    {
        if (memoryService is null)
        {
            return CommandResult.Fail("Memory service not available.");
        }

        if (args.Length == 0)
        {
            return CommandResult.Fail("Usage: /remember <text>\nExample: /remember I prefer minimal comments in code");
        }

        var memory = string.Join(" ", args);

        if (memoryService.Remember(memory))
        {
            return CommandResult.OkWithMarkup($"\n[bold green]✓[/] Remembered: [dim]{Spectre.Console.Markup.Escape(memory)}[/]\n");
        }

        return CommandResult.Fail("Failed to save memory.");
    }

    public CommandResult HandleMemory(string[] args)
    {
        if (memoryService is null)
        {
            return CommandResult.Fail("Memory service not available.");
        }

        if (args.Length > 0 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            if (memoryService.ClearMemories())
            {
                return CommandResult.Ok("All memories cleared.");
            }
            return CommandResult.Fail("Failed to clear memories.");
        }

        var memories = memoryService.GetMemories();

        if (string.IsNullOrWhiteSpace(memories) || memories.Contains("Store your preferences", StringComparison.InvariantCultureIgnoreCase))
        {
            return CommandResult.OkWithMarkup("""

                [dim]No memories saved yet.[/]

                Use [bold]/remember <text>[/] to save something:
                  [dim]/remember I prefer minimal comments in code[/]
                  [dim]/remember Use British English spelling[/]

                """);
        }

        var lines = new List<string>
        {
            "",
            "[bold]Saved Memories[/]",
            "",
            $"[dim]{Spectre.Console.Markup.Escape(memories)}[/]",
            "",
            "[dim]Use /remember <text> to add more, or /memory clear to remove all.[/]",
            ""
        };

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }
}