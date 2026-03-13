namespace Pebbles.Services;

using Pebbles.Models;
using System.Net.Http.Headers;
using System.Text.Json;

/// <summary>
/// Service for managing discovered models from AI providers.
/// Models are cached in ~/.pebbles/models.json
/// </summary>
public sealed class ModelsService : IModelsService
{
    private readonly string _modelsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly Dictionary<string, ModelsSettings> _cache = new(StringComparer.OrdinalIgnoreCase);

    public ModelsService()
    {
        var pebblesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pebbles");

        Directory.CreateDirectory(pebblesDir);
        _modelsFilePath = Path.Combine(pebblesDir, "models.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        LoadAllSettings();
    }

    public async Task<List<string>> GetModelsAsync(string provider, string? apiKey, Uri baseUrl, bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        // Check cache first (unless force refresh)
        if (!forceRefresh && _cache.TryGetValue(provider, out var cached) && cached.Models.Count > 0)
        {
            return cached.Models;
        }

        // Fetch from API
        var models = await FetchModelsFromApiAsync(provider, apiKey, baseUrl, cancellationToken).ConfigureAwait(false);

        if (models.Count > 0)
        {
            // Save to cache and file
            var settings = new ModelsSettings
            {
                Provider = provider,
                Models = models,
                LastFetched = DateTime.UtcNow
            };

            _cache[provider] = settings;
            await SaveSettingsAsync().ConfigureAwait(false);
        }

        return models;
    }

    public List<string> GetCachedModels(string provider)
    {
        return _cache.TryGetValue(provider, out var settings) ? settings.Models : [];
    }

    public DateTime GetLastFetchTime(string provider)
    {
        return _cache.TryGetValue(provider, out var settings) ? settings.LastFetched : DateTime.MinValue;
    }

    public string? GetDefaultModel(string provider)
    {
        return _cache.TryGetValue(provider, out var settings) ? settings.DefaultModel : null;
    }

    public async Task SetDefaultModelAsync(string provider, string model)
    {
        if (!_cache.TryGetValue(provider, out var settings))
        {
            settings = new ModelsSettings { Provider = provider };
            _cache[provider] = settings;
        }

        settings.DefaultModel = model;
        await SaveSettingsAsync().ConfigureAwait(false);
    }

    private void LoadAllSettings()
    {
        if (!File.Exists(_modelsFilePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_modelsFilePath);
            var settings = JsonSerializer.Deserialize<List<ModelsSettings>>(json, _jsonOptions);

            if (settings is not null)
            {
                _cache.Clear();
                foreach (var setting in settings)
                {
                    _cache[setting.Provider] = setting;
                }
            }
        }
        catch (JsonException)
        {
            // If file is corrupted, start fresh
        }
        catch (IOException)
        {
            // If we can't read the file, start fresh
        }
    }

    private async Task SaveSettingsAsync()
    {
        var json = JsonSerializer.Serialize(_cache.Values.ToList(), _jsonOptions);
        await File.WriteAllTextAsync(_modelsFilePath, json).ConfigureAwait(false);
    }

    private static async Task<List<string>> FetchModelsFromApiAsync(string provider, string? apiKey, Uri baseUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return [];
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // User-Agent header is often required
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Pebbles/1.0");

        var modelsUrl = new Uri(baseUrl, "models");

        try
        {
            var response = await httpClient.GetAsync(modelsUrl, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var jsonDoc = JsonDocument.Parse(content);

            if (jsonDoc.RootElement.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                var models = new List<string>();

                foreach (var item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
                    {
                        models.Add(idElement.GetString()!);
                    }
                }

                return models;
            }

            return [];
        }
        catch (HttpRequestException)
        {
            // Network error - return empty list
            return [];
        }
        catch (JsonException)
        {
            // Parse error - return empty list
            return [];
        }
        catch (TaskCanceledException)
        {
            // Timeout - return empty list
            return [];
        }
    }
}
