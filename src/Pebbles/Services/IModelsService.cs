namespace Pebbles.Services;

/// <summary>
/// Service for managing discovered models from AI providers.
/// Models are cached in ~/.pebbles/models.json
/// </summary>
public interface IModelsService
{
    /// <summary>
    /// Gets available models for the specified provider.
    /// Fetches from API if not cached or if forceRefresh is true.
    /// </summary>
    /// <param name="provider">The provider name (e.g., "openai", "alibabacloud", "anthropic")</param>
    /// <param name="apiKey">The API key for authentication</param>
    /// <param name="baseUrl">The base URL for the provider API</param>
    /// <param name="forceRefresh">If true, always fetch from API instead of using cache</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of available model names</returns>
    Task<List<string>> GetModelsAsync(string provider, string? apiKey, Uri baseUrl, bool forceRefresh = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets cached models for the current provider without fetching from API.
    /// </summary>
    /// <param name="provider">The provider name</param>
    /// <returns>Cached model list, or empty list if not found</returns>
    List<string> GetCachedModels(string provider);

    /// <summary>
    /// Gets the last time models were fetched for the current provider.
    /// </summary>
    /// <param name="provider">The provider name</param>
    /// <returns>Last fetch time, or DateTime.MinValue if never fetched</returns>
    DateTime GetLastFetchTime(string provider);

    /// <summary>
    /// Gets the default model for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name</param>
    /// <returns>Default model name, or null if not set</returns>
    string? GetDefaultModel(string provider);

    /// <summary>
    /// Sets the default model for the specified provider.
    /// </summary>
    /// <param name="provider">The provider name</param>
    /// <param name="model">The model name to set as default</param>
    Task SetDefaultModelAsync(string provider, string model);
}
