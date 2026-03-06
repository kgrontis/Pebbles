namespace Pebbles.Plugins;

/// <summary>
/// Session information passed to plugin command handlers.
/// </summary>
public sealed class PluginSession
{
    /// <summary>
    /// Current model name.
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// Total input tokens used in this session.
    /// </summary>
    public int TotalInputTokens { get; init; }

    /// <summary>
    /// Total output tokens used in this session.
    /// </summary>
    public int TotalOutputTokens { get; init; }

    /// <summary>
    /// Estimated cost in dollars.
    /// </summary>
    public decimal TotalCost { get; init; }
}