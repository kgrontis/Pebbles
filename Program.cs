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

// Build service provider
using var serviceProvider = new ServiceCollection()
    .AddSingleton(options)
    .AddSingleton<IAIProvider, MockAIProvider>()
    .AddSingleton<ICommandHandler, CommandHandler>()
    .AddSingleton<IChatRenderer, ChatRenderer>()
    .AddSingleton<IInputHandler>(sp =>
    {
        var commandHandler = sp.GetRequiredService<ICommandHandler>();
        return new InputHandler(commandHandler.Commands);
    })
    .AddSingleton<IChatService, ChatService>()
    .BuildServiceProvider();

// Run the application
var chatService = serviceProvider.GetRequiredService<IChatService>();
await chatService.RunAsync();