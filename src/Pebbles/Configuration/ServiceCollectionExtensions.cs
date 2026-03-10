using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Services.Commands;
using Pebbles.Services.Tools;
using Pebbles.UI;

namespace Pebbles.Configuration;

/// <summary>
/// Extension methods for configuring Pebbles services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Pebbles services to the service collection.
    /// </summary>
    public static IServiceCollection AddPebblesServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options with validation
        services.AddOptions<PebblesOptions>()
            .Bind(configuration.GetSection(PebblesOptions.SectionName));

        services.AddSingleton<IValidateOptions<PebblesOptions>, PebblesOptionsValidator>();

        // Register options as singleton for services that need it directly
        services.AddSingleton<PebblesOptions>(sp =>
            sp.GetRequiredService<IOptions<PebblesOptions>>().Value);

        // Register core services
        services.AddSingleton<ContextManager>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IModelPicker, ModelPicker>();
        services.AddSingleton<RoslynPluginService>();
        services.AddSingleton<IPluginLoader, PluginLoader>();
        services.AddSingleton<ISystemPromptService, SystemPromptService>();

        // Register AI provider based on configuration
        services.AddAIProvider(configuration);

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
        IConfiguration configuration)
    {
        var providerSection = configuration.GetSection($"{PebblesOptions.SectionName}:Provider");
        var provider = providerSection.Value ?? "mock";

        if (provider.Equals("dashscope", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAIProvider, DashScopeProvider>();
        }
        else
        {
            services.AddSingleton<IAIProvider, MockAIProvider>();
        }

        return services;
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
}