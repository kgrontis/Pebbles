namespace Pebbles.Models.Tools;

/// <summary>
/// JSON schema for tool parameters.
/// </summary>
public record ToolParameters
{
    public string Type { get; init; } = "object";
    public Dictionary<string, ToolParameterProperty> Properties { get; init; } = [];
    public List<string> Required { get; init; } = [];
}
