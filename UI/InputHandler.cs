namespace Pebbles.UI;

using Spectre.Console;
using Pebbles.Models;
using Pebbles.Services;

/// <summary>
/// Handles user input with history, command autocomplete, and file picker.
/// </summary>
public class InputHandler : IInputHandler
{
    private readonly ICommandHandler _commandHandler;
    private readonly IFileService _fileService;
    private readonly List<string> _inputHistory = [];
    private int _historyIndex = -1;
    private int _inputStartCol;
    private int _inputRow;
    private int _borderWidth;
    private const string Placeholder = "Type a message, / for commands, @ for files";
    private const string BorderColor = "dodgerblue2";

    public InputHandler(ICommandHandler commandHandler, IFileService fileService)
    {
        _commandHandler = commandHandler;
        _fileService = fileService;
    }

    public string? ReadInput(ChatSession session)
    {
        // Ensure we have room in the buffer
        EnsureBufferSpace(14);

        // Status bar (model, session, tokens)
        var totalTokens = session.TotalInputTokens + session.TotalOutputTokens;
        AnsiConsole.MarkupLine($"  [bold mediumspringgreen]{session.Model}[/] [dim]•[/] [dim]Session[/] [bold]{session.Id}[/] [dim]•[/] [dim]{session.Messages.Count} msgs • {totalTokens:N0} tokens[/]");
        
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
        var suggestions = new List<ISuggestion>();
        var showingSuggestions = false;
        var suggestionLinesRendered = 0;
        var autocompleteType = AutocompleteType.None;
        var filePickerPath = ""; // Current directory for file picker

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
                Console.SetCursorPosition(0, _inputRow);
                Console.Write(new string(' ', Console.WindowWidth));
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]Goodbye! 👋[/]");
                Environment.Exit(0);
            }

            // Enter — accept suggestion or submit
            if (key.Key == ConsoleKey.Enter)
            {
                // If suggestions are showing, accept the selected one
                if (showingSuggestions && suggestions.Count > 0 && selectedSuggestion >= 0)
                {
                    AcceptSuggestion(buffer, suggestions[selectedSuggestion], ref cursorPos, autocompleteType, filePickerPath);
                    RenderLine(buffer, cursorPos);

                    // If we selected a directory, update the file picker
                    if (suggestions[selectedSuggestion].IsDirectory)
                    {
                        var path = suggestions[selectedSuggestion].InsertText;
                        filePickerPath = path.TrimEnd('/');
                        UpdateFileSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref filePickerPath);
                    }
                    else
                    {
                        // Close suggestions after selecting
                        ClearSuggestions(suggestionLinesRendered);
                        suggestionLinesRendered = 0;
                        showingSuggestions = false;
                        selectedSuggestion = -1;
                        autocompleteType = AutocompleteType.None;
                        RenderBottomBorder();
                    }
                    continue;
                }

                // No suggestions showing - submit input
                ClearSuggestions(suggestionLinesRendered);
                // Clear the input area
                Console.SetCursorPosition(0, _inputRow - 1);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, _inputRow);
                Console.Write(new string(' ', Console.WindowWidth));

                var result = new string(buffer.ToArray());
                if (!string.IsNullOrWhiteSpace(result))
                {
                    _inputHistory.Add(result);
                    _historyIndex = _inputHistory.Count;
                }
                return result;
            }

            // Tab — accept suggestion
            if (key.Key == ConsoleKey.Tab)
            {
                if (showingSuggestions && suggestions.Count > 0 && selectedSuggestion >= 0)
                {
                    AcceptSuggestion(buffer, suggestions[selectedSuggestion], ref cursorPos, autocompleteType, filePickerPath);
                    RenderLine(buffer, cursorPos);

                    // If we selected a directory, update the file picker
                    if (suggestions[selectedSuggestion].IsDirectory)
                    {
                        var path = suggestions[selectedSuggestion].InsertText;
                        filePickerPath = path.TrimEnd('/');
                        UpdateFileSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref filePickerPath);
                    }
                    else
                    {
                        // Close suggestions after selecting
                        ClearSuggestions(suggestionLinesRendered);
                        suggestionLinesRendered = 0;
                        showingSuggestions = false;
                        selectedSuggestion = -1;
                        autocompleteType = AutocompleteType.None;
                        RenderBottomBorder();
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
                    autocompleteType = AutocompleteType.None;
                    filePickerPath = "";
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
                    RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered, autocompleteType);
                    continue;
                }

                if (_inputHistory.Count > 0 && _historyIndex > 0)
                {
                    _historyIndex--;
                    buffer.Clear();
                    buffer.AddRange(_inputHistory[_historyIndex]);
                    cursorPos = buffer.Count;
                    RenderLine(buffer, cursorPos);
                    ClearSuggestions(suggestionLinesRendered);
                    showingSuggestions = false;
                }
                continue;
            }

            // Down arrow — suggestion navigation or history
            if (key.Key == ConsoleKey.DownArrow)
            {
                if (showingSuggestions && suggestions.Count > 0)
                {
                    selectedSuggestion = (selectedSuggestion + 1) % suggestions.Count;
                    RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered, autocompleteType);
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
                    SetCursor(cursorPos);
                }
                continue;
            }
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursorPos < buffer.Count)
                {
                    cursorPos++;
                    SetCursor(cursorPos);
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
                SetCursor(cursorPos);
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
                    UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref autocompleteType, ref filePickerPath);
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
                    UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref autocompleteType, ref filePickerPath);
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
                autocompleteType = AutocompleteType.None;
                filePickerPath = "";
                RenderBottomBorder();
                continue;
            }

            // Regular character
            if (key.KeyChar >= 32)
            {
                buffer.Insert(cursorPos, key.KeyChar);
                cursorPos++;
                RenderLine(buffer, cursorPos);
                UpdateSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref autocompleteType, ref filePickerPath);
            }
        }
    }

    private void AcceptSuggestion(List<char> buffer, ISuggestion suggestion, ref int cursorPos, AutocompleteType type, string currentPath)
    {
        // Find the start of the autocomplete trigger
        int startPos = -1;
        if (type == AutocompleteType.Command)
        {
            startPos = 0;
        }
        else if (type == AutocompleteType.File)
        {
            // Find the @ symbol
            for (int i = cursorPos - 1; i >= 0; i--)
            {
                if (buffer[i] == '@')
                {
                    startPos = i;
                    break;
                }
            }
        }

        if (startPos >= 0)
        {
            // Remove the old text
            buffer.RemoveRange(startPos, cursorPos - startPos);
            cursorPos = startPos;

            // Insert the suggestion
            var textToInsert = type == AutocompleteType.File
                ? "@" + suggestion.InsertText
                : suggestion.InsertText;

            buffer.InsertRange(cursorPos, textToInsert);
            cursorPos += textToInsert.Length;
        }
    }

    private void UpdateSuggestions(List<char> buffer, ref List<ISuggestion> suggestions,
        ref bool showingSuggestions, ref int selectedSuggestion, ref int suggestionLinesRendered,
        ref AutocompleteType autocompleteType, ref string filePickerPath)
    {
        var text = new string(buffer.ToArray());
        var cursor = buffer.Count;

        // Detect autocomplete type based on cursor position
        if (buffer.Count > 0 && buffer[0] == '/')
        {
            autocompleteType = AutocompleteType.Command;
            UpdateCommandSuggestions(text, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered);
        }
        else if (IsInFileReference(buffer, cursor, out var atPos, out var currentPath))
        {
            autocompleteType = AutocompleteType.File;
            filePickerPath = currentPath;
            UpdateFileSuggestions(buffer, ref suggestions, ref showingSuggestions, ref selectedSuggestion, ref suggestionLinesRendered, ref filePickerPath);
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
            autocompleteType = AutocompleteType.None;
            suggestions.Clear();
        }
    }

    private bool IsInFileReference(List<char> buffer, int cursor, out int atPos, out string currentPath)
    {
        atPos = -1;
        currentPath = "";

        // Look for @ before cursor
        for (int i = cursor - 1; i >= 0; i--)
        {
            if (buffer[i] == '@')
            {
                atPos = i;
                // Extract path after @
                if (cursor > i + 1)
                {
                    currentPath = new string(buffer.GetRange(i + 1, cursor - i - 1).ToArray());
                }
                return true;
            }
            // Stop at whitespace - file reference can't span words
            if (char.IsWhiteSpace(buffer[i]))
                break;
        }
        return false;
    }

    private void UpdateCommandSuggestions(string text, ref List<ISuggestion> suggestions,
        ref bool showingSuggestions, ref int selectedSuggestion, ref int suggestionLinesRendered)
    {
        var matchingCommands = _commandHandler.Commands
            .Where(c => c.Name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
            .Select(c => (ISuggestion)new CommandSuggestion(c))
            .ToList();

        if (matchingCommands.Count > 0)
        {
            suggestions = matchingCommands;
            showingSuggestions = true;
            selectedSuggestion = 0; // Start with first item selected
            RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered, AutocompleteType.Command);
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

    private void UpdateFileSuggestions(List<char> buffer, ref List<ISuggestion> suggestions,
        ref bool showingSuggestions, ref int selectedSuggestion, ref int suggestionLinesRendered, ref string currentPath)
    {
        // Parse the current path - could be a directory or partial filename
        var dir = "";
        var filter = "";

        if (!string.IsNullOrEmpty(currentPath))
        {
            var lastSlash = currentPath.LastIndexOfAny(new[] { '/', '\\' });
            if (lastSlash >= 0)
            {
                dir = currentPath[..lastSlash];
                filter = currentPath[(lastSlash + 1)..];
            }
            else
            {
                filter = currentPath;
            }
        }

        var items = _fileService.ListDirectory(string.IsNullOrEmpty(dir) ? null : dir, filter);
        suggestions = items.Select(i => (ISuggestion)new FileSuggestion(i)).ToList();

        if (suggestions.Count > 0)
        {
            showingSuggestions = true;
            if (selectedSuggestion >= suggestions.Count)
                selectedSuggestion = suggestions.Count - 1;
            if (selectedSuggestion < 0)
                selectedSuggestion = 0;
            RenderSuggestions(suggestions, selectedSuggestion, ref suggestionLinesRendered, AutocompleteType.File);
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

    private void RenderLine(List<char> buffer, int cursorPos)
    {
        var safeRow = Math.Min(_inputRow, Console.BufferHeight - 1);
        Console.SetCursorPosition(_inputStartCol, safeRow);
        var clearLen = Console.WindowWidth - _inputStartCol - 1;
        Console.Write(new string(' ', clearLen));
        Console.SetCursorPosition(_inputStartCol, safeRow);

        if (buffer.Count == 0)
        {
            AnsiConsole.Markup($"[dim grey]{Placeholder}[/]");
        }
        else
        {
            var text = new string(buffer.ToArray());
            // Highlight @file references
            if (text.Contains('@'))
            {
                RenderWithFileHighlights(text);
            }
            else
            {
                Console.Write(text);
            }
        }
        SetCursor(cursorPos);
    }

    private void RenderWithFileHighlights(string text)
    {
        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '@')
            {
                // Find end of file reference
                var end = i + 1;
                while (end < text.Length && !char.IsWhiteSpace(text[end]))
                    end++;

                var fileRef = text[(i + 1)..end];
                AnsiConsole.Markup($"[bold cyan]@{Markup.Escape(fileRef)}[/]");
                i = end;
            }
            else
            {
                Console.Write(text[i]);
                i++;
            }
        }
    }

    private void SetCursor(int cursorPos)
    {
        Console.SetCursorPosition(
            Math.Min(_inputStartCol + cursorPos, Console.WindowWidth - 1),
            Math.Min(_inputRow, Console.BufferHeight - 1));
    }

    private void RenderSuggestions(List<ISuggestion> suggestions, int selected, ref int previousLines, AutocompleteType type)
    {
        var savedLeft = Console.CursorLeft;
        ClearSuggestions(previousLines, restore: false);

        var nameW = Math.Max(16, suggestions.Max(s => s.DisplayText.Length));
        var descW = Math.Max(12, suggestions.Max(s => s.Description.Length));
        
        // Content line: │ 📁 name          desc    │
        // Width breakdown (emoji = 2 cells):
        // │(1) + space(1) + 📁(2) + space(1) + name(nameW) + space(1) + desc(descW) + space(1) + │(1)
        // Total = 8 + nameW + descW
        // Inner width (between │ chars) = 6 + nameW + descW
        var innerW = 6 + nameW + descW;

        var startTop = _inputRow + 1;
        var maxDisplay = Math.Min(10, suggestions.Count);

        var neededLines = maxDisplay + 2;
        while (startTop + neededLines >= Console.BufferHeight)
        {
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.WriteLine();
            _inputRow--;
            startTop = _inputRow + 1;
        }

        Console.SetCursorPosition(_inputStartCol, startTop);

        // Calculate scroll window
        var scrollOffset = 0;
        if (suggestions.Count > maxDisplay)
        {
            scrollOffset = Math.Max(0, selected - maxDisplay / 2);
            scrollOffset = Math.Min(scrollOffset, suggestions.Count - maxDisplay);
        }

        // Header: ── Files (1-10/45) ───────────
        var title = type == AutocompleteType.File ? "Files" : "Commands";
        var scrollText = suggestions.Count > maxDisplay 
            ? $" ({scrollOffset + 1}-{Math.Min(scrollOffset + maxDisplay, suggestions.Count)}/{suggestions.Count})" 
            : "";
        
        // ─ + title + scrollText + dashes = innerW
        var dashCount = Math.Max(1, innerW - title.Length - scrollText.Length - 2); // -2 for "  " padding
        
        AnsiConsole.Markup($"[dim grey]  {title}{scrollText} {new string('─', dashCount)}[/]");

        for (var i = 0; i < maxDisplay; i++)
        {
            var idx = scrollOffset + i;
            if (idx >= suggestions.Count) break;

            var sug = suggestions[idx];
            var name = sug.DisplayText.PadRight(nameW);
            var desc = Markup.Escape(sug.Description).PadRight(descW);
            Console.SetCursorPosition(_inputStartCol, startTop + 1 + i);

            if (idx == selected)
                AnsiConsole.Markup($"  [on grey23] [bold white]{name}[/] [dim]{desc}[/][/]");
            else
                AnsiConsole.Markup($"  [cyan]{name}[/] [dim]{desc}[/]");
        }

        // Bottom separator
        Console.SetCursorPosition(_inputStartCol, startTop + 1 + maxDisplay);
        AnsiConsole.Markup($"[dim grey]  {new string('─', innerW)}[/]");

        previousLines = maxDisplay + 2;
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
            Console.SetCursorPosition(savedLeft, Math.Min(_inputRow, Console.BufferHeight - 1));
    }

    private void RenderBottomBorder()
    {
        var savedLeft = Console.CursorLeft;
        var savedTop = Console.CursorTop;

        if (_inputRow + 1 >= Console.BufferHeight)
        {
            Console.SetCursorPosition(0, Console.BufferHeight - 1);
            Console.WriteLine();
            _inputRow = Math.Max(0, _inputRow - 1);
            savedTop--;
        }

        Console.SetCursorPosition(0, Math.Min(_inputRow + 1, Console.BufferHeight - 1));
        Console.Write(new string(' ', Console.WindowWidth - 1));
        Console.SetCursorPosition(0, Math.Min(_inputRow + 1, Console.BufferHeight - 1));
        AnsiConsole.Markup($"[{BorderColor}]{new string('─', _borderWidth)}[/]");
        Console.SetCursorPosition(savedLeft, Math.Min(savedTop, Console.BufferHeight - 1));
    }

    private static void EnsureBufferSpace(int linesNeeded)
    {
        try
        {
            if (Console.CursorTop + linesNeeded >= Console.BufferHeight)
            {
                for (var i = 0; i < linesNeeded; i++)
                    Console.WriteLine();
            }
        }
        catch (IOException) { }
    }

    private enum AutocompleteType
    {
        None,
        Command,
        File
    }
}