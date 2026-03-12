namespace Pebbles.Services.Tools;

using Pebbles.Models;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Tool to list directory contents.
/// </summary>
internal sealed class ListDirectoryTool(IFileService fileService) : ITool
{
    public string Name => "list_directory";

    public string Description =>
        "Lists files and directories in a specified path. " +
        "Shows file names, directory indicators, and extensions. " +
        "Use this to explore project structure or find files. " +
        "If no path is provided, lists the current working directory.";

    public ToolDefinition GetDefinition()
    {
        return new ToolDefinition
        {
            Type = "function",
            Function = new FunctionDefinition
            {
                Name = Name,
                Description = Description,
                Parameters = new ToolParameters
                {
                    Type = "object",
                    Properties = new Dictionary<string, ToolParameterProperty>
                    {
                        ["path"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "Optional: Directory path to list (default: current working directory)"
                        },
                        ["filter"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "Optional: Filter by name prefix (e.g., '*.cs' or 'Program')"
                        }
                    }
                }
            }
        };
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Keep async signature for interface compatibility

        var args = JsonSerializer.Deserialize<ListDirectoryArgs>(arguments);
        var path = string.IsNullOrWhiteSpace(args?.Path) ? null : args.Path;
        var filter = string.IsNullOrWhiteSpace(args?.Filter) ? null : args.Filter;

        try
        {
#pragma warning disable CS8604 // Null argument for parameter accepting null
            var items = fileService.ListDirectory(path, filter);
#pragma warning restore CS8604

            if (items.Count == 0)
            {
                return new ToolExecutionResult
                {
                    Success = true,
                    Content = "Directory is empty or does not exist."
                };
            }

            var output = new System.Text.StringBuilder();
            var currentDir = string.Empty;

            foreach (var item in items)
            {
                var icon = item.IsDirectory ? "📁" : "📄";
                var name = item.IsDirectory ? $"{item.Name}/" : item.Name;
                output.AppendLine(CultureInfo.InvariantCulture, $"  {icon} {name}");
            }

            var dirPath = path is not null ? path : Directory.GetCurrentDirectory();
            output.Insert(0, $"📂 {dirPath}\n\n");

            return new ToolExecutionResult
            {
                Success = true,
                Content = output.ToString().Trim()
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Failed to list directory: {ex.Message}"
            };
        }
    }

    private sealed record ListDirectoryArgs
    {
        [JsonPropertyName("path")]
        public string? Path { get; init; }

        [JsonPropertyName("filter")]
        public string? Filter { get; init; }
    }
}