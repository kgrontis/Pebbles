namespace Pebbles.Models;

/// <summary>
/// Result of a context compression operation.
/// </summary>
internal record CompressionResult
{
    /// <summary>
    /// Whether the compression was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The generated summary of the compressed messages.
    /// </summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>
    /// Token count before compression.
    /// </summary>
    public int TokensBefore { get; init; }

    /// <summary>
    /// Token count after compression.
    /// </summary>
    public int TokensAfter { get; init; }

    /// <summary>
    /// Number of messages that were summarized.
    /// </summary>
    public int MessagesSummarized { get; init; }

    /// <summary>
    /// Number of messages kept verbatim.
    /// </summary>
    public int MessagesKept { get; init; }

    /// <summary>
    /// Error message if compression failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful compression result.
    /// </summary>
    public static CompressionResult Succeeded(string summary, int tokensBefore, int tokensAfter, int summarized, int kept) => new()
    {
        Success = true,
        Summary = summary,
        TokensBefore = tokensBefore,
        TokensAfter = tokensAfter,
        MessagesSummarized = summarized,
        MessagesKept = kept
    };

    /// <summary>
    /// Creates a failed compression result.
    /// </summary>
    public static CompressionResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };

    /// <summary>
    /// Creates a result indicating no compression was needed.
    /// </summary>
    public static CompressionResult NotNeeded() => new()
    {
        Success = true,
        Summary = string.Empty,
        TokensBefore = 0,
        TokensAfter = 0,
        MessagesSummarized = 0,
        MessagesKept = 0
    };
}

/// <summary>
/// Statistics about compression operations in a session.
/// </summary>
internal class CompressionStats
{
    /// <summary>
    /// Total number of compressions performed.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Total tokens saved across all compressions.
    /// </summary>
    public int TotalTokensSaved { get; set; }

    /// <summary>
    /// Timestamp of the last compression.
    /// </summary>
    public DateTime? LastCompressionTime { get; set; }

    /// <summary>
    /// Summary from the last compression (for iterative updates).
    /// </summary>
    public string? LastSummary { get; set; }
}