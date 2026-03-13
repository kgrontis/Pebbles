namespace Pebbles.Models;

/// <summary>
/// Specifies how to handle session resumption on startup.
/// </summary>
public enum SessionResumeMode
{
    /// <summary>
    /// Default behavior - load last active session if available.
    /// </summary>
    Default,

    /// <summary>
    /// Continue the most recent session (-c/--continue).
    /// </summary>
    Continue,

    /// <summary>
    /// Show a selection prompt to choose a session (-r/--resume).
    /// </summary>
    Select,

    /// <summary>
    /// Start a fresh session, ignoring any saved sessions.
    /// </summary>
    New
}

/// <summary>
/// Options for session resumption.
/// </summary>
public record SessionResumeOptions
{
    /// <summary>
    /// The resume mode to use.
    /// </summary>
    public SessionResumeMode Mode { get; init; } = SessionResumeMode.Default;

    /// <summary>
    /// Specific session ID to load (if provided via CLI).
    /// </summary>
    public string? SessionId { get; init; }
}