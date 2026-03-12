namespace Pebbles.Services;

using Pebbles.Models;
using Pebbles.Plugins;

/// <summary>
/// Interface for plugin loading.
/// </summary>
internal interface IPluginLoader
{
    /// <summary>
    /// Currently loaded plugins.
    /// </summary>
    IReadOnlyList<CSharpPlugin> Plugins { get; }

    /// <summary>
    /// Load plugins from global and project directories.
    /// </summary>
    PluginLoadResult LoadPlugins();

    /// <summary>
    /// Get all commands from loaded plugins.
    /// </summary>
    IEnumerable<SlashCommand> GetPluginCommands();
}