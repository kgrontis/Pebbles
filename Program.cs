using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pebbles.Configuration;
using Pebbles.Services;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Build service provider
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddPebblesServices(configuration);

using var serviceProvider = services.BuildServiceProvider();

// Run the application
var chatService = serviceProvider.GetRequiredService<IChatService>();
await chatService.RunAsync();