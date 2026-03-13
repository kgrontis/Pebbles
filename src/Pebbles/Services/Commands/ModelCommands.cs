namespace Pebbles.Services.Commands;

using Pebbles.Models;
using Spectre.Console;

/// <summary>
/// Handles model-related commands: /models, /models refresh.
/// </summary>
public sealed class ModelCommands(IModelsService modelsService, IUserSettingsService userSettingsService)
{
    public async Task<CommandResult> HandleModelsAsync(string[] args)
    {
        var provider = userSettingsService.Settings.Provider;

        if (args.Length == 0)
        {
            // Show current models
            return await HandleModelsListAsync(provider).ConfigureAwait(false);
        }

        if (args[0].Equals("refresh", StringComparison.OrdinalIgnoreCase))
        {
            return await HandleModelsRefreshAsync(provider).ConfigureAwait(false);
        }

        return CommandResult.Fail("Usage: /models [refresh]");
    }

    private async Task<CommandResult> HandleModelsListAsync(string provider)
    {
        var models = modelsService.GetCachedModels(provider);
        var lastFetch = modelsService.GetLastFetchTime(provider);
        var defaultModel = modelsService.GetDefaultModel(provider);

        if (models.Count == 0)
        {
            return CommandResult.OkWithMarkup($"""
                [yellow]No models cached for {provider}.[/]
                [dim]Run /models refresh to fetch available models from the API.[/]
                """);
        }

        var lines = new List<string>
        {
            $"[bold]Available Models for {provider}[/]",
            $"[dim]Last fetched: {lastFetch:yyyy-MM-dd HH:mm:ss} UTC[/]",
            ""
        };

        foreach (var model in models)
        {
            var marker = model == defaultModel ? "[green]✓[/] " : "  ";
            lines.Add($"{marker}{model}");
        }

        lines.Add("");
        lines.Add("[dim]Use /model <name> to switch. Run /models refresh to update list.[/]");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    private async Task<CommandResult> HandleModelsRefreshAsync(string provider)
    {
        var apiKey = userSettingsService.GetApiKey(provider);

        if (string.IsNullOrEmpty(apiKey))
        {
            return CommandResult.Fail("No API key configured. Run /provider to set up your API key.");
        }

        var baseUrl = GetBaseUrlForProvider(provider);

        AnsiConsole.MarkupLine($"[dim]Fetching models from {provider}...[/]");

        var models = await modelsService.GetModelsAsync(provider, apiKey, baseUrl, forceRefresh: true).ConfigureAwait(false);

        if (models.Count == 0)
        {
            return CommandResult.Fail($"Failed to fetch models from {provider}. The API may not support model listing or there was a network error.");
        }

        AnsiConsole.MarkupLine($"[green]✓ Fetched {models.Count} models.[/]");

        var lines = new List<string>
        {
            $"[bold]Updated Models for {provider}[/]",
            $"[dim]Fetched: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC[/]",
            ""
        };

        foreach (var model in models)
        {
            lines.Add($"  {model}");
        }

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    private static Uri GetBaseUrlForProvider(string provider)
    {
        return provider switch
        {
            "openai" => new Uri("https://api.openai.com/v1"),
            "anthropic" => new Uri("https://api.anthropic.com"),
            "alibabacloud" => new Uri("https://coding-intl.dashscope.aliyuncs.com/v1"),
            _ => new Uri("https://api.openai.com/v1")
        };
    }
}
