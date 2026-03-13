namespace Pebbles.Services;

using Pebbles.Models;
using Spectre.Console;
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

            var response = await aiProvider.GetResponseWithToolsAsync(
                input,
                toolDefinitions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            thinking = aiProvider.GetLastThinking();
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