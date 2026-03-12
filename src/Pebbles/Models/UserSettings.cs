namespace Pebbles.Models;

/// <summary>
/// User settings stored persistently across sessions.
/// </summary>
internal sealed class UserSettings
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
}