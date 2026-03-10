namespace Pebbles.Models.Tools;

/// <summary>
/// Represents a tool definition sent to the AI model.
/// </summary>
public record ToolDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ToolParameters Parameters { get; init; } = new();
}
