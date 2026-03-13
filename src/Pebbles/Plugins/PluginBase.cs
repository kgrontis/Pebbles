namespace Pebbles.Plugins;

/// <summary>
/// Base class for C# plugins. Inherit from this class to create a plugin.
/// </summary>
public abstract class PluginBase
{
    /// <summary>
    /// Plugin identifier (e.g., "my-tools").
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Plugin version (e.g., "1.0.0").
    /// </summary>
    public virtual string Version => "1.0.0";

    /// <summary>
    /// Short description of the plugin.
    /// </summary>
    public virtual string Description => string.Empty;

    /// <summary>
    /// Return the commands provided by this plugin.
    /// </summary>
    public abstract IEnumerable<PluginCommand> GetCommands();

    /// <summary>
    /// Execute a shell command and return the output.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 30000)</param>
    /// <returns>Command output (stdout + stderr)</returns>
    protected static string Shell(string command, int timeoutMs = 30000)
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

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            var completed = process.WaitForExit(timeoutMs);

            if (!completed)
            {
                process.Kill();
                return $"Error: Command timed out after {timeoutMs}ms";
            }

            return string.IsNullOrEmpty(error) ? output.Trim() : $"Error: {error.Trim()}\n{output.Trim()}";
        }
        catch (Exception ex) when (ex is not System.ComponentModel.Win32Exception) // Don't catch Win32Exception which can occur on process kill
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Read the contents of a file.
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
        catch (Exception ex) when (ex is not System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Write content to a file.
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
        catch (Exception ex) when (ex is not System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Check if a file exists.
    /// </summary>
    protected static bool FileExists(string path) => File.Exists(ResolvePath(path));

    /// <summary>
    /// List files in a directory.
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
        catch (Exception ex) when (ex is not System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current working directory.
    /// </summary>
    protected static string GetWorkingDirectory() => Directory.GetCurrentDirectory();

    /// <summary>
    /// Get an environment variable.
    /// </summary>
    protected static string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>
    /// Format a byte size as human readable.
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
    /// Resolve a path (handle ~ for home directory and relative paths).
    /// </summary>
    protected static string ResolvePath(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}