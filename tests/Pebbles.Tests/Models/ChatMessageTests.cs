namespace Pebbles.Tests.Models;

using Pebbles.Models;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class ChatMessageTests
{
    [Fact]
    public void User_CreatesUserMessage()
    {
        // Act
        var message = ChatMessage.User("Hello", 10);

        // Assert
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("Hello", message.Content);
        Assert.Equal(10, message.TokenCount);
        Assert.Null(message.Thinking);
    }

    [Fact]
    public void Assistant_CreatesAssistantMessage()
    {
        // Act
        var message = ChatMessage.Assistant("Hi there!", 15);

        // Assert
        Assert.Equal(ChatRole.Assistant, message.Role);
        Assert.Equal("Hi there!", message.Content);
        Assert.Equal(15, message.TokenCount);
        Assert.Null(message.Thinking);
    }

    [Fact]
    public void Assistant_WithThinking_CreatesMessageWithThinking()
    {
        // Arrange
        var thinking = new ThinkingBlock
        {
            Content = "Let me think...",
            Duration = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var message = ChatMessage.Assistant("Response", 20, thinking);

        // Assert
        Assert.Equal(ChatRole.Assistant, message.Role);
        Assert.NotNull(message.Thinking);
        Assert.Equal("Let me think...", message.Thinking.Content);
        Assert.Equal(TimeSpan.FromMilliseconds(500), message.Thinking.Duration);
    }

    [Fact]
    public void Message_HasCorrectTimestamp()
    {
        // Arrange
        var before = DateTime.Now;

        // Act
        var message = ChatMessage.User("Test", 5);
        var after = DateTime.Now;

        // Assert
        Assert.True(message.Timestamp >= before);
        Assert.True(message.Timestamp <= after);
    }

    [Fact]
    public void Message_HasDefaultCompleteStatus()
    {
        // Act
        var message = ChatMessage.User("Test", 5);

        // Assert
        Assert.Equal(MessageStatus.Complete, message.Status);
    }
}

public class ThinkingBlockTests
{
    [Fact]
    public void ThinkingBlock_InitializesWithDefaults()
    {
        // Act
        var block = new ThinkingBlock();

        // Assert
        Assert.Equal(string.Empty, block.Content);
        Assert.Equal(TimeSpan.Zero, block.Duration);
    }

    [Fact]
    public void ThinkingBlock_StoresContentAndDuration()
    {
        // Arrange & Act
        var block = new ThinkingBlock
        {
            Content = "Thinking content",
            Duration = TimeSpan.FromSeconds(2.5)
        };

        // Assert
        Assert.Equal("Thinking content", block.Content);
        Assert.Equal(TimeSpan.FromSeconds(2.5), block.Duration);
    }
}