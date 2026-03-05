namespace Pebbles.UI;

using Spectre.Console;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Fonts;

/// <summary>
/// Handles all chat UI rendering using Spectre.Console.
/// </summary>
public class ChatRenderer : IChatRenderer
{
    public void RenderWelcome(ChatSession session)
    {
        AnsiConsole.Clear();

        var banner = new FigletText(FigletFontLoader.Slant, "Pebbles")
            .Color(Color.MediumPurple1);
        AnsiConsole.Write(banner);

        var panel = new Panel(
            new Markup(
                "[dim]Your AI coding assistant in the terminal[/]\n" +
                $"[dim]Model:[/] [bold mediumspringgreen]{session.Model}[/]  " +
                $"[dim]Session:[/] [bold]{session.Id}[/]\n\n" +
                "[dim]Type a message to chat, or[/] [bold yellow]/help[/] [dim]for commands.[/]\n" +
                "[dim]Press[/] [bold yellow]Ctrl+C[/] [dim]or type[/] [bold yellow]/exit[/] [dim]to quit.[/]"))
        {
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1),
            BorderStyle = new Style(Color.Grey)
        };
        AnsiConsole.Write(panel);
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

    public async Task RenderThinkingAsync(IAIProvider provider, MockResponse response)
    {
        var thinkingContent = "";

        AnsiConsole.Markup("[bold yellow]⟡ Thinking[/]");
        AnsiConsole.Markup("[dim] ...[/]");
        AnsiConsole.WriteLine();

        await foreach (var chunk in provider.StreamThinkingAsync(response))
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

        await foreach (var chunk in provider.StreamContentAsync(response))
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

    private void RenderMarkdownLine(string line, ref bool inCodeBlock, ref bool inTable)
    {
        if (line.TrimStart().StartsWith("```"))
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
            if (line.TrimStart().StartsWith("|--") || line.TrimStart().StartsWith("| --") || line.TrimStart().StartsWith("|---"))
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

        if (line.TrimStart().StartsWith("# "))
        {
            AnsiConsole.Markup($"  [bold underline mediumpurple1]{Markup.Escape(line.TrimStart()[2..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("## "))
        {
            AnsiConsole.Markup($"  [bold mediumspringgreen]{Markup.Escape(line.TrimStart()[3..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("### "))
        {
            AnsiConsole.Markup($"  [bold yellow]{Markup.Escape(line.TrimStart()[4..])}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("> "))
        {
            var text = ApplyInlineFormatting(line.TrimStart()[2..]);
            AnsiConsole.Markup($"  [yellow]│[/] [italic]{text}[/]");
            return;
        }
        if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
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
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+)`", "[bold grey93 on grey23]$1[/]");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*([^*]+)\*\*", "[bold]$1[/]");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\*)\*([^*]+)\*(?!\*)", "[italic]$1[/]");
        return text;
    }

    public void RenderCommandResult(CommandResult result, ChatSession session)
    {
        AnsiConsole.WriteLine();
        if (result.ShouldClear) { AnsiConsole.Clear(); RenderStatusBar(session); return; }
        if (result.Message is not null)
        {
            var color = result.Success ? "dim" : "red";
            var icon = result.Success ? "●" : "✖";
            AnsiConsole.MarkupLine($"[{color}]{icon} {Markup.Escape(result.Message)}[/]");
        }
        AnsiConsole.WriteLine();
    }

    private static void RenderStatusBar(ChatSession session)
    {
        var rule = new Rule($"[dim]{session.Model} • Session {session.Id} • {session.Messages.Count} messages • {session.TotalInputTokens + session.TotalOutputTokens:N0} tokens[/]")
        {
            Style = Style.Parse("dim grey"),
            Justification = Justify.Center
        };
        AnsiConsole.Write(rule);
        AnsiConsole.WriteLine();
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
        RenderAssistantLiveStreamAsync(IAIProvider provider, string userInput, bool compactMode)
    {
        AnsiConsole.Markup("[bold mediumspringgreen]⬡ Pebbles[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        var content = new System.Text.StringBuilder();
        var tokenCount = 0;
        var currentLine = new System.Text.StringBuilder();
        var inCodeBlock = false;

        await foreach (var token in provider.StreamResponseAsync(userInput))
        {
            tokenCount++;
            if (string.IsNullOrEmpty(token)) continue;

            content.Append(token);
            
            foreach (var c in token)
            {
                if (c == '\n')
                {
                    var lineText = currentLine.ToString();
                    currentLine.Clear();
                    
                    if (lineText.TrimStart().StartsWith("```"))
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
        return (content.ToString(), string.Empty, TimeSpan.Zero, outputTokens);
    }
    
    private static string FormatMarkdownLine(string line)
    {
        var indent = "  ";
        if (line.TrimStart().StartsWith("# "))
            return $"{indent}[bold underline mediumpurple1]{Markup.Escape(line.TrimStart()[2..])}[/]";
        if (line.TrimStart().StartsWith("## "))
            return $"{indent}[bold mediumspringgreen]{Markup.Escape(line.TrimStart()[3..])}[/]";
        if (line.TrimStart().StartsWith("### "))
            return $"{indent}[bold yellow]{Markup.Escape(line.TrimStart()[4..])}[/]";
        if (line.TrimStart().StartsWith("> "))
            return $"{indent}[yellow]│[/] [italic]{ApplyInlineFormatting(line.TrimStart()[2..])}[/]";
        if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
            return $"{indent}[mediumspringgreen]●[/] {ApplyInlineFormatting(line.TrimStart()[2..])}";
        if (line.TrimStart().Length > 2 && char.IsDigit(line.TrimStart()[0]) && line.TrimStart()[1] == '.')
            return $"{indent}[mediumspringgreen]{line.TrimStart()[0]}.[/] {ApplyInlineFormatting(line.TrimStart()[3..])}";
        return $"{indent}{ApplyInlineFormatting(Markup.Escape(line))}";
    }
}