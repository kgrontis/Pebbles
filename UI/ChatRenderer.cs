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

        // Stream thinking content
        await foreach (var chunk in provider.StreamThinkingAsync(response))
        {
            thinkingContent += chunk;
        }

        // Render completed thinking block as a collapsed panel
        var thinkingText = Markup.Escape(thinkingContent.Trim());
        var thinkingPanel = new Panel(new Markup(thinkingText))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(1, 0),
            Header = new PanelHeader(
                $"[yellow] Thinking ({response.ThinkingDuration.TotalSeconds:F1}s) [/]")
        };

        // Move cursor up to overwrite the "Thinking..." line
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

        // Flush remaining content
        if (currentLine.Length > 0)
        {
            RenderMarkdownLine(currentLine, ref inCodeBlock, ref inTable);
            AnsiConsole.WriteLine();
        }

        AnsiConsole.WriteLine();
    }

    private void RenderMarkdownLine(string line, ref bool inCodeBlock, ref bool inTable)
    {
        // Code block toggle
        if (line.TrimStart().StartsWith("```"))
        {
            inCodeBlock = !inCodeBlock;
            if (inCodeBlock)
            {
                var lang = line.TrimStart().Length > 3
                    ? line.TrimStart()[3..].Trim()
                    : "";
                var label = string.IsNullOrEmpty(lang) ? "code" : lang;
                AnsiConsole.Markup($"  [dim grey]┌─ {Markup.Escape(label)} ──────────────────────────────────[/]");
            }
            else
            {
                AnsiConsole.Markup("  [dim grey]└──────────────────────────────────────────[/]");
            }
            return;
        }

        // Inside code block
        if (inCodeBlock)
        {
            AnsiConsole.Markup($"  [dim grey]│[/] [grey93]{Markup.Escape(line)}[/]");
            return;
        }

        // Table lines
        if (line.TrimStart().StartsWith('|'))
        {
            if (line.TrimStart().StartsWith("|--") || line.TrimStart().StartsWith("| --") ||
                line.TrimStart().StartsWith("|---"))
            {
                AnsiConsole.Markup($"  [dim]{Markup.Escape(line)}[/]");
                return;
            }

            var escaped = Markup.Escape(line);
            if (!inTable)
            {
                AnsiConsole.Markup($"  [bold]{escaped}[/]");
                inTable = true;
            }
            else
            {
                AnsiConsole.Markup($"  {escaped}");
            }
            return;
        }

        if (inTable && !line.TrimStart().StartsWith('|'))
            inTable = false;

        // H1
        if (line.TrimStart().StartsWith("# "))
        {
            var text = line.TrimStart()[2..];
            AnsiConsole.Markup($"  [bold underline mediumpurple1]{Markup.Escape(text)}[/]");
            return;
        }

        // H2
        if (line.TrimStart().StartsWith("## "))
        {
            var text = line.TrimStart()[3..];
            AnsiConsole.Markup($"  [bold mediumspringgreen]{Markup.Escape(text)}[/]");
            return;
        }

        // H3
        if (line.TrimStart().StartsWith("### "))
        {
            var text = line.TrimStart()[4..];
            AnsiConsole.Markup($"  [bold yellow]{Markup.Escape(text)}[/]");
            return;
        }

        // Blockquote
        if (line.TrimStart().StartsWith("> "))
        {
            var text = line.TrimStart()[2..];
            text = ApplyInlineFormatting(text);
            AnsiConsole.Markup($"  [yellow]│[/] [italic]{text}[/]");
            return;
        }

        // Unordered list
        if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
        {
            var text = line.TrimStart()[2..];
            text = ApplyInlineFormatting(text);
            AnsiConsole.Markup($"  [mediumspringgreen]●[/] {text}");
            return;
        }

        // Ordered list
        if (line.TrimStart().Length > 2 && char.IsDigit(line.TrimStart()[0]) && line.TrimStart()[1] == '.')
        {
            var num = line.TrimStart()[0];
            var text = line.TrimStart()[3..];
            text = ApplyInlineFormatting(text);
            AnsiConsole.Markup($"  [mediumspringgreen]{num}.[/] {text}");
            return;
        }

        // Regular line
        var formatted = ApplyInlineFormatting(Markup.Escape(line));
        AnsiConsole.Markup($"  {formatted}");
    }

    private static string ApplyInlineFormatting(string text)
    {
        // Inline code `text`
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"`([^`]+)`", "[bold grey93 on grey23]$1[/]");

        // Bold **text**
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*([^*]+)\*\*", "[bold]$1[/]");

        // Italic *text*
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"(?<!\*)\*([^*]+)\*(?!\*)", "[italic]$1[/]");

        return text;
    }

    public void RenderCommandResult(CommandResult result, ChatSession session)
    {
        AnsiConsole.WriteLine();

        if (result.ShouldClear)
        {
            AnsiConsole.Clear();
            RenderStatusBar(session);
            return;
        }

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

        AnsiConsole.MarkupLine(
            $"  [dim grey]⎯ {inputTokens:N0} input → {outputTokens:N0} output • " +
            $"${(inputTokens * 0.003 + outputTokens * 0.015) / 1000.0:F4} • " +
            $"{session.TotalInputTokens + session.TotalOutputTokens:N0} total tokens[/]");
        AnsiConsole.WriteLine();
    }
}