namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Interface for session storage and retrieval.
/// </summary>
internal interface ISessionStore
{
    /// <summary>
    /// Saves a session to persistent storage.
    /// </summary>
    Task SaveSessionAsync(ChatSession session);
    
    /// <summary>
    /// Loads a session by ID.
    /// </summary>
    Task<ChatSession?> LoadSessionAsync(string sessionId);
    
    /// <summary>
    /// Lists all session IDs.
    /// </summary>
    Task<IEnumerable<string>> ListSessionIdsAsync();
    
    /// <summary>
    /// Deletes a session.
    /// </summary>
    Task DeleteSessionAsync(string sessionId);
    
    /// <summary>
    /// Creates a new session.
    /// </summary>
    Task<ChatSession> CreateNewSessionAsync(string model);
    
    /// <summary>
    /// Gets the last active session ID.
    /// </summary>
    Task<string?> GetLastActiveSessionIdAsync();
    
    /// <summary>
    /// Sets the last active session ID.
    /// </summary>
    Task SetLastActiveSessionIdAsync(string sessionId);
}
