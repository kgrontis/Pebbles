namespace Pebbles.Services;

using Pebbles.Models;
using Spectre.Console;
using System.Text;
using System.Text.Json;

/// <summary>
/// Service for executing tool calls from AI responses.
/// </summary>
public sealed class ToolExecutionService(IAIProvider aiProvider, IToolRegistry toolRegistry) : IToolExecutionService
{

    /// <inheritdoc />
    public async Task<ChatMessage> ExecuteToolLoopAsync(string input, CancellationToken cancellationToken = default)
    {
        const int maxToolIterations = 5;
        var iteration = 0;
        string? finalContent = null;
        int outputTokens = 0;
        string thinking = string.Empty;
        TimeSpan thinkingDuration = TimeSpan.Zero;

        while (iteration < maxToolIterations)
        {
            iteration++;

            var toolDefinitions = toolRegistry.GetAllToolDefinitions();

            // Use streaming to show thinking in real-time
            var content = new StringBuilder();
            var thinkingContent = new StringBuilder();
            AIResponse? response = null;

            var currentThinkingLine = new StringBuilder();
            var inThinkingBlock = false;
            var thinkingStarted = false;
            var inCodeBlock = false;
            var currentLine = new StringBuilder();

            await foreach (var chunk in aiProvider.StreamResponseWithToolsAsync(
                input,
                toolDefinitions,
                cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (chunk.FinalResponse is not null)
                {
                    response = chunk.FinalResponse;
                    continue;
                }

                if (chunk.Token is null)
                    continue;

                var token = chunk.Token;

                // Check if this is thinking content
                if (token.StartsWith("[THINKING]", StringComparison.InvariantCultureIgnoreCase))
                {
                    var thinkingToken = token[10..]; // Remove [THINKING] prefix
                    thinkingContent.Append(thinkingToken);

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
                            AnsiConsole.MarkupLine(Markup.Escape(lineText));
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
                    AnsiConsole.MarkupLine(Markup.Escape(currentLine.ToString()));
            }

            if (response is null)
            {
                response = new AIResponse
                {
                    Content = content.ToString(),
                    ToolCalls = []
                };
            }

            thinking = thinkingContent.ToString();
            thinkingDuration = aiProvider.GetLastThinkingDuration();
            outputTokens = response.OutputTokens;

            if (response.ToolCalls.Count == 0)
            {
                finalContent = response.Content;
                break;
            }

            var toolResults = await ExecuteToolCallsAsync(response.ToolCalls, cancellationToken).ConfigureAwait(false);

            input = $"Tool results: {JsonSerializer.Serialize(toolResults)}";
        }

        return ChatMessage.Assistant(
            finalContent ?? "Tool execution completed.",
            outputTokens,
            new ThinkingBlock { Content = thinking, Duration = thinkingDuration });
    }

    private async Task<List<ToolResult>> ExecuteToolCallsAsync(
        IReadOnlyList<ToolCall> toolCalls,
        CancellationToken cancellationToken)
    {
        var results = new List<ToolResult>();

        foreach (var toolCall in toolCalls)
        {
            AnsiConsole.MarkupLine($"[dim]🔧 Executing: {toolCall.Function.Name}[/]");
            AnsiConsole.MarkupLine($"[dim]   Arguments: {toolCall.Function.Arguments}[/]");

            var result = await toolRegistry.ExecuteToolAsync(
                toolCall.Function.Name,
                toolCall.Function.Arguments,
                cancellationToken).ConfigureAwait(false);

            results.Add(new ToolResult
            {
                ToolCallId = toolCall.Id,
                Name = toolCall.Function.Name,
                Content = result.Success ? result.Content : result.Error ?? "Unknown error",
                IsError = !result.Success
            });

            AnsiConsole.MarkupLine(
                result.Success
                    ? $"[green]✓ {toolCall.Function.Name} completed[/]"
                    : $"[red]✗ {toolCall.Function.Name} failed: {result.Error}[/]");
        }

        return results;
    }
}