namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Interface for extension loading.
/// </summary>
public interface IExtensionLoader
{
    /// <summary>
    /// Currently loaded extensions.
    /// </summary>
    IReadOnlyList<LuaExtension> Extensions { get; }

    /// <summary>
    /// Load extensions from global and project directories.
    /// </summary>
    ExtensionLoadResult LoadExtensions();

    /// <summary>
    /// Get all commands from loaded extensions.
    /// </summary>
    IEnumerable<SlashCommand> GetExtensionCommands();
}