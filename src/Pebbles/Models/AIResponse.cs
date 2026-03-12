using System.Collections.ObjectModel;

namespace Pebbles.Models;

/// <summary>
/// Response from AI that may include tool calls.
/// </summary>
internal record AIResponse
{
    public string Content { get; init; } = string.Empty;
    public Collection<ToolCall> ToolCalls { get; init; } = [];
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string? Thinking { get; init; }
}