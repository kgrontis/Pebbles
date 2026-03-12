using System.Collections.ObjectModel;

namespace Pebbles.Models;

/// <summary>
/// Mock response for testing purposes.
/// </summary>
internal class MockResponse
{
    public Collection<string> Keywords { get; init; } = [];
    public string Thinking { get; init; } = string.Empty;
    public TimeSpan ThinkingDuration { get; init; }
    public string Content { get; init; } = string.Empty;
}