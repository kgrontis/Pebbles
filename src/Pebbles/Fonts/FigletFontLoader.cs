using System.Reflection;
using Spectre.Console;

namespace Pebbles.Fonts;

/// <summary>
/// Loads FIGlet fonts from embedded resources.
/// </summary>
internal static class FigletFontLoader
{
    private static FigletFont? _slant;

    public static FigletFont Slant => _slant ??= LoadFont("Pebbles.Fonts.slant.flf");

    private static FigletFont LoadFont(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded font not found: {resourceName}");
        return FigletFont.Load(stream);
    }
}