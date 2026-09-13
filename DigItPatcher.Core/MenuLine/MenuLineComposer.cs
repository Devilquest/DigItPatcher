using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.MenuLine;

/// <summary>The two files a menu line composition rewrites, ready to be written over an install.</summary>
public sealed record MenuLineComposition(byte[] Archive, byte[] MainExe);

/// <summary>Why a composition was not attempted, naming the piece the guard could not verify.</summary>
public sealed record MenuLineRefusal(string Reason);

/// <summary>The one thing a run calls: verifies the pieces, composes the line, grows the strip, writes neither
/// file itself.</summary>
public static class MenuLineComposer
{
    internal const int ScreenHeight = 200;
    internal const int Margin = 2;

    // Below a y immediate of 120 the strip's top rows stay visible on the sub-menus instead of walking off screen.
    private const int AnchorFloor = 120;

    /// <summary>Composes the main menu's own line out of the patch's version and the active mods' names, or refuses.</summary>
    public static bool TryCompose(GameInstall install, IReadOnlyList<string> modNames,
        out MenuLineComposition? composition, out MenuLineRefusal? refusal)
    {
        composition = null;

        if (!MenuLineSources.Verify(install, out var sources, out var reason))
        {
            refusal = new MenuLineRefusal(reason!);
            return false;
        }

        var cell = MenuLineStrip.ReadCell(sources!.Strip);
        var ink = MenuLineFont.ReadInk(sources.Strip, cell.Width, MenuLineStrip.OriginalHeight);
        var font = MenuLineFont.Glyphs(ink);
        var version = PatchVersionText.Embedded().Version;
        var line = MenuLineText.Embedded().Compose(version, modNames);

        MenuLineStrip.Composition grown;
        try
        {
            grown = MenuLineStrip.Compose(sources.Strip, font, ink.Background, [line]);
        }
        catch (InvalidOperationException ex)
        {
            // A line too wide, or a character with no glyph, is only measurable once it is laid out, one
            // step past the guard.
            refusal = new MenuLineRefusal(ex.Message);
            return false;
        }

        int anchor = ScreenHeight - grown.Rows - Margin;
        if (anchor < AnchorFloor)
        {
            refusal = new MenuLineRefusal(
                $"a {grown.Rows}-row line would rest at row {anchor}, below the floor of {AnchorFloor} " +
                "that keeps it off the sub-menus");
            return false;
        }

        var (archive, mainExe) = MenuLineWriter.Build(sources, grown.Strip, anchor);
        composition = new MenuLineComposition(archive, mainExe);
        refusal = null;
        return true;
    }
}
