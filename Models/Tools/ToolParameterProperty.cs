namespace Pebbles.Models.Tools;

/// <summary>
/// A single parameter property in the schema.
/// </summary>
public record ToolParameterProperty
{
    public string Type { get; init; } = "string";
    public string? Description { get; init; }
    public string? Enum { get; init; }
}
