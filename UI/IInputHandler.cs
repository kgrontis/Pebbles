namespace Pebbles.UI;

using Pebbles.Models;
using Pebbles.Services;

/// <summary>
/// Handles user input with history and autocomplete.
/// </summary>
public interface IInputHandler
{
    /// <summary>
    /// Reads user input, returns null to exit.
    /// </summary>
    string? ReadInput(ChatSession session);
}

/// <summary>
/// Suggestion item for autocomplete.
/// </summary>
public interface ISuggestion
{
    string DisplayText { get; }
    string InsertText { get; }
    string Description { get; }
    bool IsDirectory { get; }
}

/// <summary>
/// Command suggestion wrapper.
/// </summary>
public class CommandSuggestion : ISuggestion
{
    private readonly SlashCommand _command;

    public CommandSuggestion(SlashCommand command) => _command = command;

    public string DisplayText => _command.Name;
    public string InsertText => _command.Name;
    public string Description => _command.Description;
    public bool IsDirectory => false;
}

/// <summary>
/// File/folder suggestion wrapper.
/// </summary>
public class FileSuggestion : ISuggestion
{
    private readonly FileItem _item;

    public FileSuggestion(FileItem item) => _item = item;

    public string DisplayText => _item.IsDirectory ? $"{_item.Name}/" : _item.Name;
    public string InsertText => _item.Path + (_item.IsDirectory ? "/" : "");
    public string Description => _item.IsDirectory ? "folder" : _item.Extension;
    public bool IsDirectory => _item.IsDirectory;
}