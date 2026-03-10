namespace Pebbles.Services.Tools;

using Pebbles.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Tool to read file contents.
/// </summary>
public class ReadFileTool : ITool
{
    private readonly IFileService _fileService;

    public ReadFileTool(IFileService fileService)
    {
        _fileService = fileService;
    }

    public string Name => "read_file";

    public string Description =>
        "Reads and returns the content of a specified file. " +
        "For large files, use offset and limit to read specific portions. " +
        "Handles text, images, and PDF files.";

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
                            Description = "The path to the file to read. Can be absolute or relative to the current working directory."
                        },
                        ["offset"] = new ToolParameterProperty
                        {
                            Type = "number",
                            Description = "Optional: 0-based line number to start reading from. Requires 'limit'."
                        },
                        ["limit"] = new ToolParameterProperty
                        {
                            Type = "number",
                            Description = "Optional: Maximum number of lines to read. Use with 'offset' for pagination."
                        }
                    },
                    Required = ["path"]
                }
            }
        };
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        // Debug: Log the raw arguments
        Console.WriteLine($"[DEBUG] ReadFileTool received arguments: {arguments}");
        
        var args = JsonSerializer.Deserialize<ReadFileArgs>(arguments);
        if (args is null || string.IsNullOrWhiteSpace(args.Path))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Invalid arguments: 'path' is required. Received: {arguments}"
            };
        }

        var result = _fileService.ReadFile(args.Path);

        return result.Success
            ? new ToolExecutionResult { Success = true, Content = result.Content }
            : new ToolExecutionResult { Success = false, Error = result.Error };
    }

    private record ReadFileArgs
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;
        
        [JsonPropertyName("offset")]
        public int? Offset { get; init; }
        
        [JsonPropertyName("limit")]
        public int? Limit { get; init; }
    }
}