namespace Pebbles.Services.Commands;

using Pebbles.Models;

/// <summary>
/// Handles file-related commands: /read, /files, /clearfiles.
/// </summary>
internal sealed class FileCommands(IFileService fileService)
{
    public CommandResult HandleRead(string[] args)
    {
        if (args.Length == 0)
        {
            return CommandResult.Fail("Usage: /read <path>\nExample: /read Program.cs");
        }

        var path = string.Join(" ", args);
        var content = fileService.ReadFile(path);

        if (!content.Success)
        {
            return CommandResult.Fail(content.Error ?? "Unknown error reading file");
        }

        var lines = new List<string>
        {
            "",
            $"[bold green]✓[/] Loaded: [dim]{path}[/] ({FormatSize(content.Size)})",
            "",
            $"[dim]─── {path} ───[/]",
            Spectre.Console.Markup.Escape(content.Content),
            "[dim]───[/]",
            ""
        };

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    public CommandResult HandleFiles()
    {
        var files = fileService.LoadedFiles;

        if (files.Count == 0)
        {
            return CommandResult.OkWithMarkup("\n[dim]No files loaded. Use /read <path> or @file.cs syntax to load files.[/]\n");
        }

        var lines = new List<string>
        {
            "",
            $"[bold]Loaded Files ({files.Count})[/]",
            ""
        };

        foreach (var (path, content) in files)
        {
            var status = content.Success ? "[green]✓[/]" : "[red]✗[/]";
            var size = content.Success ? FormatSize(content.Size) : content.Error;
            lines.Add($"  {status} [dim]{path}[/] ({size})");
        }

        lines.Add("");
        lines.Add("[dim]Files are included in AI context automatically.[/]");
        lines.Add("[dim]Use /clearfiles to remove all files from context.[/]");

        return CommandResult.OkWithMarkup(string.Join("\n", lines));
    }

    public CommandResult HandleClearFiles()
    {
        var count = fileService.LoadedFiles.Count;
        fileService.ClearFiles();

        return CommandResult.OkWithMarkup($"\n[dim]Cleared {count} file(s) from context.[/]\n");
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024} KB",
            _ => $"{bytes / (1024 * 1024):F1} MB"
        };
}