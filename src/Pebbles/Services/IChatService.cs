namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Main chat loop orchestration.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Runs the chat application until exit.
    /// </summary>
    /// <param name="resumeOptions">Options for session resumption.</param>
    Task RunAsync(SessionResumeOptions? resumeOptions = null);
}