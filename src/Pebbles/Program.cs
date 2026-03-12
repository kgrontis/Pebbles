using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pebbles.Configuration;
using Pebbles.Services;
using Spectre.Console;
using System.Text;

// Set console encoding to UTF-8 for Unicode symbol support
Console.OutputEncoding = Encoding.UTF8;

// Ensure global agent directory exists with default prompts
AgentInitializer.EnsureInitialized();

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Initialize user settings service early (before DI container)
var userSettingsService = new UserSettingsService();
var providerSetupService = new ProviderSetupService(userSettingsService);

// Run provider setup if needed (first run or missing API key)
await providerSetupService.RunSetupIfNeededAsync().ConfigureAwait(false);

// Build service provider with the selected provider
var services = new ServiceCollection();

// Add logging - suppress verbose HTTP client logs
services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning);

    // Suppress HttpClient logging entirely
    builder.AddFilter("System.Net.Http", LogLevel.Error);
    builder.AddFilter("Microsoft.Extensions.Http", LogLevel.Error);
});

// Register user settings service (already created)
services.AddSingleton<IUserSettingsService>(userSettingsService);
services.AddSingleton<IProviderSetupService>(providerSetupService);

// Add all Pebbles services with user settings
services.AddPebblesServices(configuration, userSettingsService);

using var serviceProvider = services.BuildServiceProvider();

// Run the application with error handling
var chatService = serviceProvider.GetRequiredService<IChatService>();
try
{
    await chatService.RunAsync().ConfigureAwait(false);
}
catch (HttpRequestException ex)
{
    AnsiConsole.MarkupLine($"[red]Network Error: {ex.Message}[/]");
    AnsiConsole.MarkupLine("[dim]Check your API key and network connection.[/]");
    Environment.Exit(1);
}
catch (TaskCanceledException) when (!Environment.HasShutdownStarted)
{
    // This indicates a timeout (not user cancellation)
    AnsiConsole.MarkupLine("[red]Timeout: The request took too long.[/]");
    AnsiConsole.MarkupLine("[dim]Try increasing HttpClientTimeoutSeconds in your configuration.[/]");
    Environment.Exit(1);
}
catch (OperationCanceledException)
{
    // User cancelled via Ctrl+C
    AnsiConsole.MarkupLine("[dim]Operation cancelled.[/]");
    Environment.Exit(0);
}
catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
{
    AnsiConsole.MarkupLine($"[red]Configuration Error: {ex.Message}[/]");
    Environment.Exit(2);
}
catch (InvalidOperationException ex)
{
    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
    Environment.Exit(1);
}
catch (TimeoutException ex)
{
    AnsiConsole.MarkupLine($"[red]Timeout: {ex.Message}[/]");
    AnsiConsole.MarkupLine("[dim]The request took too long. Please try again.[/]");
    Environment.Exit(1);
}