namespace Pebbles.Services;

using Pebbles.Models;
using System.Text.Json;

/// <summary>
/// File-based user settings storage.
/// Settings are stored in ~/.pebbles/user_settings.json
/// API keys are stored per provider in the settings file.
/// </summary>
internal class UserSettingsService : IUserSettingsService
{
    private readonly string _settingsFilePath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly UserSettings _settings;

    // Environment variable names for each provider
    private static readonly Dictionary<string, string> ProviderEnvVars = new(StringComparer.OrdinalIgnoreCase)
    {
        ["alibabacloud"] = "ALIBABA_CLOUD_API_KEY",
        ["openai"] = "OPENAI_API_KEY",
        ["anthropic"] = "ANTHROPIC_API_KEY"
    };

    public UserSettingsService() : this(null) { }

    protected UserSettingsService(string? customDirectory)
    {
        var pebblesDir = customDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pebbles");

        Directory.CreateDirectory(pebblesDir);
        _settingsFilePath = Path.Combine(pebblesDir, "user_settings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        _settings = LoadSettings();
    }

    public UserSettings Settings => _settings;

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_settings, _jsonOptions);
        await File.WriteAllTextAsync(_settingsFilePath, json).ConfigureAwait(false);
    }

    public string? GetApiKey(string provider)
    {
        // First check if we have a key stored in settings for this provider
        if (_settings.ApiKeys.TryGetValue(provider, out var storedKey) && !string.IsNullOrEmpty(storedKey))
        {
            return storedKey;
        }

        // Fall back to environment variable
        if (!ProviderEnvVars.TryGetValue(provider, out var envVar))
            return null;

        return Environment.GetEnvironmentVariable(envVar);
    }

    public async Task SetApiKey(string provider, string apiKey)
    {
        // Store in settings dictionary for persistence
        _settings.ApiKeys[provider] = apiKey;
        await SaveAsync().ConfigureAwait(false);

        // Also set as environment variable for the current process
        if (ProviderEnvVars.TryGetValue(provider, out var envVar))
        {
            Environment.SetEnvironmentVariable(envVar, apiKey);
        }
    }

    public async Task SetProviderAsync(string provider)
    {
        _settings.Provider = provider;
        _settings.SetupCompleted = true;
        await SaveAsync().ConfigureAwait(false);
    }

    public bool HasValidApiKey()
    {
        var apiKey = GetApiKey(_settings.Provider);
        return !string.IsNullOrEmpty(apiKey);
    }

    private UserSettings LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new UserSettings();
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, _jsonOptions);
            return settings ?? new UserSettings();
        }
        catch (JsonException)
        {
            // If settings file is corrupted, start fresh
            return new UserSettings();
        }
        catch (IOException)
        {
            // If we can't read the file, start fresh
            return new UserSettings();
        }
    }

}