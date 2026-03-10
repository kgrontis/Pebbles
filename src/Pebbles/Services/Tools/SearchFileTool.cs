namespace Pebbles.Services.Tools;

using System.Text.Json;
using System.Text.RegularExpressions;
using Pebbles.Models;

/// <summary>
/// Tool to search for text patterns in files (grep-style search).
/// </summary>
public class SearchFilesTool() : ITool
{
    public string Name => "search_files";

    public string Description =>
        "Searches for a text pattern in files using regex. " +
        "Returns matching lines with file paths and line numbers. " +
        "Use this to find code patterns, usages, or definitions across the codebase.";

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
                    Properties = new Dictionary<string, ToolParameterProperty>
                    {
                        ["pattern"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "The regex pattern to search for."
                        },
                        ["path"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "Optional: Directory to search in. Defaults to current directory."
                        },
                        ["include"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "Optional: Glob pattern for files to include (e.g., '*.cs')."
                        },
                        ["max_results"] = new ToolParameterProperty
                        {
                            Type = "number",
                            Description = "Optional: Maximum number of results to return. Defaults to 100."
                        }
                    },
                    Required = ["pattern"]
                }
            }
        };
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Deserialize<SearchFilesArgs>(arguments);
        if (args is null || string.IsNullOrWhiteSpace(args.Pattern))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Invalid arguments: 'pattern' is required"
            };
        }

        try
        {
            var searchPath = args.Path ?? Directory.GetCurrentDirectory();
            var maxResults = args.MaxResults ?? 100;
            var results = new List<string>();
            var count = 0;

            Regex regex;
            try
            {
                regex = new Regex(args.Pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            }
            catch (Exception ex)
            {
                return new ToolExecutionResult
                {
                    Success = false,
                    Error = $"Invalid regex pattern: {ex.Message}"
                };
            }

            var files = Directory.EnumerateFiles(searchPath, args.Include ?? "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (count >= maxResults) break;
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    var lines = await File.ReadAllLinesAsync(file, cancellationToken);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (regex.IsMatch(lines[i]))
                        {
                            results.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                            count++;
                            if (count >= maxResults) break;
                        }
                    }
                }
                catch
                {
                    // Skip binary/unreadable files
                }
            }

            var content = count == 0
                ? "No matches found."
                : $"Found {count} match(es):\n\n{string.Join("\n", results)}";

            return new ToolExecutionResult
            {
                Success = true,
                Content = content
            };
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Search failed: {ex.Message}"
            };
        }
    }

    private record SearchFilesArgs
    {
        public string Pattern { get; init; } = string.Empty;
        public string? Path { get; init; }
        public string? Include { get; init; }
        public int? MaxResults { get; init; }
    }
}