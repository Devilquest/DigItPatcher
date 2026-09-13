using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.MenuLine;

/// <summary>The pieces the menu line is composed from, once the strip and the blit site have both verified intact.</summary>
internal sealed record MenuLineSources(
    byte[] Archive,
    IReadOnlyList<XrsEntry> Entries,
    byte[] Strip,
    byte[] MainExe,
    MainExeLayout Layout)
{
    private const string ArchiveName = "DIGIT0.XRS";
    private const string ExeName = "MAIN.EXE";
    private const string StripName = "CR.SPF";

    // The blit whose two immediates this tool edits, addressed within seg3.
    internal const int Site = 0xDF1B;

    private static readonly byte[] SiteBytes =
    [
        0xC6, 0x06, 0x23, 0x7B, 0x00, 0x6A, 0x09, 0x6A, 0x01, 0x6A, 0x01, 0x68, 0xF4, 0x00, 0x6A, 0x07,
        0xB8, 0x26, 0x00, 0x2B, 0x06, 0x76, 0x14, 0x50, 0xA1, 0x76, 0x14, 0xC1, 0xE8, 0x02, 0x05, 0xBA,
        0x00, 0x50
    ];

    /// <summary>Distance from the blit's first byte to each of the two immediates this tool writes.</summary>
    internal const int BottomRowFromSite = 15, AnchorFromSite = 31;

    /// <summary>Locates the strip and the blit site and verifies each structurally, or reports which one did not hold up.</summary>
    public static bool Verify(GameInstall install, out MenuLineSources? sources, out string? reason)
    {
        sources = null;

        if (!install.Has(ArchiveName)) { reason = $"{ArchiveName} is missing"; return false; }
        if (!TryReadWhole(install, ArchiveName, out var archive)) { reason = $"{ArchiveName} could not be read"; return false; }
        if (!XrsDirectory.TryRead(archive, out var entries)) { reason = $"{ArchiveName} does not open as an archive"; return false; }

        var entry = entries.FirstOrDefault(candidate => candidate.Name.Equals(StripName, StringComparison.OrdinalIgnoreCase));
        if (entry is null) { reason = $"{StripName} is missing from {ArchiveName}"; return false; }

        var working = new byte[FrameCodec.WorkingSize];
        if (!SheetChain.Walk(archive.AsSpan(entry.Start, entry.Length), working).IsIntact)
        {
            reason = $"{StripName} did not decode intact";
            return false;
        }

        var (_, chunks) = FrameContainer.Split(archive.AsSpan(entry.Start, entry.Length));
        if (chunks.Count != 1) { reason = $"{StripName} decodes to {chunks.Count} pages, not 1"; return false; }

        var strip = FrameContainer.DecodePages(chunks)[0];
        var cell = MenuLineStrip.ReadCell(strip);
        if (cell.Height < MenuLineStrip.OriginalHeight || (cell.Height - MenuLineStrip.OriginalHeight) % MenuLineStrip.Band != 0)
        {
            reason = $"{StripName} is {cell.Width}x{cell.Height}, not a cell this tool can rebuild from " +
                     $"({MenuLineStrip.OriginalHeight}, plus any multiple of {MenuLineStrip.Band})";
            return false;
        }

        if (!install.Has(ExeName)) { reason = $"{ExeName} is missing"; return false; }
        if (!TryReadWhole(install, ExeName, out var mainExe)) { reason = $"{ExeName} could not be read"; return false; }
        if (!MainExeLayout.TryRead(mainExe, out var layout)) { reason = $"{ExeName} does not carry a segment table"; return false; }
        if (!SiteMatches(mainExe, layout))
        {
            reason = $"{ExeName} does not carry the blit site this tool was measured against";
            return false;
        }

        sources = new MenuLineSources(archive, entries, strip, mainExe, layout);
        reason = null;
        return true;
    }

    // The two bytes this tool itself writes are masked out, so a copy already carrying a line still matches.
    private static bool SiteMatches(byte[] mainExe, MainExeLayout layout)
    {
        int siteAt = layout.Seg3(Site);
        if (mainExe.Length < siteAt + SiteBytes.Length) return false;

        var found = mainExe.AsSpan(siteAt, SiteBytes.Length).ToArray();
        var template = (byte[])SiteBytes.Clone();
        found[BottomRowFromSite] = template[BottomRowFromSite] = 0;
        found[AnchorFromSite] = template[AnchorFromSite] = 0;
        return found.AsSpan().SequenceEqual(template);
    }

    private static bool TryReadWhole(GameInstall install, string fileName, out byte[] bytes)
    {
        try
        {
            bytes = install.ReadAll(fileName);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            bytes = [];
            return false;
        }
    }
}
