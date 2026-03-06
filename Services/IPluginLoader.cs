namespace Pebbles.Services;

using Pebbles.Models;

/// <summary>
/// Interface for plugin loading.
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Currently loaded plugins.
    /// </summary>
    IReadOnlyList<LuaPlugin> Plugins { get; }

    /// <summary>
    /// Load plugins from global and project directories.
    /// </summary>
    PluginLoadResult LoadPlugins();

    /// <summary>
    /// Get all commands from loaded plugins.
    /// </summary>
    IEnumerable<SlashCommand> GetPluginCommands();
}