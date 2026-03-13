namespace Pebbles.Models;

/// <summary>
/// User settings stored persistently across sessions.
/// </summary>
public sealed class UserSettings
{
    /// <summary>
    /// The selected AI provider (alibabacloud, openai, anthropic).
    /// </summary>
    public string Provider { get; set; } = "mock";

    /// <summary>
    /// Whether the user has completed initial setup.
    /// </summary>
    public bool SetupCompleted { get; set; }

    /// <summary>
    /// The default model to use for the selected provider.
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// API keys for each provider (key = provider name, value = API key).
    /// </summary>
    public Dictionary<string, string> ApiKeys { get; set; } = new();

    /// <summary>
    /// Model providers configuration (matches qwen-code's modelProviders).
    /// Key is provider name (e.g., "openai", "anthropic", "alibabacloud").
    /// Value is list of model configurations for that provider.
    /// </summary>
    public Dictionary<string, List<ModelProviderConfig>> ModelProviders { get; set; } = new();
}