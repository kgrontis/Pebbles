namespace Pebbles.Models;

/// <summary>
/// Persistent storage for discovered models per provider.
/// Stored in ~/.pebbles/models.json
/// </summary>
public sealed class ModelsSettings
{
    /// <summary>
    /// The provider these models belong to.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// List of available model names.
    /// </summary>
    public List<string> Models { get; set; } = [];

    /// <summary>
    /// When the models were last fetched from the API.
    /// </summary>
    public DateTime LastFetched { get; set; }

    /// <summary>
    /// The user's default model selection.
    /// </summary>
    public string? DefaultModel { get; set; }
}
