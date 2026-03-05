namespace Pebbles.UI;

using Pebbles.Models;

/// <summary>
/// Handles user input with history and autocomplete.
/// </summary>
public interface IInputHandler
{
    /// <summary>
    /// Reads user input, returns null to exit.
    /// </summary>
    string? ReadInput();
}