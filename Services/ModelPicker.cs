namespace Pebbles.Services;

/// <summary>
/// Interactive model picker for selecting AI models.
/// </summary>
public interface IModelPicker
{
    /// <summary>
    /// Shows an interactive model picker and returns the selected model.
    /// </summary>
    /// <param name="models">Available models</param>
    /// <param name="currentModel">Currently selected model</param>
    /// <returns>Selected model, or null if cancelled</returns>
    string? PickModel(string[] models, string currentModel);
}

/// <summary>
/// Console-based interactive model picker.
/// </summary>
public class ModelPicker : IModelPicker
{
    public string? PickModel(string[] models, string currentModel)
    {
        if (models.Length == 0)
            return null;

        var selectedIndex = Array.FindIndex(models, m => m == currentModel);
        if (selectedIndex < 0) selectedIndex = 0;

        var startRow = Console.CursorTop + 1;
        var maxDisplay = Math.Min(10, models.Length);
        var scrollOffset = 0;

        Render(models, currentModel, selectedIndex, scrollOffset, startRow, maxDisplay);

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            switch (key.Key)
            {
                case ConsoleKey.Enter:
                    ClearPicker(startRow, maxDisplay);
                    return models[selectedIndex];

                case ConsoleKey.Escape:
                    ClearPicker(startRow, maxDisplay);
                    return null;

                case ConsoleKey.UpArrow:
                    if (selectedIndex > 0)
                    {
                        selectedIndex--;
                        if (selectedIndex < scrollOffset)
                            scrollOffset = selectedIndex;
                        Render(models, currentModel, selectedIndex, scrollOffset, startRow, maxDisplay);
                    }
                    break;

                case ConsoleKey.DownArrow:
                    if (selectedIndex < models.Length - 1)
                    {
                        selectedIndex++;
                        if (selectedIndex >= scrollOffset + maxDisplay)
                            scrollOffset = selectedIndex - maxDisplay + 1;
                        Render(models, currentModel, selectedIndex, scrollOffset, startRow, maxDisplay);
                    }
                    break;
            }
        }
    }

    private void Render(string[] models, string currentModel, int selected, int scrollOffset, int startRow, int maxDisplay)
    {
        var nameW = Math.Max(24, models.Max(m => m.Length));
        var innerW = 2 + nameW + 2;

        // Header
        Console.SetCursorPosition(0, startRow);
        var scrollText = models.Length > maxDisplay 
            ? $" ({scrollOffset + 1}-{Math.Min(scrollOffset + maxDisplay, models.Length)}/{models.Length})" 
            : "";
        var dashCount = Math.Max(1, innerW - 6 - scrollText.Length);
        Spectre.Console.AnsiConsole.Markup($"[dim grey]  Models{scrollText} {new string('─', dashCount)}[/]");

        // Items
        for (var i = 0; i < maxDisplay; i++)
        {
            var idx = scrollOffset + i;
            if (idx >= models.Length) break;

            var model = models[idx];
            var isCurrent = model == currentModel;
            var isSelected = idx == selected;
            var display = model.PadRight(nameW);

            Console.SetCursorPosition(0, startRow + 1 + i);

            if (isSelected)
            {
                var marker = isCurrent ? "● " : "  ";
                Spectre.Console.AnsiConsole.Markup($"  [on grey23] {marker}[bold white]{display}[/][{(isCurrent ? "dim green" : "dim")}] {(isCurrent ? "(current)" : "")}[/][/]");
            }
            else
            {
                var marker = isCurrent ? "[green]●[/] " : "  ";
                Spectre.Console.AnsiConsole.Markup($"  {marker}[cyan]{display}[/] [dim]{(isCurrent ? "(current)" : "")}[/]");
            }
        }

        // Footer hint
        Console.SetCursorPosition(0, startRow + 1 + maxDisplay);
        Spectre.Console.AnsiConsole.Markup("[dim grey]  ──────────────────────────────────────────[/]");

        // Restore cursor to hint line
        Console.SetCursorPosition(0, startRow + 2 + maxDisplay);
        Spectre.Console.AnsiConsole.Markup("[dim]  ↑↓ Select • Enter Confirm • Esc Cancel[/]");
    }

    private void ClearPicker(int startRow, int lineCount)
    {
        for (var i = 0; i <= lineCount + 2; i++)
        {
            var y = startRow + i;
            if (y >= Console.BufferHeight) break;
            Console.SetCursorPosition(0, y);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }
        Console.SetCursorPosition(0, startRow);
    }
}