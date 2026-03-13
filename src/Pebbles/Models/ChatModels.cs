using System.Collections.ObjectModel;

namespace Pebbles.Models;

public enum ChatRole
{
    User,
    Assistant,
    System,
    Tool
}

public enum MessageStatus
{
    Pending,
    Streaming,
    Complete
}

/// <summary>
/// Represents a thinking block in an AI response.
/// </summary>
public record ThinkingBlock
{
    public string Content { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool IsCollapsed { get; init; } = true;
}

/// <summary>
/// Represents a single message in the chat conversation.
/// </summary>
public record ChatMessage
{
    public ChatRole Role { get; init; }
    public string Content { get; init; } = string.Empty;
    public ThinkingBlock? Thinking { get; init; }
    public MessageStatus Status { get; init; } = MessageStatus.Complete;
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public int TokenCount { get; init; }

    /// <summary>
    /// Creates a user message.
    /// </summary>
    public static ChatMessage User(string content, int tokenCount) => new()
    {
        Role = ChatRole.User,
        Content = content,
        TokenCount = tokenCount
    };

    /// <summary>
    /// Creates an assistant message.
    /// </summary>
    public static ChatMessage Assistant(string content, int tokenCount, ThinkingBlock? thinking = null) => new()
    {
        Role = ChatRole.Assistant,
        Content = content,
        TokenCount = tokenCount,
        Thinking = thinking
    };
}

/// <summary>
/// Represents the current chat session state.
/// </summary>
public class ChatSession
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..8];
    public string Model { get; set; } = "qwen3.5-plus";
    public Collection<ChatMessage> Messages { get; } = [];
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }

    /// <summary>
    /// Compression statistics for this session.
    /// </summary>
    public CompressionStats CompressionStats { get; } = new();

    /// <summary>
    /// Whether auto-compression is enabled for this session.
    /// </summary>
    public bool AutoCompressionEnabled { get; set; } = true;

    /// <summary>
    /// Whether a compression operation is currently in progress.
    /// </summary>
    public bool IsCompressing { get; set; }

    public double TotalCost =>
        (TotalInputTokens * 0.003 + TotalOutputTokens * 0.015) / 1000.0;

    /// <summary>
    /// Creates a new session with the specified model.
    /// </summary>
    public static ChatSession Create(string model) => new() { Model = model };
}