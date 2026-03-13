using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Services.Commands;
using Pebbles.Services.Tools;
using Pebbles.UI;
using System.Runtime.InteropServices;

namespace Pebbles.Configuration;

/// <summary>
/// Extension methods for configuring Pebbles services.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Pebbles services to the service collection.
    /// </summary>
    public static IServiceCollection AddPebblesServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IUserSettingsService? userSettingsService = null)
    {
        // Configure options with validation
        services.AddOptions<PebblesOptions>()
            .Bind(configuration.GetSection(PebblesOptions.SectionName));

        services.AddSingleton<IValidateOptions<PebblesOptions>, PebblesOptionsValidator>();

        // Register options as singleton for services that need it directly
        // Apply provider from user settings if available
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PebblesOptions>>().Value;
            if (userSettingsService is not null)
            {
                options.Provider = userSettingsService.Settings.Provider;
            }
            return options;
        });

        // Register core services
        services.AddSingleton<ContextManager>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IModelPicker, ModelPicker>();
        services.AddSingleton<RoslynPluginService>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<ISystemPromptService, SystemPromptService>();

        // Register AI provider based on configuration
        services.AddAIProvider(configuration, userSettingsService);

        // Register tool services
        services.AddTools();

        // Register compression and memory
        services.AddSingleton<ICompressionService, CompressionService>();
        services.AddSingleton<IMemoryService>(sp =>
        {
            var promptSvc = sp.GetRequiredService<ISystemPromptService>();
            var aiProvider = sp.GetRequiredService<IAIProvider>();
            return new MemoryService(promptSvc, aiProvider);
        });

        // Register context management
        services.AddSingleton<IContextManagementService, ContextManagementService>();

        // Register session store
        services.AddSingleton<ISessionStore, SessionStore>();
        services.AddSingleton<SessionCommands>();

        // Register skill services
        services.AddSingleton<ISkillLoader, SkillLoader>();
        services.AddSingleton<SkillCommands>();

        // Register command handlers (split by responsibility)
        services.AddCommandHandlers();

        // Register UI services
        services.AddSingleton<IChatRenderer, ChatRenderer>();
        services.AddSingleton<IInputHandler>(sp =>
        {
            var cmdHandler = sp.GetRequiredService<ICommandHandler>();
            var fileSvc = sp.GetRequiredService<IFileService>();
            return new InputHandler(cmdHandler, fileSvc);
        });

        // Register main chat service
        services.AddSingleton<IChatService, ChatService>();

        return services;
    }

    /// <summary>
    /// Adds the AI provider based on configuration.
    /// </summary>
    public static IServiceCollection AddAIProvider(
        this IServiceCollection services,
        IConfiguration configuration,
        IUserSettingsService? userSettingsService = null)
    {
        var providerSection = configuration.GetSection($"{PebblesOptions.SectionName}:Provider");
        var provider = providerSection.Value ?? ProviderNames.Mock;

        // Register HttpClient for the selected provider using IHttpClientFactory pattern
        // This avoids socket exhaustion and properly manages HttpClient lifetimes
        if (IsAlibabaCloudProvider(provider))
        {
            services.AddHttpClient(ProviderNames.AlibabaCloud)
                .ConfigureHttpClient((sp, client) =>
                {
                    var options = sp.GetRequiredService<PebblesOptions>();
                    client.Timeout = TimeSpan.FromSeconds(options.HttpClientTimeoutSeconds);
                    var apiKey = GetApiKey(userSettingsService, options.AlibabaCloudApiKey, "alibabacloud", "ALIBABA_CLOUD_API_KEY") ?? throw new InvalidOperationException(
                            "Alibaba Cloud API key not configured. Set ALIBABA_CLOUD_API_KEY environment variable " +
                            "or add AlibabaCloudApiKey to appsettings.json");
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

                    // Required User-Agent header for Coding Plan API
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(GetUserAgent());
                });

            services.AddSingleton<IAIProvider>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(ProviderNames.AlibabaCloud);
                var options = sp.GetRequiredService<PebblesOptions>();
                var contextManager = sp.GetRequiredService<ContextManager>();
                var fileService = sp.GetRequiredService<IFileService>();
                var systemPromptService = sp.GetRequiredService<ISystemPromptService>();
                var skillCommands = sp.GetRequiredService<SkillCommands>();
                return new AlibabaCloudProvider(httpClient, options, contextManager, fileService, systemPromptService, skillCommands);
            });
        }
        else if (provider.Equals(ProviderNames.OpenAI, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(ProviderNames.OpenAI)
                .ConfigureHttpClient((sp, client) =>
                {
                    var options = sp.GetRequiredService<PebblesOptions>();
                    client.Timeout = TimeSpan.FromSeconds(options.HttpClientTimeoutSeconds);
                    var apiKey = GetApiKey(userSettingsService, options.OpenAiApiKey, "openai", "OPENAI_API_KEY");
                    if (apiKey is null)
                    {
                        throw new InvalidOperationException(
                            "OpenAI API key not configured. Set OPENAI_API_KEY environment variable " +
                            "or add OpenAiApiKey to appsettings.json");
                    }
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(GetUserAgent());
                });

            services.AddSingleton<IAIProvider>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(ProviderNames.OpenAI);
                var options = sp.GetRequiredService<PebblesOptions>();
                return new OpenAIProvider(httpClient, options);
            });
        }
        else if (provider.Equals(ProviderNames.Anthropic, StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient(ProviderNames.Anthropic)
                .ConfigureHttpClient((sp, client) =>
                {
                    var options = sp.GetRequiredService<PebblesOptions>();
                    client.Timeout = TimeSpan.FromSeconds(options.HttpClientTimeoutSeconds);
                    var apiKey = GetApiKey(userSettingsService, options.AnthropicApiKey, "anthropic", "ANTHROPIC_API_KEY");
                    if (apiKey is null)
                    {
                        throw new InvalidOperationException(
                            "Anthropic API key not configured. Set ANTHROPIC_API_KEY environment variable " +
                            "or add AnthropicApiKey to appsettings.json");
                    }
                    client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                    client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.UserAgent.ParseAdd(GetUserAgent());
                });

            services.AddSingleton<IAIProvider>(sp =>
            {
                var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                var httpClient = httpClientFactory.CreateClient(ProviderNames.Anthropic);
                var options = sp.GetRequiredService<PebblesOptions>();
                return new AnthropicProvider(httpClient, options);
            });
        }
        else
        {
            services.AddSingleton<IAIProvider, MockAIProvider>();
        }

        return services;
    }

    /// <summary>
    /// Checks if the provider is Alibaba Cloud (including legacy DashScope name).
    /// </summary>
    private static bool IsAlibabaCloudProvider(string provider) =>
        provider.Equals(ProviderNames.AlibabaCloud, StringComparison.OrdinalIgnoreCase) ||
        provider.Equals(ProviderNames.DashScope, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets an API key from user settings, config, or environment variable.
    /// Priority: user settings file > config value > environment variable.
    /// </summary>
    private static string? GetApiKey(
        IUserSettingsService? userSettingsService,
        string? configValue,
        string providerName,
        string envVarName)
    {
        // First check user settings file (persisted from previous sessions)
        if (userSettingsService is not null)
        {
            var storedKey = userSettingsService.GetApiKey(providerName);
            if (!string.IsNullOrWhiteSpace(storedKey))
            {
                return storedKey;
            }
        }

        // Then check config value (must be non-empty)
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue;
        }

        // Finally fall back to environment variable (must be non-empty)
        var envValue = Environment.GetEnvironmentVariable(envVarName);
        return string.IsNullOrWhiteSpace(envValue) ? null : envValue;
    }

    /// <summary>
    /// Adds tool-related services.
    /// </summary>
    public static IServiceCollection AddTools(this IServiceCollection services)
    {
        services.AddSingleton<IToolPluginLoader, ToolPluginLoader>();

        // Register built-in tools
        services.AddSingleton<ReadFileTool>();
        services.AddSingleton<SearchFilesTool>();
        services.AddSingleton<ShellTool>();
        services.AddSingleton<ListDirectoryTool>();
        services.AddSingleton<WriteFileTool>();

        // Register tool registry with tools
        services.AddSingleton<IToolRegistry>(sp =>
        {
            var pluginLoader = sp.GetRequiredService<IToolPluginLoader>();
            var fileService = sp.GetRequiredService<IFileService>();

            var registry = new ToolRegistry(pluginLoader);
            registry.RegisterTool(new ReadFileTool(fileService));
            registry.RegisterTool(new SearchFilesTool());
            registry.RegisterTool(new ShellTool());
            registry.RegisterTool(new ListDirectoryTool(fileService));
            registry.RegisterTool(new WriteFileTool());
            registry.LoadToolPlugins();

            return registry;
        });

        // Register tool execution service AFTER registry
        services.AddSingleton<IToolExecutionService, ToolExecutionService>();

        return services;
    }

    /// <summary>
    /// Adds command handlers using the composite pattern.
    /// Each handler class has 2-4 dependencies for better testability.
    /// </summary>
    private static void AddCommandHandlers(this IServiceCollection services)
    {
        // Specialized command handlers (only those with instance dependencies)
        services.AddSingleton<FileCommands>();
        services.AddSingleton<CompressionCommands>();
        services.AddSingleton<MemoryCommands>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<IToolPluginLoader, ToolPluginLoader>();

        // Composite handler that aggregates all specialized handlers
        // Note: PluginCommands is created internally by CompositeCommandHandler to avoid circular dependency
        services.AddSingleton<ICommandHandler, CompositeCommandHandler>();
    }

    /// <summary>
    /// Generates a User-Agent string in the format: Pebbles/{version} ({platform}; {arch})
    /// </summary>
    private static string GetUserAgent()
    {
        var version = typeof(ServiceCollectionExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        var platform = Environment.OSVersion.Platform.ToString();
        var arch = RuntimeInformation.ProcessArchitecture.ToString();
        return $"Pebbles/{version} ({platform}; {arch})";
    }
}