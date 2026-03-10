namespace Pebbles.Services.Commands;

using Pebbles.Models;

/// <summary>
/// Handles plugin-related commands: /plugins, /reload, /tools.
/// </summary>
public sealed class PluginCommands(
    IPluginLoader pluginLoader,
    IToolPluginLoader? toolPluginLoader,
    ICommandHandler? parentHandler = null)
{
    public CommandResult HandlePlugins()
    {
        var plugins = pluginLoader.Plugins;

        if (plugins.Count == 0)
        {
            return CommandResult.OkWithMarkup($"""

                [dim]No plugins loaded.[/]

                Plugin directories:
                  Global:   [dim]~/.pebbles/agent/plugins/scripts/[/]
                  Project:  [dim]./.pebbles/agent/plugins/scripts/[/]

                Create a C# script in one of these directories to add custom commands.
                Use /reload to load new plugins.
                """);
        }

        var lines = new List<string>
        {
            "",
            $"[bold]Loaded Plugins ({plugins.Count})[/]",
            ""
        };

        foreach (var plugin in plugins)
        {
            lines.Add($"  [bold]{plugin.Name}[/] [dim]v{plugin.Version}[/]");
            if (!string.IsNullOrEmpty(plugin.Description))
                lines.Add($"    [dim]{plugin.Description}[/]");
            var commands = plugin.Instance?.GetCommands().ToList() ?? [];
            lines.Add($"    [dim]Commands: {commands.Count}[/]");
            foreach (var cmd in commands)
            {
                lines.Add($"      [dim]•[/] {cmd.Name} — {cmd.Description}");
            }
            lines.Add("");
        }

        lines.Add("[dim]Use /reload to reload plugins.[/]");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    public CommandResult HandleReload()
    {
        var result = pluginLoader.LoadPlugins();
        parentHandler?.RefreshPluginCommands();

        var lines = new List<string>
        {
            "",
            $"[bold green]✓[/] Reloaded plugins",
            ""
        };

        if (result.Plugins.Count > 0)
        {
            lines.Add($"  Plugins: {result.TotalCommands} command(s) from {result.Plugins.Count} plugin(s)");
            foreach (var plugin in result.Plugins)
            {
                lines.Add($"    [dim]•[/] {plugin.Name} v{plugin.Version} ({plugin.Instance?.GetCommands().Count() ?? 0} commands)");
            }
        }
        else
        {
            lines.Add("  [dim]No plugins loaded.[/]");
        }

        if (result.Errors.Count > 0)
        {
            lines.Add("");
            lines.Add($"  [bold yellow]Warnings:[/]");
            foreach (var (path, error) in result.Errors)
            {
                lines.Add($"    [dim]•[/] {Path.GetFileName(path)}: {error}");
            }
        }

        lines.Add("");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    public CommandResult HandleTools()
    {
        var lines = new List<string>
        {
            "",
            "[bold]Available Tools[/]",
            "",
            "  [dim]Built-in tools:[/]",
            "    • read_file — Read file contents",
            "    • search_files — Search for text patterns"
        };

        if (toolPluginLoader is not null)
        {
            var plugins = toolPluginLoader.Plugins;
            if (plugins.Count > 0)
            {
                lines.Add("");
                lines.Add($"  [dim]Plugin tools ({plugins.Count}):[/]");

                foreach (var plugin in plugins)
                {
                    lines.Add($"    • {plugin.Name} — {plugin.Description} [dim]v{plugin.Version}[/]");
                }
            }
        }

        lines.Add("");
        lines.Add("[dim]Tools are automatically available to the AI during conversations.[/]");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }
}