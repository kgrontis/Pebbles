namespace Pebbles.Tests.Services;

using Pebbles.Models;
using Pebbles.Services;

#pragma warning disable CA2007 // xUnit recommends not using ConfigureAwait in tests

public class SessionStoreTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly TestableSessionStore _store;
    private bool _disposed;

    public SessionStoreTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"pebbles_session_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);
        _store = new TestableSessionStore(_testDirectory);
    }

    [Fact]
    public async Task SaveSessionAsync_CreatesFile()
    {
        // Arrange
        var session = ChatSession.Create("test-model");
        session.Messages.Add(ChatMessage.User("Hello", 5));

        // Act
        await _store.SaveSessionAsync(session);

        // Assert
        var expectedPath = Path.Combine(_testDirectory, "sessions", $"{session.Id}.json");
        Assert.True(File.Exists(expectedPath));
    }

    [Fact]
    public async Task LoadSessionAsync_ReturnsSession_WhenExists()
    {
        // Arrange
        var session = ChatSession.Create("test-model");
        await _store.SaveSessionAsync(session);

        // Act
        var loaded = await _store.LoadSessionAsync(session.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("test-model", loaded.Model);
    }

    [Fact]
    public async Task LoadSessionAsync_ReturnsNull_WhenNotExists()
    {
        // Act
        var result = await _store.LoadSessionAsync("nonexistent-id");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ListSessionIdsAsync_ReturnsEmpty_WhenNoSessions()
    {
        // Act
        var ids = await _store.ListSessionIdsAsync();

        // Assert
        Assert.Empty(ids);
    }

    [Fact]
    public async Task ListSessionIdsAsync_ReturnsAllSessionIds()
    {
        // Arrange
        var session1 = ChatSession.Create("model-1");
        var session2 = ChatSession.Create("model-2");
        await _store.SaveSessionAsync(session1);
        await Task.Delay(100); // Ensure different timestamps
        await _store.SaveSessionAsync(session2);

        // Act
        var ids = await _store.ListSessionIdsAsync();

        // Assert
        Assert.Equal(2, ids.Count());
    }

    [Fact]
    public async Task ListSessionIdsAsync_ReturnsMostRecentFirst()
    {
        // Arrange
        var session1 = ChatSession.Create("model-1");
        await _store.SaveSessionAsync(session1);
        await Task.Delay(100);
        var session2 = ChatSession.Create("model-2");
        await _store.SaveSessionAsync(session2);

        // Act
        var ids = (await _store.ListSessionIdsAsync()).ToList();

        // Assert
        Assert.Equal(session2.Id, ids.First());
        Assert.Equal(session1.Id, ids.Last());
    }

    [Fact]
    public async Task DeleteSessionAsync_RemovesFile()
    {
        // Arrange
        var session = ChatSession.Create("test-model");
        await _store.SaveSessionAsync(session);

        // Act
        await _store.DeleteSessionAsync(session.Id);

        // Assert
        var loaded = await _store.LoadSessionAsync(session.Id);
        Assert.Null(loaded);
    }

    [Fact]
    public async Task DeleteSessionAsync_DoesNotThrow_WhenNotExists()
    {
        // Act & Assert - Should not throw
        await _store.DeleteSessionAsync("nonexistent-id");
    }

    [Fact]
    public async Task CreateNewSessionAsync_CreatesSessionWithModel()
    {
        // Act
        var session = await _store.CreateNewSessionAsync("gpt-4");

        // Assert
        Assert.NotNull(session);
        Assert.Equal("gpt-4", session.Model);
        Assert.NotEmpty(session.Id);
    }

    [Fact]
    public async Task GetLastActiveSessionIdAsync_ReturnsNull_WhenNotSet()
    {
        // Act
        var result = await _store.GetLastActiveSessionIdAsync();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetLastActiveSessionIdAsync_SavesId()
    {
        // Act
        await _store.SetLastActiveSessionIdAsync("test-session-id");

        // Assert
        var result = await _store.GetLastActiveSessionIdAsync();
        Assert.Equal("test-session-id", result);
    }

    [Fact]
    public async Task SaveAndLoad_PreservesModelAndTokens()
    {
        // Arrange
        var session = ChatSession.Create("test-model");
        session.TotalInputTokens = 5;
        session.TotalOutputTokens = 10;

        // Act
        await _store.SaveSessionAsync(session);
        var loaded = await _store.LoadSessionAsync(session.Id);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal("test-model", loaded.Model);
        Assert.Equal(5, loaded.TotalInputTokens);
        Assert.Equal(10, loaded.TotalOutputTokens);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing && Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, recursive: true);
        }

        _disposed = true;
    }
}

/// <summary>
/// Testable SessionStore that uses a custom directory.
/// </summary>
internal sealed class TestableSessionStore : ISessionStore
{
    private readonly string _sessionsDirectory;
    private readonly string _lastActiveFile;
    private readonly System.Text.Json.JsonSerializerOptions _jsonOptions;

    public TestableSessionStore(string baseDirectory)
    {
        _sessionsDirectory = Path.Combine(baseDirectory, "sessions");
        _lastActiveFile = Path.Combine(baseDirectory, "last_session.txt");
        Directory.CreateDirectory(_sessionsDirectory);

        _jsonOptions = new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        var filePath = GetSessionFilePath(session.Id);
        var json = System.Text.Json.JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    public async Task<ChatSession?> LoadSessionAsync(string sessionId)
    {
        var filePath = GetSessionFilePath(sessionId);
        if (!File.Exists(filePath))
            return null;

        var json = await File.ReadAllTextAsync(filePath);
        return System.Text.Json.JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
    }

    public Task<IEnumerable<string>> ListSessionIdsAsync()
    {
        var files = Directory.GetFiles(_sessionsDirectory, "*.json");
        var sessions = files
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => Path.GetFileNameWithoutExtension(f.Name));

        return Task.FromResult(sessions);
    }

    public Task DeleteSessionAsync(string sessionId)
    {
        var filePath = GetSessionFilePath(sessionId);
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public Task<ChatSession> CreateNewSessionAsync(string model)
    {
        return Task.FromResult(ChatSession.Create(model));
    }

    public async Task<string?> GetLastActiveSessionIdAsync()
    {
        if (!File.Exists(_lastActiveFile))
            return null;

        return await File.ReadAllTextAsync(_lastActiveFile).ConfigureAwait(false);
    }

    public async Task SetLastActiveSessionIdAsync(string sessionId)
    {
        await File.WriteAllTextAsync(_lastActiveFile, sessionId).ConfigureAwait(false);
    }

    private string GetSessionFilePath(string sessionId) => Path.Combine(_sessionsDirectory, $"{sessionId}.json");
}