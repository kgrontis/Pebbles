namespace Pebbles.Plugins;

/// <summary>
/// Represents a command provided by a plugin.
/// </summary>
internal sealed class PluginCommand
{
    /// <summary>
    /// Command name (e.g., "/git").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Command description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Usage example.
    /// </summary>
    public string Usage { get; init; } = string.Empty;

    /// <summary>
    /// Handler function that executes the command.
    /// </summary>
    public required Func<string[], PluginSession, string> Handler { get; init; }
}