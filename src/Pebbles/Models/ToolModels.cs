namespace Pebbles.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a tool definition sent to the AI model (OpenAI-compatible format).
/// </summary>
internal class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition? Function { get; set; }
}

/// <summary>
/// The function definition within a tool.
/// </summary>
internal class FunctionDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("parameters")]
    public ToolParameters Parameters { get; set; } = new();
}

/// <summary>
/// JSON schema for tool parameters.
/// </summary>
internal class ToolParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ToolParameterProperty> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = [];
}

/// <summary>
/// A single parameter property in the schema.
/// </summary>
internal class ToolParameterProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("enum")]
    public List<string>? Enum { get; init; }
}

/// <summary>
/// A tool call request from the AI model.
/// </summary>
internal record ToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = "function";
    public ToolCallFunction Function { get; init; } = new();
}

/// <summary>
/// Function details within a tool call.
/// </summary>
internal record ToolCallFunction
{
    public string Name { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty; // JSON string
}

/// <summary>
/// A tool call result to send back to the AI model.
/// </summary>
internal record ToolResult
{
    public string ToolCallId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsError { get; init; }
}

/// <summary>
/// Result of executing a tool.
/// </summary>
internal record ToolExecutionResult
{
    public bool Success { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? Error { get; init; }
}
