namespace Pebbles.Services;

using Pebbles.Configuration;
using Spectre.Console;

/// <summary>
/// Handles interactive provider setup on first run.
/// </summary>
public interface IProviderSetupService
{
    /// <summary>
    /// Runs the provider setup flow if needed.
    /// Returns true if setup was completed or already done.
    /// </summary>
    Task<bool> RunSetupIfNeededAsync();

    /// <summary>
    /// Forces the provider selection flow (for /provider command).
    /// </summary>
    Task RunProviderSelectionAsync();
}

/// <summary>
/// Handles interactive provider setup on first run.
/// </summary>
public sealed class ProviderSetupService(IUserSettingsService userSettingsService) : IProviderSetupService
{
    private static readonly (string Name, string DisplayName, string EnvVar)[] Providers =
    [
        ("alibabacloud", "Alibaba Cloud (Qwen, GLM, MiniMax)", "ALIBABA_CLOUD_API_KEY"),
        ("openai", "OpenAI", "OPENAI_API_KEY"),
        ("anthropic", "Anthropic", "ANTHROPIC_API_KEY")
    ];

    public async Task<bool> RunSetupIfNeededAsync()
    {
        // Check if setup is already completed with a valid API key
        if (userSettingsService.Settings.SetupCompleted && userSettingsService.HasValidApiKey())
        {
            // Show welcome back message with current provider
            var provider = Providers.FirstOrDefault(p =>
                string.Equals(p.Name, userSettingsService.Settings.Provider, StringComparison.OrdinalIgnoreCase));

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold cyan]Welcome back to Pebbles![/]");
            if (provider != default)
            {
                AnsiConsole.MarkupLine($"[dim]Using {provider.DisplayName}. Change with /provider command.[/]");
            }
            AnsiConsole.WriteLine();
            return true;
        }

        // Run setup flow
        await RunProviderSelectionAsync().ConfigureAwait(false);
        return userSettingsService.Settings.SetupCompleted;
    }

    public async Task RunProviderSelectionAsync()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold cyan]Welcome to Pebbles![/]");
        AnsiConsole.MarkupLine("[dim]Your AI coding assistant in the terminal.[/]");
        AnsiConsole.WriteLine();

        // Show provider selection with indicator for providers that have keys stored
        var choices = Providers.Select(p =>
        {
            var hasKey = userSettingsService.GetApiKey(p.Name) is not null;
            return hasKey ? $"{p.DisplayName} [dim](key saved)[/]" : p.DisplayName;
        }).ToList();

        var selectedProvider = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Select your AI provider:[/]")
                .PageSize(10)
                .AddChoices(choices));

        // Find the provider key (strip the " (key saved)" suffix if present)
        var selectedIndex = choices.IndexOf(selectedProvider);
        var (Name, DisplayName, EnvVar) = Providers[selectedIndex];

        // Check if this provider already has a stored key
        var existingKey = userSettingsService.GetApiKey(Name);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]You selected:[/] [cyan]{DisplayName}[/]");

        if (existingKey is not null)
        {
            // Provider already has a key - just switch to it
            await userSettingsService.SetProviderAsync(Name).ConfigureAwait(false);
            AnsiConsole.MarkupLine($"[green]✓ Switched to {DisplayName}.[/]");
            AnsiConsole.MarkupLine("[dim]Your saved API key will be used.[/]");
        }
        else
        {
            // Need to prompt for API key
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[dim]Enter your API key for {DisplayName}:[/]");
            AnsiConsole.WriteLine();

            var enteredKey = PromptForApiKey();

            if (string.IsNullOrWhiteSpace(enteredKey))
            {
                AnsiConsole.MarkupLine("[yellow]No API key provided. You can set it later using the /provider command.[/]");
            }
            else
            {
                await userSettingsService.SetApiKey(Name, enteredKey).ConfigureAwait(false);
                AnsiConsole.MarkupLine("[green]✓ API key saved.[/]");
            }

            // Save the provider selection
            await userSettingsService.SetProviderAsync(Name).ConfigureAwait(false);
        }

        // Initialize model providers configuration
        await InitializeModelProvidersAsync(Name).ConfigureAwait(false);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✓ Setup complete![/]");
        AnsiConsole.MarkupLine("[dim]You can change providers anytime with the /provider command.[/]");
        AnsiConsole.WriteLine();
    }

    private async Task InitializeModelProvidersAsync(string providerName)
    {
        // For Alibaba Cloud (Qwen OAuth equivalent), initialize with hard-coded models
        if (providerName.Equals("alibabacloud", StringComparison.OrdinalIgnoreCase))
        {
            var models = AlibabaCloudModels.Models;

            // Initialize model providers if not already set
            if (!userSettingsService.Settings.ModelProviders.ContainsKey("alibabacloud"))
            {
                userSettingsService.Settings.ModelProviders["alibabacloud"] = models;

                // Set default model to first available
                if (models.Count > 0)
                {
                    userSettingsService.Settings.DefaultModel = models[0].Id;
                    AnsiConsole.MarkupLine($"[green]✓ Default model set to: {models[0].Name}[/]");
                }

                await userSettingsService.SaveAsync().ConfigureAwait(false);
            }
        }
        else
        {
            // For other providers (OpenAI, Anthropic), user must configure models manually
            // Set a sensible default based on provider
            var defaultModel = providerName switch
            {
                "openai" => "gpt-4o",
                "anthropic" => "claude-3-5-sonnet-20241022",
                _ => null
            };

            if (!string.IsNullOrEmpty(defaultModel))
            {
                userSettingsService.Settings.DefaultModel = defaultModel;
                await userSettingsService.SaveAsync().ConfigureAwait(false);
                AnsiConsole.MarkupLine($"[dim]Default model: {defaultModel} (configure more in user_settings.json)[/]");
            }
        }
    }

    private static string PromptForApiKey()
    {
        // Use Console.ReadLine directly for password-style input
        // Spectre.Console's TextPrompt with secret doesn't work well in all terminals
        AnsiConsole.Markup("[bold]API Key:[/] ");

        try
        {
            // Try to read as password (hidden input)
            var key = ReadPassword();
            return key;
        }
        catch (InvalidOperationException)
        {
            // Fallback to normal input if console doesn't support key reading
            return Console.ReadLine() ?? string.Empty;
        }
    }

#pragma warning disable CA1303 // Literal string for password masking is intentional
    private static string ReadPassword()
    {
        var chars = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace && chars.Count > 0)
            {
                chars.RemoveAt(chars.Count - 1);
                // Remove the last asterisk from display
                Console.Write("\b \b");
            }
            else if (key.Key != ConsoleKey.Backspace)
            {
                chars.Add(key.KeyChar);
                // Show asterisk for each character typed
                Console.Write('*');
            }
        }
        return new string([.. chars]);
    }
#pragma warning restore CA1303
}
