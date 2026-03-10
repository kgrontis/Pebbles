using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pebbles.Configuration;
using Pebbles.Models;
using Pebbles.Services;
using Pebbles.Services.Tools;
using Pebbles.UI;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Configure options
var options = new PebblesOptions();
var section = configuration.GetSection(PebblesOptions.SectionName);
options.DefaultModel = section["DefaultModel"] ?? options.DefaultModel;
options.AvailableModels = section.GetSection("AvailableModels").Get<string[]>() ?? options.AvailableModels;
options.InputCostPer1K = section.GetValue("InputCostPer1K", options.InputCostPer1K);
options.OutputCostPer1K = section.GetValue("OutputCostPer1K", options.OutputCostPer1K);
options.TokenEstimationMultiplier = section.GetValue("TokenEstimationMultiplier", options.TokenEstimationMultiplier);
options.Provider = section["Provider"] ?? options.Provider;
options.DashScopeApiKey = section["DashScopeApiKey"];
options.DashScopeBaseUrl = section["DashScopeBaseUrl"] ?? options.DashScopeBaseUrl;
options.AutoCompressionEnabled = section.GetValue("AutoCompressionEnabled", options.AutoCompressionEnabled);
options.CompressionThreshold = section.GetValue("CompressionThreshold", options.CompressionThreshold);
options.KeepRecentMessages = section.GetValue("KeepRecentMessages", options.KeepRecentMessages);

// Build service provider
var services = new ServiceCollection();
services.AddSingleton(options);
services.AddSingleton<ContextManager>();
services.AddSingleton<IFileService, FileService>();
services.AddSingleton<IModelPicker, ModelPicker>();
services.AddSingleton<RoslynPluginService>();
services.AddSingleton<IPluginLoader, PluginLoader>();
services.AddSingleton<ISystemPromptService, SystemPromptService>();

// Choose AI provider based on configuration
if (options.Provider.Equals("dashscope", StringComparison.OrdinalIgnoreCase))
{
    services.AddSingleton<IAIProvider, DashScopeProvider>();
}
else
{
    services.AddSingleton<IAIProvider, MockAIProvider>();
}

// Register compression service
services.AddSingleton<ICompressionService, CompressionService>();

// Register memory service
services.AddSingleton<IMemoryService, MemoryService>(sp =>
{
    var promptSvc = sp.GetRequiredService<ISystemPromptService>();
    var aiProvider = sp.GetRequiredService<IAIProvider>();
    return new MemoryService(promptSvc, aiProvider);
});

services.AddSingleton<ICommandHandler, CommandHandler>(sp =>
{
    var ctx = sp.GetRequiredService<ContextManager>();
    var fileSvc = sp.GetRequiredService<IFileService>();
    var modelPicker = sp.GetRequiredService<IModelPicker>();
    var pluginLoader = sp.GetRequiredService<IPluginLoader>();
    var compressionSvc = sp.GetRequiredService<ICompressionService>();
    var aiProvider = sp.GetRequiredService<IAIProvider>();
    var memorySvc = sp.GetRequiredService<IMemoryService>();
    return new CommandHandler(options, ctx, fileSvc, modelPicker, pluginLoader, compressionSvc, aiProvider, memorySvc);
});
services.AddSingleton<IChatRenderer, ChatRenderer>();
services.AddSingleton<IInputHandler>(sp =>
{
    var cmdHandler = sp.GetRequiredService<ICommandHandler>();
    var fileSvc = sp.GetRequiredService<IFileService>();
    return new InputHandler(cmdHandler, fileSvc);
});
services.AddSingleton<IChatService, ChatService>();
services.AddSingleton<IToolPluginLoader, ToolPluginLoader>();
services.AddSingleton(sp =>
{
    var registry = new ToolRegistry(sp.GetRequiredService<IToolPluginLoader>());

    // Register essential tools (Phase 2 will add these)
    registry.RegisterTool(new ReadFileTool(sp.GetRequiredService<IFileService>()));
    // registry.RegisterTool(new WriteFileTool(sp.GetRequiredService<IFileService>()));
    // registry.RegisterTool(new RunShellTool());
     registry.RegisterTool(new SearchFilesTool(sp.GetRequiredService<IFileService>()));
    // registry.RegisterTool(new ListDirectoryTool());

    registry.LoadToolPlugins();

    return registry;
});

using var serviceProvider = services.BuildServiceProvider();

// Run the application
var chatService = serviceProvider.GetRequiredService<IChatService>();
await chatService.RunAsync();