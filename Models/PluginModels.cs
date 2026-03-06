namespace Pebbles.Models;

/// <summary>
/// Represents a loaded C# plugin.
/// </summary>
public sealed class CSharpPlugin
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
    /// Path to the C# script file.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// The plugin instance.
    /// </summary>
    public Plugins.PluginBase? Instance { get; set; }
}

/// <summary>
/// Result of loading plugins.
/// </summary>
public sealed class PluginLoadResult
{
    /// <summary>
    /// Successfully loaded plugins.
    /// </summary>
    public List<CSharpPlugin> Plugins { get; set; } = [];

    /// <summary>
    /// Errors encountered during loading.
    /// </summary>
    public List<(string Path, string Error)> Errors { get; set; } = [];

    /// <summary>
    /// Total number of commands loaded.
    /// </summary>
    public int TotalCommands => Plugins.Sum(p => p.Instance?.GetCommands().Count() ?? 0);
}