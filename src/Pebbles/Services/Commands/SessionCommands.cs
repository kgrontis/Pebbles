namespace Pebbles.Services.Commands;

using Pebbles.Models;
using Pebbles.Services;
using Spectre.Console;
using System.Security;

/// <summary>
/// Handles session-related commands: /save, /load, /sessions, /delete.
/// </summary>
public sealed class SessionCommands(ISessionStore sessionStore)
{
    public async Task<CommandResult> HandleSave(ChatSession session)
    {
        try
        {
            await sessionStore.SaveSessionAsync(session).ConfigureAwait(false);
            await sessionStore.SetLastActiveSessionIdAsync(session.Id).ConfigureAwait(false);
            
            return CommandResult.OkWithMarkup($$"""
                [bold green]✓[/] Session saved
                
                  ID: [dim]{{session.Id}}[/]
                  Messages: [dim]{{session.Messages.Count}}[/]
                  Model: [dim]{{session.Model}}[/]
                """);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            return CommandResult.Fail($"Failed to save session: {ex.Message}");
        }
    }

    public async Task<CommandResult> HandleLoad(string sessionId, ChatSession currentSession)
    {
        try
        {
            var loadedSession = await sessionStore.LoadSessionAsync(sessionId).ConfigureAwait(false);
            
            if (loadedSession is null)
            {
                return CommandResult.Fail($"Session '{sessionId}' not found");
            }
            
            // Copy messages from loaded session to current
            currentSession.Messages.Clear();
            foreach (var msg in loadedSession.Messages)
            {
                currentSession.Messages.Add(msg);
            }
            currentSession.Model = loadedSession.Model;
            currentSession.TotalInputTokens = loadedSession.TotalInputTokens;
            currentSession.TotalOutputTokens = loadedSession.TotalOutputTokens;
            
            await sessionStore.SetLastActiveSessionIdAsync(sessionId).ConfigureAwait(false);
            
            return CommandResult.OkWithMarkup($$"""
                [bold green]✓[/] Session loaded
                
                  ID: [dim]{{sessionId}}[/]
                  Messages: [dim]{{currentSession.Messages.Count}}[/]
                  Model: [dim]{{currentSession.Model}}[/]
                """);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            return CommandResult.Fail($"Failed to load session: {ex.Message}");
        }
    }

    public async Task<CommandResult> HandleSessions()
    {
        try
        {
            var sessionIds = await sessionStore.ListSessionIdsAsync().ConfigureAwait(false);
            var lastActive = await sessionStore.GetLastActiveSessionIdAsync().ConfigureAwait(false);
            
            if (!sessionIds.Any())
            {
                return CommandResult.OkWithMarkup("""
                    [dim]No saved sessions yet.[/]
                    
                    Use [bold]/save[/] to save the current session.
                    """);
            }
            
            var lines = new List<string>
            {
                "",
                "[bold]Saved Sessions[/]",
                ""
            };
            
            foreach (var id in sessionIds)
            {
                var marker = id == lastActive ? "[green]●[/]" : " ";
                lines.Add($"  {marker} [dim]{id}[/]");
            }
            
            lines.Add("");
            lines.Add("[dim]Use /load <id> to load a session, /delete <id> to remove.[/]");
            
            return CommandResult.OkWithMarkup(string.Join("\n", lines));
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            return CommandResult.Fail($"Failed to list sessions: {ex.Message}");
        }
    }

    public async Task<CommandResult> HandleDelete(string sessionId)
    {
        try
        {
            var session = await sessionStore.LoadSessionAsync(sessionId).ConfigureAwait(false);
            if (session is null)
            {
                return CommandResult.Fail($"Session '{sessionId}' not found");
            }
            
            await sessionStore.DeleteSessionAsync(sessionId).ConfigureAwait(false);
            
            return CommandResult.OkWithMarkup($$"""
                [bold green]✓[/] Session deleted
                
                  ID: [dim]{{sessionId}}[/]
                  Messages: [dim]{{session.Messages.Count}}[/]
                """);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException || ex is NotSupportedException || ex is SecurityException)
        {
            return CommandResult.Fail($"Failed to delete session: {ex.Message}");
        }
    }
}
