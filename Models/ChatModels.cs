namespace Pebbles.Models;

public enum ChatRole
{
    User,
    Assistant,
    System
}

public enum MessageStatus
{
    Pending,
    Streaming,
    Complete
}

public class ThinkingBlock
{
    public string Content { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool IsCollapsed { get; set; } = true;
}

public class ChatMessage
{
    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public ThinkingBlock? Thinking { get; set; }
    public MessageStatus Status { get; set; } = MessageStatus.Complete;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int TokenCount { get; set; }
}

public class ChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Model { get; set; } = "pebbles-3.5-sonnet";
    public List<ChatMessage> Messages { get; } = [];
    public bool CompactMode { get; set; }
    public int TotalInputTokens { get; set; }
    public int TotalOutputTokens { get; set; }
    public double TotalCost => (TotalInputTokens * 0.003 + TotalOutputTokens * 0.015) / 1000.0;
}
