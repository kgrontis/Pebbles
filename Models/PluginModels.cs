namespace Pebbles.Models;

/// <summary>
/// Represents a loaded Lua plugin.
/// </summary>
public sealed class LuaPlugin
{
    /// <summary>
    /// Plugin name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version.
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Path to the Lua script file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Commands provided by this plugin.
    /// </summary>
    public List<PluginCommand> Commands { get; set; } = [];

    /// <summary>
    /// Hooks provided by this plugin.
    /// </summary>
    public List<PluginHook> Hooks { get; set; } = [];
}

/// <summary>
/// Represents a command from a plugin.
/// </summary>
public sealed class PluginCommand
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
/// Represents a hook from a plugin.
/// </summary>
public sealed class PluginHook
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
/// Result of loading plugins.
/// </summary>
public sealed class PluginLoadResult
{
    /// <summary>
    /// Successfully loaded plugins.
    /// </summary>
    public List<LuaPlugin> Plugins { get; set; } = [];

    /// <summary>
    /// Errors encountered during loading.
    /// </summary>
    public List<(string Path, string Error)> Errors { get; set; } = [];

    /// <summary>
    /// Total number of commands loaded.
    /// </summary>
    public int TotalCommands => Plugins.Sum(e => e.Commands.Count);
}