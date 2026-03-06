namespace Pebbles.Models;

/// <summary>
/// Represents a loaded Lua extension.
/// </summary>
public sealed class LuaExtension
{
    /// <summary>
    /// Extension name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Extension version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Extension description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Path to the Lua script file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Commands provided by this extension.
    /// </summary>
    public List<ExtensionCommand> Commands { get; set; } = [];

    /// <summary>
    /// Hooks provided by this extension.
    /// </summary>
    public List<ExtensionHook> Hooks { get; set; } = [];
}

/// <summary>
/// Represents a command from an extension.
/// </summary>
public sealed class ExtensionCommand
{
    /// <summary>
    /// Command name (e.g., "/git").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Command description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Usage example.
    /// </summary>
    public string Usage { get; set; } = string.Empty;

    /// <summary>
    /// The Lua function that handles this command.
    /// </summary>
    public object? Handler { get; set; }
}

/// <summary>
/// Represents a hook from an extension.
/// </summary>
public sealed class ExtensionHook
{
    /// <summary>
    /// Hook type (on_start, on_before_send, on_after_receive, etc.).
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The Lua function for this hook.
    /// </summary>
    public object? Handler { get; set; }
}

/// <summary>
/// Result of loading extensions.
/// </summary>
public sealed class ExtensionLoadResult
{
    /// <summary>
    /// Successfully loaded extensions.
    /// </summary>
    public List<LuaExtension> Extensions { get; set; } = [];

    /// <summary>
    /// Errors encountered during loading.
    /// </summary>
    public List<(string Path, string Error)> Errors { get; set; } = [];

    /// <summary>
    /// Total number of commands loaded.
    /// </summary>
    public int TotalCommands => Extensions.Sum(e => e.Commands.Count);
}