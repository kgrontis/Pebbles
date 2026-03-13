namespace Pebbles.UI;

using Spectre.Console;
using Pebbles.Fonts;
using Pebbles.Models;
using Pebbles.Services;

/// <summary>
/// Handles all chat UI rendering using Spectre.Console.
/// </summary>
internal partial class ChatRenderer : IChatRenderer
{
    private bool _inTable;

    public void RenderWelcome(ChatSession session)
    {
        AnsiConsole.Clear();

        // Render FIGlet logo
        var figlet = new FigletText(FigletFontLoader.Slant, "Pebbles")
        {
            Color = Color.MediumSpringGreen
        };
        AnsiConsole.Write(figlet);

        AnsiConsole.MarkupLine("[dim]— AI coding assistant in your terminal[/]");
        AnsiConsole.WriteLine();

        // Status line
        AnsiConsole.MarkupLine($"  [dim]Model:[/] [bold mediumspringgreen]{session.Model}[/]  [dim]Session:[/] [bold]{session.Id}[/]");
        AnsiConsole.WriteLine();

        // Help hints
        AnsiConsole.MarkupLine($"  [dim]Type a message to chat, or[/] [bold yellow]/help[/] [dim]for commands.[/]");
        AnsiConsole.MarkupLine($"  [dim]Press[/] [bold yellow]Ctrl+C[/] [dim]or type[/] [bold yellow]/exit[/] [dim]to quit.[/]");

        // Bottom border
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  [dim grey]{new string('─', Math.Max(0, Console.WindowWidth - 12))}[/]");
        AnsiConsole.WriteLine();
    }

    public void RenderUserMessage(string message)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Markup("[bold dodgerblue2]❯ You[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        foreach (var line in message.Split('\n'))
            AnsiConsole.MarkupLine($"  {Markup.Escape(line)}");

        AnsiConsole.WriteLine();
    }

    public void RenderAssistantMessage(string content, ThinkingBlock? thinking = null)
    {
        // Render thinking block if present
        if (thinking is not null && !string.IsNullOrEmpty(thinking.Content))
        {
            AnsiConsole.Markup("[bold yellow]⟡ Thinking[/]");
            AnsiConsole.Markup("[dim] ...[/]");
            AnsiConsole.WriteLine();

            var thinkingText = Markup.Escape(thinking.Content.Trim());
            var thinkingPanel = new Panel(new Markup(thinkingText))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Yellow),
                Padding = new Padding(1, 0),
                Header = new PanelHeader($"[yellow] Thinking ({thinking.Duration.TotalSeconds:F1}s) [/]")
            };

            AnsiConsole.Write(thinkingPanel);
            AnsiConsole.WriteLine();
        }

        // Render assistant label
        AnsiConsole.Markup("[bold mediumspringgreen]⬡ Pebbles[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        // Render content with markdown formatting
        var inCodeBlock = false;
        var currentLine = new System.Text.StringBuilder();

        foreach (var c in content)
        {
            if (c == '\n')
            {
                var lineText = currentLine.ToString();
                currentLine.Clear();
                RenderMarkdownLine(lineText, ref inCodeBlock, ref _inTable);
                AnsiConsole.WriteLine();
            }
            else
            {
                currentLine.Append(c);
            }
        }

        if (currentLine.Length > 0)
        {
            RenderMarkdownLine(currentLine.ToString(), ref inCodeBlock, ref _inTable);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine();
    }

    public async Task RenderThinkingAsync(IAIProvider provider, MockResponse response)
    {
        var thinkingContent = "";

        AnsiConsole.Markup("[bold yellow]⟡ Thinking[/]");
        AnsiConsole.Markup("[dim] ...[/]");
        AnsiConsole.WriteLine();

        await foreach (var chunk in provider.StreamThinkingAsync(response).ConfigureAwait(false))
        {
            thinkingContent += chunk;
        }

        var thinkingText = Markup.Escape(thinkingContent.Trim());
        var thinkingPanel = new Panel(new Markup(thinkingText))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 0),
            Header = new PanelHeader($"[yellow] Thinking ({response.ThinkingDuration.TotalSeconds:F1}s) [/]")
        };

        AnsiConsole.Cursor.MoveUp(1);
        AnsiConsole.Write("\r" + new string(' ', Console.WindowWidth) + "\r");
        AnsiConsole.Write(thinkingPanel);
        AnsiConsole.WriteLine();
    }

    public async Task RenderAssistantStreamAsync(IAIProvider provider, MockResponse response)
    {
        AnsiConsole.Markup("[bold mediumspringgreen]⬡ Pebbles[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        var inCodeBlock = false;
        var inTable = false;
        var currentLine = "";

        await foreach (var chunk in provider.StreamContentAsync(response).ConfigureAwait(false))
        {
            foreach (var c in chunk)
            {
                currentLine += c;
                if (c == '\n')
                {
                    RenderMarkdownLine(currentLine.TrimEnd('\n'), ref inCodeBlock, ref inTable);
                    AnsiConsole.WriteLine();
                    currentLine = "";
                }
            }
        }

        if (currentLine.Length > 0)
        {
            RenderMarkdownLine(currentLine, ref inCodeBlock, ref inTable);
            AnsiConsole.WriteLine();
        }
        AnsiConsole.WriteLine();
    }

    private static void RenderMarkdownLine(string line, ref bool inCodeBlock, ref bool inTable)
    {
        if (line.TrimStart().StartsWith("```", StringComparison.InvariantCultureIgnoreCase))
        {
            inCodeBlock = !inCodeBlock;
            if (inCodeBlock)
            {
                var lang = line.TrimStart().Length > 3 ? line.TrimStart()[3..].Trim() : "";
                var label = string.IsNullOrEmpty(lang) ? "code" : lang;
                AnsiConsole.Markup($"  [dim grey]┌─ {Markup.Escape(label)} ──────────────────────────────────[/]");
            }
            else
            {
                AnsiConsole.Markup("  [dim grey]└──────────────────────────────────────────[/]");
            }
            return;
        }

        if (inCodeBlock)
        {
            AnsiConsole.Markup($"  [dim grey]│[/] [grey93]{Markup.Escape(line)}[/]");
            return;
        }

        if (line.TrimStart().StartsWith('|'))
        {
            if (line.TrimStart().StartsWith("|--", StringComparison.InvariantCultureIgnoreCase) || line.TrimStart().StartsWith("| --", StringComparison.InvariantCultureIgnoreCase) || line.TrimStart().StartsWith("|---", StringComparison.InvariantCultureIgnoreCase))
            {
                AnsiConsole.Markup($"  [dim]{Markup.Escape(line)}[/]");
                return;
            }
            var escaped = Markup.Escape(line);
            if (!inTable) { AnsiConsole.Markup($"  [bold]{escaped}[/]"); inTable = true; }
            else { AnsiConsole.Markup($"  {escaped}"); }
            return;
        }

        if (inTable && !line.TrimStart().StartsWith('|')) inTable = false;

        if (line.TrimStart().StartsWith("# ", StringComparison.InvariantCultureIgnoreCase))
        {
            AnsiConsole.Markup($"  [bold underline mediumpurple1]{Markup.Escape(line.TrimStart()[2..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("## ", StringComparison.InvariantCultureIgnoreCase))
        {
            AnsiConsole.Markup($"  [bold mediumspringgreen]{Markup.Escape(line.TrimStart()[3..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("### ", StringComparison.InvariantCultureIgnoreCase))
        {
            AnsiConsole.Markup($"  [bold yellow]{Markup.Escape(line.TrimStart()[4..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("> ", StringComparison.InvariantCultureIgnoreCase))
        {
            var text = ApplyInlineFormatting(line.TrimStart()[2..]);
            AnsiConsole.Markup($"  [yellow]│[/] [italic]{text}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("- ", StringComparison.InvariantCultureIgnoreCase) || line.TrimStart().StartsWith("* ", StringComparison.InvariantCultureIgnoreCase))
        {
            var text = ApplyInlineFormatting(line.TrimStart()[2..]);
            AnsiConsole.Markup($"  [mediumspringgreen]●[/] {text}");
            return;
        }
        if (line.TrimStart().Length > 2 && char.IsDigit(line.TrimStart()[0]) && line.TrimStart()[1] == '.')
        {
            var num = line.TrimStart()[0];
            var text = ApplyInlineFormatting(line.TrimStart()[3..]);
            AnsiConsole.Markup($"  [mediumspringgreen]{num}.[/] {text}");
            return;
        }

        var formatted = ApplyInlineFormatting(Markup.Escape(line));
        AnsiConsole.Markup($"  {formatted}");
    }

    private static string ApplyInlineFormatting(string text)
    {
        text = InlineCodeRegex().Replace(text, "[bold grey93 on grey23]$1[/]");
        text = BoldRegex().Replace(text, "[bold]$1[/]");
        text = ItalicRegex().Replace(text, "[italic]$1[/]");
        return text;
    }

    public void RenderCommandResult(CommandResult result, ChatSession session)
    {
        AnsiConsole.WriteLine();
        if (result.ShouldClear) { AnsiConsole.Clear(); RenderWelcome(session); return; }
        if (result.Message is not null)
        {
            if (result.RawOutput)
            {
                // Raw output - no prefix, just the message on its own line
                AnsiConsole.Markup(result.Message + "\n");
            }
            else
            {
                // Standard output with status indicator
                var color = result.Success ? "dim" : "red";
                var icon = result.Success ? "●" : "✖";
                
                if (result.AllowMarkup)
                {
                    // Message intentionally contains markup - try to render it
                    try
                    {
                        AnsiConsole.MarkupLine($"[{color}]{icon}[/] {result.Message}");
                    }
                    catch (InvalidOperationException)
                    {
                        // Markup parsing failed - escape the message to be safe
                        AnsiConsole.MarkupLine($"[{color}]{icon}[/] {Markup.Escape(result.Message)}");
                    }
                }
                else
                {
                    // Message is plain text - always escape
                    AnsiConsole.MarkupLine($"[{color}]{icon}[/] {Markup.Escape(result.Message)}");
                }
            }
        }
    }

    public void RenderStatusBar(ChatSession session)
    {
        // Just print the status bar - don't try to overwrite
        // The InputHandler will handle clearing when it shows the input
        var totalTokens = session.TotalInputTokens + session.TotalOutputTokens;
        var statusBar = $"[bold mediumspringgreen]{session.Model}[/] [dim]•[/] [dim]Session[/] [bold]{session.Id}[/] [dim]•[/] [dim]{session.Messages.Count} msgs • {totalTokens:N0} tokens[/]";
        
        AnsiConsole.MarkupLine($"  {statusBar}");
        AnsiConsole.MarkupLine($"  [dim grey]{new string('─', Math.Max(0, Console.WindowWidth - 4))}[/]");
    }

    public void RenderTokenInfo(int inputTokens, int outputTokens, ChatSession session)
    {
        session.TotalInputTokens += inputTokens;
        session.TotalOutputTokens += outputTokens;
        AnsiConsole.MarkupLine($"  [dim grey]⎯ {inputTokens:N0} input → {outputTokens:N0} output • ${session.TotalCost:F4} • {session.TotalInputTokens + session.TotalOutputTokens:N0} total tokens[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders an assistant response with live API streaming.
    /// </summary>
    public async Task<(string Content, string Thinking, TimeSpan ThinkingDuration, int OutputTokens)> 
        RenderAssistantLiveStreamAsync(IAIProvider provider, string userInput)
    {
        AnsiConsole.Markup("[bold mediumspringgreen]⬡ Pebbles[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        var content = new System.Text.StringBuilder();
        var thinking = new System.Text.StringBuilder();
        var tokenCount = 0;
        var thinkingTokenCount = 0;

        var currentLine = new System.Text.StringBuilder();
        var currentThinkingLine = new System.Text.StringBuilder();
        var inCodeBlock = false;
        var inThinkingBlock = false;
        var thinkingStarted = false;

        await foreach (var token in provider.StreamResponseAsync(userInput).ConfigureAwait(false))
        {
            tokenCount++;
            if (string.IsNullOrEmpty(token)) continue;

            // Check if this is thinking content
            if (token.StartsWith("[THINKING]", StringComparison.InvariantCultureIgnoreCase))
            {
                var thinkingToken = token[10..]; // Remove [THINKING] prefix
                thinking.Append(thinkingToken);
                thinkingTokenCount++;
                
                if (!thinkingStarted)
                {
                    thinkingStarted = true;
                    inThinkingBlock = true;
                    // Start thinking panel
                    AnsiConsole.MarkupLine("  [dim grey]┌─ thinking ───────────────────────────────[/]");
                }
                
                // Render thinking content with styling
                foreach (var c in thinkingToken)
                {
                    if (c == '\n')
                    {
                        var lineText = currentThinkingLine.ToString();
                        currentThinkingLine.Clear();
                        AnsiConsole.MarkupLine($"  [dim grey]│[/] [italic grey]{Markup.Escape(lineText)}[/]");
                    }
                    else
                    {
                        currentThinkingLine.Append(c);
                    }
                }
                continue;
            }

            // Regular content
            if (inThinkingBlock)
            {
                // End thinking block
                if (currentThinkingLine.Length > 0)
                {
                    AnsiConsole.MarkupLine($"  [dim grey]│[/] [italic grey]{Markup.Escape(currentThinkingLine.ToString())}[/]");
                    currentThinkingLine.Clear();
                }
                AnsiConsole.MarkupLine("  [dim grey]└──────────────────────────────────────────[/]");
                AnsiConsole.WriteLine();
                inThinkingBlock = false;
            }

            content.Append(token);
            
            foreach (var c in token)
            {
                if (c == '\n')
                {
                    var lineText = currentLine.ToString();
                    currentLine.Clear();
                    
                    if (lineText.TrimStart().StartsWith("```", StringComparison.InvariantCultureIgnoreCase))
                    {
                        inCodeBlock = !inCodeBlock;
                        if (inCodeBlock)
                        {
                            var lang = lineText.TrimStart().Length > 3 ? lineText.TrimStart()[3..].Trim() : "";
                            var label = string.IsNullOrEmpty(lang) ? "code" : lang;
                            AnsiConsole.MarkupLine($"  [dim grey]┌─ {Markup.Escape(label)} ──────────────────────────────────[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("  [dim grey]└──────────────────────────────────────────[/]");
                        }
                    }
                    else if (inCodeBlock)
                    {
                        AnsiConsole.MarkupLine($"  [dim grey]│[/] [grey93]{Markup.Escape(lineText)}[/]");
                    }
                    else
                    {
                        var formatted = FormatMarkdownLine(lineText);
                        AnsiConsole.MarkupLine(formatted);
                    }
                }
                else
                {
                    currentLine.Append(c);
                }
            }
        }

        // Close any open blocks
        if (inThinkingBlock)
        {
            if (currentThinkingLine.Length > 0)
            {
                AnsiConsole.MarkupLine($"  [dim grey]│[/] [italic grey]{Markup.Escape(currentThinkingLine.ToString())}[/]");
            }
            AnsiConsole.MarkupLine("  [dim grey]└──────────────────────────────────────────[/]");
            AnsiConsole.WriteLine();
        }

        if (currentLine.Length > 0)
        {
            if (inCodeBlock)
                AnsiConsole.MarkupLine($"  [dim grey]│[/] [grey93]{Markup.Escape(currentLine.ToString())}[/]");
            else
                AnsiConsole.MarkupLine(FormatMarkdownLine(currentLine.ToString()));
        }

        AnsiConsole.WriteLine();
        
        // Ensure cursor is at a clean position for next input
        try
        {
            if (Console.CursorTop < Console.BufferHeight - 5)
                AnsiConsole.WriteLine();
        }
        catch (IOException) { }
        
        var outputTokens = (int)Math.Ceiling(tokenCount * 0.5);
        var thinkingDuration = thinkingTokenCount > 0 ? TimeSpan.FromMilliseconds(thinkingTokenCount * 50) : TimeSpan.Zero;
        return (content.ToString(), thinking.ToString(), thinkingDuration, outputTokens);
    }
    
    private static string FormatMarkdownLine(string line)
    {
        var indent = "  ";
        if (line.TrimStart().StartsWith("# ", StringComparison.InvariantCultureIgnoreCase))
            return $"{indent}[bold underline mediumpurple1]{Markup.Escape(line.TrimStart()[2..])}[/]";
        if (line.TrimStart().StartsWith("## ", StringComparison.InvariantCultureIgnoreCase))
            return $"{indent}[bold mediumspringgreen]{Markup.Escape(line.TrimStart()[3..])}[/]";
        if (line.TrimStart().StartsWith("### ", StringComparison.InvariantCultureIgnoreCase))
            return $"{indent}[bold yellow]{Markup.Escape(line.TrimStart()[4..])}[/]";
        if (line.TrimStart().StartsWith("> ", StringComparison.InvariantCultureIgnoreCase))
            return $"{indent}[yellow]│[/] [italic]{ApplyInlineFormatting(line.TrimStart()[2..])}[/]";
        if (line.TrimStart().StartsWith("- ", StringComparison.InvariantCultureIgnoreCase) || line.TrimStart().StartsWith("* ", StringComparison.InvariantCultureIgnoreCase))
            return $"{indent}[mediumspringgreen]●[/] {ApplyInlineFormatting(line.TrimStart()[2..])}";
        if (line.TrimStart().Length > 2 && char.IsDigit(line.TrimStart()[0]) && line.TrimStart()[1] == '.')
            return $"{indent}[mediumspringgreen]{line.TrimStart()[0]}.[/] {ApplyInlineFormatting(line.TrimStart()[3..])}";
        return $"{indent}{ApplyInlineFormatting(Markup.Escape(line))}";
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"`([^`]+)`")]
    private static partial System.Text.RegularExpressions.Regex InlineCodeRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial System.Text.RegularExpressions.Regex BoldRegex();
    [System.Text.RegularExpressions.GeneratedRegex(@"(?<!\*)\*([^*]+)\*(?!\*)")]
    private static partial System.Text.RegularExpressions.Regex ItalicRegex();
}