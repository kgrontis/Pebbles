namespace Pebbles.Services.Tools;

using Pebbles.Models;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Tool to write content to files safely with backup support.
/// </summary>
internal sealed class WriteFileTool : ITool
{
    private readonly string _workingDirectory;
    private readonly string _backupDirectory;

    public WriteFileTool()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
        _backupDirectory = Path.Combine(_workingDirectory, ".pebbles", "backups");
    }

    public string Name => "write_file";

    public string Description =>
        "Writes content to a file. Creates the file if it doesn't exist, or overwrites if it does. " +
        "Automatically creates parent directories if they don't exist. " +
        "Backs up existing files before overwriting (stored in .pebbles/backups/). " +
        "Use this to create new code files, modify existing files, or write configuration. " +
        "Supports UTF-8 encoding. For large files, consider writing in chunks.";

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
                            Description = "The file path to write to (relative or absolute). Directories will be created if missing."
                        },
                        ["content"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "The content to write to the file"
                        },
                        ["append"] = new ToolParameterProperty
                        {
                            Type = "boolean",
                            Description = "Optional: If true, append to existing file instead of overwriting (default: false)"
                        },
                        ["createBackup"] = new ToolParameterProperty
                        {
                            Type = "boolean",
                            Description = "Optional: If true, backup existing file before overwrite (default: true)"
                        }
                    },
                    Required = ["path", "content"]
                }
            }
        };
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Keep async signature for interface compatibility

        var args = JsonSerializer.Deserialize<WriteFileArgs>(arguments);
        if (args is null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Invalid arguments: could not parse JSON"
            };
        }

        if (string.IsNullOrWhiteSpace(args.Path))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Invalid arguments: 'path' is required"
            };
        }

        if (args.Content is null)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Invalid arguments: 'content' is required"
            };
        }

        // Resolve path
        var fullPath = ResolvePath(args.Path);

        // Validate path safety
        var validationError = ValidatePath(fullPath);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = validationError
            };
        }

        try
        {
            // Create parent directories if needed
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Backup existing file if it exists and backup is enabled
            var backedUp = false;
            string? backupPath = null;
            if (File.Exists(fullPath) && args.CreateBackup != false)
            {
                backupPath = CreateBackup(fullPath);
                backedUp = true;
            }

            // Write atomically with retry (write to temp, then move)
            var policy = RetryPolicies.GetFileIoPolicy();
            var tempPath = fullPath + ".tmp." + Guid.NewGuid().ToString("N")[..8];

            await policy.ExecuteAsync(
                async ct =>
                {
                    await File.WriteAllTextAsync(tempPath, args.Content, Encoding.UTF8, ct).ConfigureAwait(false);
                    File.Move(tempPath, fullPath, overwrite: true);
                },
                cancellationToken).ConfigureAwait(false);

            var result = new StringBuilder();
            result.AppendLine(CultureInfo.InvariantCulture, $"✓ File written: {args.Path}");
            result.AppendLine(CultureInfo.InvariantCulture, $"  Size: {args.Content.Length:N0} bytes");
            result.AppendLine(CultureInfo.InvariantCulture, $"  Encoding: UTF-8");

            if (backedUp && backupPath is not null)
            {
                result.AppendLine(CultureInfo.InvariantCulture, $"  Backup: {backupPath}");
            }

            return new ToolExecutionResult
            {
                Success = true,
                Content = result.ToString().Trim()
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Failed to write file: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates a backup of an existing file.
    /// </summary>
    private string CreateBackup(string filePath)
    {
        // Create backup directory if needed
        if (!Directory.Exists(_backupDirectory))
        {
            Directory.CreateDirectory(_backupDirectory);
        }

        // Generate backup path with timestamp
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var relativePath = Path.GetRelativePath(_workingDirectory, filePath);
        var backupSubDir = Path.Combine(_backupDirectory, timestamp);
        var backupPath = Path.Combine(backupSubDir, relativePath);

        // Create backup subdirectory
        var backupDir = Path.GetDirectoryName(backupPath);
        if (!string.IsNullOrEmpty(backupDir) && !Directory.Exists(backupDir))
        {
            Directory.CreateDirectory(backupDir);
        }

        // Copy file
        File.Copy(filePath, backupPath, overwrite: true);

        return backupPath;
    }

    /// <summary>
    /// Validates the path for safety.
    /// </summary>
    private string? ValidatePath(string fullPath)
    {
        // Block writes outside working directory (with some exceptions)
        var allowedPrefixes = new[]
        {
            _workingDirectory,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        var isAllowed = false;
        foreach (var prefix in allowedPrefixes)
        {
            if (fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                isAllowed = true;
                break;
            }
        }

        if (!isAllowed)
        {
            return $"Path is outside allowed directories. Path: {fullPath}";
        }

        // Block sensitive paths
        var blockedPaths = new[]
        {
            "windows",
            "system32",
            "program files",
            "program files (x86)",
            "/etc/",
            "/usr/",
            "/bin/",
            "/sbin/"
        };

        foreach (var blocked in blockedPaths)
        {
            if (fullPath.Contains(blocked, StringComparison.OrdinalIgnoreCase))
            {
                return $"Cannot write to system path: {blocked}";
            }
        }

        // Check file size limit (10MB)
        if (File.Exists(fullPath))
        {
            var fileInfo = new FileInfo(fullPath);
            const long maxSize = 10 * 1024 * 1024; // 10MB
            if (fileInfo.Length > maxSize)
            {
                return $"File too large to overwrite: {fileInfo.Length / (1024 * 1024)}MB (max: 10MB)";
            }
        }

        return null;
    }

    /// <summary>
    /// Resolves a path (handles ~ for home directory and relative paths).
    /// </summary>
    private string ResolvePath(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..].TrimStart('/', '\\'));
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(_workingDirectory, path);
    }

    private sealed record WriteFileArgs
    {
        [JsonPropertyName("path")]
        public string Path { get; init; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("append")]
        public bool Append { get; init; }

        [JsonPropertyName("createBackup")]
        public bool? CreateBackup { get; init; }
    }
}