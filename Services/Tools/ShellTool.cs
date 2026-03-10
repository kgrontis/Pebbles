namespace Pebbles.Services.Tools;

using Pebbles.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Tool to execute shell commands safely.
/// </summary>
public sealed class ShellTool : ITool
{
    private readonly string _workingDirectory;

    public ShellTool()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
    }

    public string Name => "run_command";

    public string Description =>
        "Executes a shell command and returns the output. " +
        "Use for build operations (dotnet build, dotnet test), " +
        "git operations (git status, git commit), file operations (mkdir, rm, cp), " +
        "and other system commands. " +
        "Commands execute in the project root directory. " +
        "Timeout is 60 seconds by default.";

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
                        ["command"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "The shell command to execute. Examples: 'dotnet build', 'git status', 'mkdir NewFolder'"
                        },
                        ["timeout"] = new ToolParameterProperty
                        {
                            Type = "number",
                            Description = "Optional: Timeout in seconds (default: 60, max: 300)"
                        },
                        ["workingDirectory"] = new ToolParameterProperty
                        {
                            Type = "string",
                            Description = "Optional: Working directory for command execution (default: project root)"
                        }
                    },
                    Required = ["command"]
                }
            }
        };
    }

    public async Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default)
    {
        var args = JsonSerializer.Deserialize<ShellArgs>(arguments);
        if (args is null || string.IsNullOrWhiteSpace(args.Command))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = "Invalid arguments: 'command' is required"
            };
        }

        // Validate command for safety
        var validationError = ValidateCommand(args.Command);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = validationError
            };
        }

        // Determine timeout (default 60s, max 300s)
        var timeout = Math.Min(Math.Max(args.Timeout ?? 60, 1), 300) * 1000;

        // Determine working directory
        var workDir = string.IsNullOrWhiteSpace(args.WorkingDirectory)
            ? _workingDirectory
            : Path.GetFullPath(args.WorkingDirectory);

        if (!Directory.Exists(workDir))
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Working directory does not exist: {workDir}"
            };
        }

        try
        {
            var policy = RetryPolicies.GetShellCommandPolicy();
            var output = await policy.ExecuteAsync(
                async ct => await ExecuteCommandAsync(args.Command, workDir, timeout, ct),
                cancellationToken);

            return new ToolExecutionResult
            {
                Success = true,
                Content = output
            };
        }
        catch (OperationCanceledException)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Command timed out after {timeout / 1000} seconds"
            };
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult
            {
                Success = false,
                Error = $"Command execution failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Validates the command for safety.
    /// </summary>
    private static string? ValidateCommand(string command)
    {
        // Block dangerous commands
        var dangerousCommands = new[]
        {
            "rm -rf /",
            "rm -rf *",
            "del /f /s /q",
            "format",
            "mkfs",
            "dd if=/dev/zero",
            ":(){:|:&};:",
            "chmod -R 777 /",
            "chown -R"
        };

        foreach (var dangerous in dangerousCommands)
        {
            if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
            {
                return $"Dangerous command detected: '{dangerous}'. This command is blocked for safety.";
            }
        }

        // Block interactive commands
        var interactiveCommands = new[]
        {
            "sudo ",
            "su ",
            "passwd ",
            "ssh ",
            "telnet ",
            "ftp ",
            "nc ",
            "netcat "
        };

        foreach (var interactive in interactiveCommands)
        {
            if (command.StartsWith(interactive, StringComparison.OrdinalIgnoreCase))
            {
                return $"Interactive command '{interactive}' is not supported. Use non-interactive commands only.";
            }
        }

        return null;
    }

    /// <summary>
    /// Executes a shell command asynchronously.
    /// </summary>
    private static async Task<string> ExecuteCommandAsync(string command, string workingDirectory, int timeoutMs, CancellationToken cancellationToken)
    {
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var fileName = isWindows ? "cmd.exe" : "/bin/bash";
        var arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"";

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        process.Start();

        // Wait for completion or timeout
        var completed = await Task.Run(() =>
        {
            return process.WaitForExit(timeoutMs);
        }, cancellationToken);

        if (!completed)
        {
            process.Kill();
            throw new OperationCanceledException($"Command timed out after {timeoutMs}ms");
        }

        var output = await outputTask;
        var error = await errorTask;

        // Combine output and error
        var result = new StringBuilder();
        if (!string.IsNullOrEmpty(output))
            result.AppendLine(output.Trim());
        if (!string.IsNullOrEmpty(error))
            result.AppendLine($"ERROR: {error.Trim()}");

        // Include exit code
        if (process.ExitCode != 0)
            result.AppendLine($"\nExit code: {process.ExitCode}");

        return result.ToString().Trim();
    }

    private record ShellArgs
    {
        [JsonPropertyName("command")]
        public string Command { get; init; } = string.Empty;

        [JsonPropertyName("timeout")]
        public int? Timeout { get; init; }

        [JsonPropertyName("workingDirectory")]
        public string? WorkingDirectory { get; init; }
    }
}