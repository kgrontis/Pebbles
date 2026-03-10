using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pebbles.Configuration;
using Pebbles.Services;
using System.Text;

// Set console encoding to UTF-8 for Unicode symbol support
Console.OutputEncoding = Encoding.UTF8;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
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