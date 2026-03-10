namespace Pebbles.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a tool definition sent to the AI model (OpenAI-compatible format).
/// </summary>
public class ToolDefinition
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public FunctionDefinition? Function { get; set; }
}

/// <summary>
/// The function definition within a tool.
/// </summary>
public class FunctionDefinition
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
public class ToolParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, ToolParameterProperty> Properties { get; set; } = new();

    [JsonPropertyName("required")]
    public List<string> Required { get; set; } = new();
}

/// <summary>
/// A single parameter property in the schema.
/// </summary>
public class ToolParameterProperty
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
public record ToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = "function";
    public ToolCallFunction Function { get; init; } = new();
}

/// <summary>
/// Function details within a tool call.
/// </summary>
public record ToolCallFunction
{
    public string Name { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty; // JSON string
}

/// <summary>
/// A tool call result to send back to the AI model.
/// </summary>
public record ToolResult
{
    public string ToolCallId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public bool IsError { get; init; }
}

/// <summary>
/// Result of executing a tool.
/// </summary>
public record ToolExecutionResult
{
    public bool Success { get; init; }
    public string Content { get; init; } = string.Empty;
    public string? Error { get; init; }
}
