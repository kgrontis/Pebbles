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
    Task RunAsync();
}