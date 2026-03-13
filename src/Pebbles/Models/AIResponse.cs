using System.Collections.ObjectModel;

namespace Pebbles.Models;

/// <summary>
/// Response from AI that may include tool calls.
/// </summary>
public record AIResponse
{
    public string Content { get; init; } = string.Empty;
    public Collection<ToolCall> ToolCalls { get; init; } = [];
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public string? Thinking { get; init; }

    /// <summary>
    /// Tokens used for reasoning/thinking (from completion_tokens_details.reasoning_tokens).
    /// </summary>
    public int ReasoningTokens { get; init; }

    /// <summary>
    /// Tokens read from cache (from prompt_tokens_details.cached_tokens).
    /// </summary>
    public int CachedTokens { get; init; }
}

/// <summary>
/// Streaming response chunk that can be either a token or a final response with tool calls.
/// </summary>
public record StreamingToolResponse
{
    /// <summary>
    /// Token content (prefixed with [THINKING] for thinking content).
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Final response with tool calls (set only on the last chunk).
    /// </summary>
    public AIResponse? FinalResponse { get; init; }

    /// <summary>
    /// Creates a token chunk.
    /// </summary>
    public static StreamingToolResponse FromToken(string token) => new() { Token = token };

    /// <summary>
    /// Creates a final response chunk.
    /// </summary>
    public static StreamingToolResponse FromResponse(AIResponse response) => new() { FinalResponse = response };
}