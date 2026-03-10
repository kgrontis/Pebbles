namespace Pebbles.Plugins;

using Pebbles.Models;
using System.Text.Json;

/// <summary>
/// Base class for tool plugins. Inherit from this class to create a tool plugin.
/// Provides helper methods and common functionality.
/// </summary>
public abstract class ToolPluginBase : IToolPlugin
{
    /// <summary>
    /// Tool identifier (e.g., "my_custom_tool").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Tool version (e.g., "1.0.0").
    /// </summary>
    public virtual string Version => "1.0.0";

    /// <summary>
    /// Short description of what the tool does.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Returns the tool definition (schema) for the AI model.
    /// </summary>
    public abstract ToolDefinition GetDefinition();

    /// <summary>
    /// Executes the tool with the given arguments.
    /// </summary>
    public abstract Task<ToolExecutionResult> ExecuteAsync(string arguments, CancellationToken cancellationToken = default);

    /// <summary>
    /// Helper: Deserialize arguments from JSON.
    /// </summary>
    protected T? DeserializeArgs<T>(string arguments) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(arguments);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper: Execute a shell command and return the output.
    /// </summary>
    protected static async Task<string> ShellAsync(string command, int timeoutMs = 30000)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "Error: Empty command";

        try
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var fileName = isWindows ? "cmd.exe" : "/bin/sh";
            var arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"";

            using var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

            if (!completed)
            {
                process.Kill();
                return $"Error: Command timed out after {timeoutMs}ms";
            }

            var output = await outputTask;
            var error = await errorTask;

            return string.IsNullOrEmpty(error) ? output.Trim() : $"Error: {error.Trim()}\n{output.Trim()}";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Helper: Read the contents of a file.
    /// </summary>
    protected static string? ReadFile(string path)
    {
        try
        {
            var fullPath = ResolvePath(path);
            if (!File.Exists(fullPath))
                return null;

            return File.ReadAllText(fullPath);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper: Write content to a file.
    /// </summary>
    protected static bool WriteFile(string path, string content)
    {
        try
        {
            var fullPath = ResolvePath(path);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Helper: Check if a file exists.
    /// </summary>
    protected static bool FileExists(string path) => File.Exists(ResolvePath(path));

    /// <summary>
    /// Helper: List files in a directory.
    /// </summary>
    protected static string[]? ListDirectory(string path)
    {
        try
        {
            var fullPath = ResolvePath(path);
            if (!Directory.Exists(fullPath))
                return null;

            return [.. Directory.GetFileSystemEntries(fullPath)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Helper: Get an environment variable.
    /// </summary>
    protected static string? GetEnv(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>
    /// Helper: Format a byte size as human readable.
    /// </summary>
    protected static string FormatSize(long bytes) =>
        bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024 * 1024)} MB",
            _ => $"{bytes / (1024 * 1024 * 1024):F1} GB"
        };

    /// <summary>
    /// Helper: Resolve a path (handle ~ for home directory and relative paths).
    /// </summary>
    protected static string ResolvePath(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}