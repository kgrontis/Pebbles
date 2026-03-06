namespace Pebbles.Services;

using System.Diagnostics;
using MoonSharp.Interpreter;

/// <summary>
/// Manages the Lua runtime and provides global functions for extensions.
/// </summary>
public sealed class LuaExtensionService
{
    private readonly string _workingDirectory;

    public LuaExtensionService()
    {
        _workingDirectory = Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Creates a new MoonSharp script instance with global functions registered.
    /// </summary>
    public Script CreateScript()
    {
        var script = new Script(CoreModules.Basic | CoreModules.String | CoreModules.Table | CoreModules.Math);

        // Register global functions
        script.Globals["shell"] = (Func<string, int, string>)ShellExecute;
        script.Globals["shell_simple"] = (Func<string, string>)ShellExecuteSimple;
        script.Globals["read_file"] = (Func<string, string?>)ReadFile;
        script.Globals["write_file"] = (Func<string, string, bool>)WriteFile;
        script.Globals["file_exists"] = (Func<string, bool>)FileExists;
        script.Globals["list_dir"] = (Func<string, string[]?>)ListDirectory;
        script.Globals["get_cwd"] = (Func<string>)GetCurrentDirectory;
        script.Globals["env"] = (Func<string, string?>)GetEnvironmentVariable;
        script.Globals["print_line"] = (Action<string>)PrintLine;
        script.Globals["format_size"] = (Func<long, string>)FormatSize;

        return script;
    }

    /// <summary>
    /// Execute a shell command and return the output.
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="timeoutMs">Timeout in milliseconds (default 30000)</param>
    /// <returns>Command output (stdout + stderr)</returns>
    public string ShellExecute(string command, int timeoutMs = 30000)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "Error: Empty command";

        try
        {
            var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
            var fileName = isWindows ? "cmd.exe" : "/bin/sh";
            var arguments = isWindows ? $"/c {command}" : $"-c \"{command}\"";

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = _workingDirectory
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
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Simple shell execute with default timeout.
    /// </summary>
    public string ShellExecuteSimple(string command) => ShellExecute(command, 30000);

    /// <summary>
    /// Read the contents of a file.
    /// </summary>
    public string? ReadFile(string path)
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
    /// Write content to a file.
    /// </summary>
    public bool WriteFile(string path, string content)
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
    /// Check if a file exists.
    /// </summary>
    public bool FileExists(string path) => File.Exists(ResolvePath(path));

    /// <summary>
    /// List files in a directory.
    /// </summary>
    public string[]? ListDirectory(string path)
    {
        try
        {
            var fullPath = ResolvePath(path);
            if (!Directory.Exists(fullPath))
                return null;

            return Directory.GetFileSystemEntries(fullPath)
                .Select(Path.GetFileName)
                .Where(n => n is not null)
                .Cast<string>()
                .ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get the current working directory.
    /// </summary>
    public string GetCurrentDirectory() => _workingDirectory;

    /// <summary>
    /// Get an environment variable.
    /// </summary>
    public string? GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

    /// <summary>
    /// Print a line (for debugging/testing).
    /// </summary>
    public void PrintLine(string message) => Console.WriteLine(message);

    /// <summary>
    /// Format a byte size as human readable.
    /// </summary>
    public static string FormatSize(long bytes) =>
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
    private static string ResolvePath(string path)
    {
        if (path.StartsWith('~'))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, path[2..]);
        }

        return Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
    }
}