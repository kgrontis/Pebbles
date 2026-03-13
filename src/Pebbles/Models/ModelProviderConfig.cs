namespace Pebbles.Models;

/// <summary>
/// Configuration for a model provider.
/// Matches qwen-code's ModelProviderConfig structure.
/// </summary>
public sealed class ModelProviderConfig
{
    /// <summary>
    /// The model identifier (e.g., "gpt-5.4", "claude-4-5-sonnet").
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the model.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the model's capabilities.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Environment variable name for the API key.
    /// </summary>
    public string? EnvKey { get; set; }

    /// <summary>
    /// Optional custom base URL for the API.
    /// </summary>
    public Uri? BaseUrl { get; set; }

    /// <summary>
    /// Model capabilities (vision, etc.).
    /// </summary>
    public ModelCapabilities? Capabilities { get; set; }
}

/// <summary>
/// Describes model capabilities.
/// </summary>
public sealed class ModelCapabilities
{
    /// <summary>
    /// Whether the model supports vision/image input.
    /// </summary>
    public bool Vision { get; set; }
}
