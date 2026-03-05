using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pebbles.Configuration;
using Pebbles.Services;
using Pebbles.UI;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Configure options (AOT-friendly manual binding)
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
options.SystemPrompt = section["SystemPrompt"] ?? options.SystemPrompt;

// Build service provider
var services = new ServiceCollection();
services.AddSingleton(options);
services.AddSingleton<ContextManager>();

// Choose AI provider based on configuration
if (options.Provider.Equals("dashscope", StringComparison.OrdinalIgnoreCase))
{
    services.AddSingleton<IAIProvider, DashScopeProvider>();
}
else
{
    services.AddSingleton<IAIProvider, MockAIProvider>();
}

services.AddSingleton<ICommandHandler, CommandHandler>(sp =>
{
    var ctx = sp.GetRequiredService<ContextManager>();
    return new CommandHandler(options, ctx);
});
services.AddSingleton<IChatRenderer, ChatRenderer>();
services.AddSingleton<IInputHandler>(sp =>
{
    var commandHandler = sp.GetRequiredService<ICommandHandler>();
    return new InputHandler(commandHandler.Commands);
});
services.AddSingleton<IChatService, ChatService>();

using var serviceProvider = services.BuildServiceProvider();

// Run the application
var chatService = serviceProvider.GetRequiredService<IChatService>();
await chatService.RunAsync();