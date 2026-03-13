namespace Pebbles.Services;

using Pebbles.Models;
using System.Text.Json;

/// <summary>
/// File-based session storage implementation.
/// </summary>
public sealed class SessionStore : ISessionStore
{
    private readonly string _sessionsDirectory;
    private readonly string _lastActiveFile;
    private readonly JsonSerializerOptions _jsonOptions;

    public SessionStore()
    {
        var pebblesDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".pebbles");
        
        _sessionsDirectory = Path.Combine(pebblesDir, "sessions");
        _lastActiveFile = Path.Combine(pebblesDir, "last_session.txt");
        
        Directory.CreateDirectory(_sessionsDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task SaveSessionAsync(ChatSession session)
    {
        var filePath = GetSessionFilePath(session.Id);
        var json = JsonSerializer.Serialize(session, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    public async Task<ChatSession?> LoadSessionAsync(string sessionId)
    {
        var filePath = GetSessionFilePath(sessionId);
        if (!File.Exists(filePath))
            return null;
        
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ChatSession>(json, _jsonOptions);
    }

    public async Task<IEnumerable<string>> ListSessionIdsAsync()
    {
        var files = Directory.GetFiles(_sessionsDirectory, "*.json");
        var sessions = new List<(string Id, DateTime LastModified)>();
        
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            var id = Path.GetFileNameWithoutExtension(info.Name);
            sessions.Add((id, info.LastWriteTimeUtc));
        }
        
        return sessions
            .OrderByDescending(s => s.LastModified)
            .Select(s => s.Id);
    }

    public async Task DeleteSessionAsync(string sessionId)
    {
        var filePath = GetSessionFilePath(sessionId);
        if (File.Exists(filePath))
        {
            await Task.Run(() => File.Delete(filePath)).ConfigureAwait(false);
        }
    }

    public Task<ChatSession> CreateNewSessionAsync(string model)
    {
        var session = ChatSession.Create(model);
        return Task.FromResult(session);
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

    private string GetSessionFilePath(string sessionId)
    {
        return Path.Combine(_sessionsDirectory, $"{sessionId}.json");
    }
}
