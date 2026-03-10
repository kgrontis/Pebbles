namespace Pebbles.Models;

/// <summary>
/// Mock response for testing purposes.
/// </summary>
public class MockResponse
{
    public List<string> Keywords { get; init; } = [];
    public string Thinking { get; init; } = string.Empty;
    public TimeSpan ThinkingDuration { get; init; }
    public string Content { get; init; } = string.Empty;
}