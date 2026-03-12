namespace Pebbles.UI;

using Pebbles.Models;
using Pebbles.Services;

/// <summary>
/// Handles user input with history and autocomplete.
/// </summary>
internal interface IInputHandler
{
    /// <summary>
    /// Reads user input, returns null to exit.
    /// </summary>
    string? ReadInput(ChatSession session);
}

/// <summary>
/// Suggestion item for autocomplete.
/// </summary>
internal interface ISuggestion
{
    string DisplayText { get; }
    string InsertText { get; }
    string Description { get; }
    bool IsDirectory { get; }
}

/// <summary>
/// Command suggestion wrapper.
/// </summary>
internal class CommandSuggestion(SlashCommand command) : ISuggestion
{
    public string DisplayText => command.Name;
    public string InsertText => command.Name;
    public string Description => command.Description;
    public bool IsDirectory => false;
}

/// <summary>
/// File/folder suggestion wrapper.
/// </summary>
internal class FileSuggestion(FileItem item) : ISuggestion
{
    public string DisplayText => item.IsDirectory ? $"{item.Name}/" : item.Name;
    public string InsertText => item.Path + (item.IsDirectory ? "/" : "");
    public string Description => item.IsDirectory ? "folder" : item.Extension;
    public bool IsDirectory => item.IsDirectory;
}