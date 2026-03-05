namespace Pebbles.UI;

using Spectre.Console;
using Pebbles.Models;

/// <summary>
/// Handles user input with history and autocomplete.
/// </summary>
public class InputHandler : IInputHandler
{
    private readonly List<SlashCommand> _commands;
    private readonly List<string> _inputHistory = [];
    private int _historyIndex = -1;
    private int _inputStartCol;
    private int _inputRow;
    private int _borderWidth;
    private const string Placeholder = "Type a message, or /help for commands";
    private const string BorderColor = "dodgerblue2";

    public InputHandler(IEnumerable<SlashCommand> commands)
    {
        _commands = commands.OrderBy(c => c.Name).ToList();
    }

    public string? ReadInput()
    {
        // Top border
        _borderWidth = Console.WindowWidth - 1;
        AnsiConsole.MarkupLine($"[{BorderColor}]{new string('─', _borderWidth)}[/]");

        // Prompt
        AnsiConsole.Markup("[bold dodgerblue2]❯[/] ");
        _inputStartCol = Console.CursorLeft;
        _inputRow = Console.CursorTop;

        var buffer = new List<char>();
        var cursorPos = 0;
        var selectedSuggestion = -1;
        var suggestions = new List<SlashCommand>();
        var showingSuggestions = false;
        var suggestionLinesRendered = 0;

        // Show placeholder + bottom border
        RenderLine(buffer, cursorPos);
        RenderBottomBorder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            // Ctrl+C
            if (key is { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control })
            {
                ClearSuggestions(suggestionLinesRendered);
                RenderBottomBorder();
                Console.SetCursorPosition(0, _inputRow + 2);
                AnsiConsole.MarkupLine("[dim]Goodbye! 👋[/]");
                Environment.Exit(0);
            }

            // Enter — submit
            if (key.Key == ConsoleKey.Enter)
            {
                ClearSuggestions(suggestionLinesRendered);
                RenderBottomBorder();
                Console.SetCursorPosition(0, _inputRow + 2);
                var result = new string(buffer.ToArray());
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _inputHistory.Add(result);
                    _historyIndex = _inputHistory.Count;
                }
                return result;
            }

            // Tab — accept suggestion or cycle
            if (key.Key == ConsoleKey.Tab)
            {
                if (showingSuggestions && suggestions.Count > 0)
                {
                    if (key.Modifiers.HasFlag(ConsoleModifiers.Shift))
                        selectedSuggestion = selectedSuggestion <= 0 ? suggestions.Count - 1 : selectedSuggestion - 1;
                    else
                        selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;

                    var cmd = suggestions[selectedSuggestion].Name;
                    buffer.Clear();
                    buffer.AddRange(cmd);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                    RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered);
                    continue;
                }

                if (buffer.Count > 0 && buffer[0] == '/')
                {
                    var prefix = new string(buffer.ToArray());
                    suggestions = _commands.Where(c =>
                        c.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (suggestions.Count > 0)
                    {
                        showingSuggestions = true;
                        selectedSuggestion = 0;
                        var cmd = suggestions[0].Name;
                        buffer.Clear();
                        buffer.AddRange(cmd);
                        cursorPos = buffer.Count;
                        RenderLine(buffer, cursorPos);
                        RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered);
                    }
                    continue;
                }

                continue;
            }

            // Escape — dismiss suggestions or clear buffer
            if (key.Key == ConsoleKey.Escape)
            {
                if (showingSuggestions)
                {
                    ClearSuggestions(suggestionLinesRendered);
                    suggestionLinesRendered = 0;
                    showingSuggestions = false;
                    selectedSuggestion = -1;
                    RenderBottomBorder();
                    continue;
                }
                buffer.Clear();
                cursorPos = 0;
                RenderLine(buffer, cursorPos);
                continue;
            }

            // Up arrow — suggestion navigation or history
            if (key.Key == ConsoleKey.UpArrow)
            {
                if (showingSuggestions && suggestions.Count > 0)
                {
                    selectedSuggestion = selectedSuggestion <= 0 ? suggestions.Count - 1 : selectedSuggestion - 1;
                    var cmd = suggestions[selectedSuggestion].Name;
                    buffer.Clear();
                    buffer.AddRange(cmd);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                    RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered);
                    continue;
                }

                if (_inputHistory.Count > 0 && _historyIndex > 0)
                {
                    _historyIndex--;
                    buffer.Clear();
                    buffer.AddRange(_inputHistory[_historyIndex]);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                }
                continue;
            }

            // Down arrow — suggestion navigation or history
            if (key.Key == ConsoleKey.DownArrow)
            {
                if (showingSuggestions && suggestions.Count > 0)
                {
                    selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;
                    var cmd = suggestions[selectedSuggestion].Name;
                    buffer.Clear();
                    buffer.AddRange(cmd);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                    RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered);
                    continue;
                }

                if (_historyIndex < _inputHistory.Count - 1)
                {
                    _historyIndex++;
                    buffer.Clear();
                    buffer.AddRange(_inputHistory[_historyIndex]);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                }
                else
                {
                    _historyIndex = _inputHistory.Count;
                    buffer.Clear();
                    cursorPos = 0;
                    RenderLine(buffer, cursorPos);
                }
                continue;
            }

            // Left/Right arrow — cursor movement
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursorPos > 0)
                {
                    cursorPos--;
                    Console.SetCursorPosition(_inputStartCol + cursorPos, _inputRow);
                }
                continue;
            }
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursorPos < buffer.Count)
                {
                    cursorPos++;
                    Console.SetCursorPosition(_inputStartCol + cursorPos, _inputRow);
                }
                continue;
            }

            // Home / End
            if (key.Key == ConsoleKey.Home)
            {
                cursorPos = 0;
                Console.SetCursorPosition(_inputStartCol, _inputRow);
                continue;
            }
            if (key.Key == ConsoleKey.End)
            {
                cursorPos = buffer.Count;
                Console.SetCursorPosition(_inputStartCol + cursorPos, _inputRow);
                continue;
            }

            // Backspace
            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursorPos > 0)
                {
                    buffer.RemoveAt(cursorPos - 1);
                    cursorPos--;
                    RenderLine(buffer, cursorPos);
                    UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered);
                }
                continue;
            }

            // Delete
            if (key.Key == ConsoleKey.Delete)
            {
                if (cursorPos < buffer.Count)
                {
                    buffer.RemoveAt(cursorPos);
                    RenderLine(buffer, cursorPos);
                    UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered);
                }
                continue;
            }

            // Ctrl+U — clear line
            if (key is { Key: ConsoleKey.U, Modifiers: ConsoleModifiers.Control })
            {
                buffer.Clear();
                cursorPos = 0;
                RenderLine(buffer, cursorPos);
                ClearSuggestions(suggestionLinesRendered);
                suggestionLinesRendered = 0;
                showingSuggestions = false;
                selectedSuggestion = -1;
                RenderBottomBorder();
                continue;
            }

            // Regular character
            if (key.KeyChar >= 32)
            {
                buffer.Insert(cursorPos, key.KeyChar);
                cursorPos++;
                RenderLine(buffer, cursorPos);
                UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered);
            }
        }
    }

    /// <summary>Redraws the input text (or placeholder when empty) using absolute positioning.</summary>
    private void RenderLine(List<char> buffer, int cursorPos)
    {
        Console.SetCursorPosition(_inputStartCol, _inputRow);
        // Clear to end of available line space
        var clearLen = Console.WindowWidth - _inputStartCol - 1;
        Console.Write(new string(' ', clearLen));
        Console.SetCursorPosition(_inputStartCol, _inputRow);

        if (buffer.Count == 0)
        {
            AnsiConsole.Markup($"[dim grey]{Placeholder}[/]");
            Console.SetCursorPosition(_inputStartCol, _inputRow);
        }
        else
        {
            Console.Write(new string(buffer.ToArray()));
            Console.SetCursorPosition(_inputStartCol + cursorPos, _inputRow);
        }
    }

    private void UpdateSuggestions(List<char> buffer, ref List<SlashCommand> suggestions,
        ref bool showingSuggestions, ref int selectedSuggestion, ref int suggestionLinesRendered)
    {
        var text = new string(buffer.ToArray());

        if (buffer.Count > 0 && buffer[0] == '/')
        {
            suggestions = _commands.Where(c =>
                c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase)).ToList();

            if (suggestions.Count > 0)
            {
                showingSuggestions = true;
                if (selectedSuggestion >= suggestions.Count)
                    selectedSuggestion = suggestions.Count - 1;
                if (selectedSuggestion < 0)
                    selectedSuggestion = -1;
                RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered);
            }
            else
            {
                ClearSuggestions(suggestionLinesRendered);
                suggestionLinesRendered = 0;
                showingSuggestions = false;
                selectedSuggestion = -1;
                RenderBottomBorder();
            }
        }
        else
        {
            if (showingSuggestions)
            {
                ClearSuggestions(suggestionLinesRendered);
                suggestionLinesRendered = 0;
                RenderBottomBorder();
            }
            showingSuggestions = false;
            selectedSuggestion = -1;
            suggestions.Clear();
        }
    }

    private void RenderSuggestions(List<SlashCommand> suggestions, int selected, ref int previousLines)
    {
        // Save cursor position (should be on input row)
        var savedLeft = Console.CursorLeft;

        // Clear previous suggestion lines
        ClearSuggestions(previousLines, restore: false);

        // Compute column widths from actual content
        var nameW = Math.Max(12, suggestions.Max(c => c.Name.Length));
        var descW = Math.Max(20, suggestions.Max(c => c.Description.Length));
        var innerW = 2 + nameW + 1 + descW + 2;

        var startTop = _inputRow + 1;

        // Ensure enough room — scroll if near bottom
        var neededLines = suggestions.Count + 2;
        while (startTop + neededLines >= Console.BufferHeight)
        {
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.WriteLine();
            _inputRow--;
            startTop = _inputRow + 1;
        }

        Console.SetCursorPosition(_inputStartCol, startTop);

        // Top border
        var header = "─ Commands ";
        AnsiConsole.Markup($"[dim grey]╭{header}{new string('─', innerW - header.Length)}╮[/]");

        for (var i = 0; i < suggestions.Count; i++)
        {
            var cmd = suggestions[i];
            var name = cmd.Name.PadRight(nameW);
            var desc = Spectre.Console.Markup.Escape(cmd.Description).PadRight(descW);
            Console.SetCursorPosition(_inputStartCol, startTop + 1 + i);

            if (i == selected)
                AnsiConsole.Markup($"[dim grey]│[/][on grey23]  [bold white]{name}[/] [dim]{desc}[/]  [/][dim grey]│[/]");
            else
                AnsiConsole.Markup($"[dim grey]│[/]  [yellow]{name}[/] [dim]{desc}[/]  [dim grey]│[/]");
        }

        // Bottom border
        Console.SetCursorPosition(_inputStartCol, startTop + 1 + suggestions.Count);
        AnsiConsole.Markup($"[dim grey]╰{new string('─', innerW)}╯[/]");

        previousLines = neededLines;

        // Restore cursor to input row
        Console.SetCursorPosition(savedLeft, _inputRow);
    }

    private void ClearSuggestions(int lineCount, bool restore = true)
    {
        if (lineCount == 0) return;

        var savedLeft = Console.CursorLeft;

        for (var i = 1; i <= lineCount; i++)
        {
            var y = _inputRow + i;
            if (y >= Console.BufferHeight) break;
            Console.SetCursorPosition(0, y);
            Console.Write(new string(' ', Console.WindowWidth - 1));
        }

        if (restore)
            Console.SetCursorPosition(savedLeft, _inputRow);
    }

    private void RenderBottomBorder()
    {
        var savedLeft = Console.CursorLeft;
        var savedTop = Console.CursorTop;

        // Ensure room for border line
        if (_inputRow + 1 >= Console.BufferHeight)
        {
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.WriteLine();
            _inputRow--;
            savedTop--;
        }

        Console.SetCursorPosition(0, _inputRow + 1);
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, _inputRow + 1);
        AnsiConsole.Markup($"[{BorderColor}]{new string('─', _borderWidth)}[/]");
        Console.SetCursorPosition(savedLeft, savedTop);
    }
}
