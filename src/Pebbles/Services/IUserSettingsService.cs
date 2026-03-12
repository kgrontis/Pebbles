namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Service for managing user settings persisted across sessions.
/// </summary>
internal interface IUserSettingsService
{
    /// <summary>
    /// Gets the current user settings.
    /// </summary>
    UserSettings Settings { get; }

    /// <summary>
    /// Saves the current settings to persistent storage.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Gets the API key for the specified provider.
    /// First checks environment variables, then stored settings.
    /// </summary>
    string? GetApiKey(string provider);

    /// <summary>
    /// Sets the API key for the specified provider.
    /// Stores it as an environment variable.
    /// </summary>
    void SetApiKey(string provider, string apiKey);

    /// <summary>
    /// Sets the current provider and saves settings.
    /// </summary>
    Task SetProviderAsync(string provider);

    /// <summary>
    /// Checks if the current provider has a valid API key configured.
    /// </summary>
    bool HasValidApiKey();
}