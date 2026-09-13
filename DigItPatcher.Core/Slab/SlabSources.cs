using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.Slab;

/// <summary>The pieces a blank slab is derived from, once every one of them has verified intact.</summary>
internal sealed record SlabSources(
    byte[] Archive,
    IReadOnlyList<XrsEntry> Entries,
    byte[] MainExe,
    MainExeLayout Layout,
    byte[] Art,
    byte[] Donor,
    (byte R, byte G, byte B)[] Palette,
    GameFont Font)
{
    private const string ArchiveName = "DIGIT0.XRS";
    private const string ExeName = "MAIN.EXE";

    private const string InstructionsForeground = "SLB00F.MPF";
    private const string InstructionsPalette = "SLB00.PAL";
    private const string CreditsForeground = "SLB01F.MPF";
    private const string CreditsMask = "SLB01M.MPF";
    private const string CreditsPalette = "SLB01.PAL";

    private const int Pages = 8;

    // DS:0x10: one word per screen, instructions then credits.
    private const int SlabCountTable = 0x10;
    private const int CreditsScreenIndex = 1;

    /// <summary>Locates every piece the derivation needs and verifies it structurally, or reports which
    /// one did not hold up.</summary>
    public static bool Verify(GameInstall install, out SlabSources? sources, out string? reason)
    {
        sources = null;

        if (!install.Has(ArchiveName)) { reason = $"{ArchiveName} is missing"; return false; }
        if (!TryReadWhole(install, ArchiveName, out var archive)) { reason = $"{ArchiveName} could not be read"; return false; }
        if (!XrsDirectory.TryRead(archive, out var entries)) { reason = $"{ArchiveName} does not open as an archive"; return false; }

        string[] needed = [InstructionsForeground, CreditsForeground, CreditsMask, InstructionsPalette, CreditsPalette];
        var located = new Dictionary<string, XrsEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in needed)
        {
            var entry = entries.FirstOrDefault(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (entry is null) { reason = $"{name} is missing from {ArchiveName}"; return false; }
            located[name] = entry;
        }

        var working = new byte[FrameCodec.WorkingSize];
        string[] images = [InstructionsForeground, CreditsForeground, CreditsMask];
        foreach (var name in images)
        {
            var entry = located[name];
            if (!SheetChain.Walk(archive.AsSpan(entry.Start, entry.Length), working).IsIntact)
            {
                reason = $"{name} did not decode intact";
                return false;
            }
        }

        byte[] donor = [], art = [];
        foreach (var name in images)
        {
            var entry = located[name];
            var (_, chunks) = FrameContainer.Split(archive.AsSpan(entry.Start, entry.Length));
            if (chunks.Count != Pages) { reason = $"{name} decodes to {chunks.Count} pages, not {Pages}"; return false; }

            if (name == InstructionsForeground) donor = Concat(FrameContainer.DecodePages(chunks));
            else if (name == CreditsForeground) art = Concat(FrameContainer.DecodePages(chunks));
        }

        var creditsPalette = GamePalette.Read(archive.AsSpan(located[CreditsPalette].Start, located[CreditsPalette].Length));
        var instructionsPalette = GamePalette.Read(archive.AsSpan(located[InstructionsPalette].Start, located[InstructionsPalette].Length));
        if (!creditsPalette.AsSpan().SequenceEqual(instructionsPalette))
        {
            reason = $"{CreditsPalette} and {InstructionsPalette} are not the same palette";
            return false;
        }

        if (!install.Has(ExeName)) { reason = $"{ExeName} is missing"; return false; }
        if (!TryReadWhole(install, ExeName, out var mainExe)) { reason = $"{ExeName} could not be read"; return false; }
        if (!MainExeLayout.TryRead(mainExe, out var layout)) { reason = $"{ExeName} does not carry a segment table"; return false; }
        if (!FontBlobIsIntact(mainExe, layout, working)) { reason = $"{ExeName} does not hold a font at its documented address"; return false; }

        int slabCountAt = layout.DGroup(SlabCountTable + (CreditsScreenIndex * 2));
        if (mainExe.Length < slabCountAt + 2) { reason = $"{ExeName} is too short to hold the credits screen's slab count"; return false; }
        int slabCount = mainExe[slabCountAt] | (mainExe[slabCountAt + 1] << 8);
        if (slabCount is not (1 or 2)) { reason = $"its slab count reads {slabCount}, not 1 or 2"; return false; }

        var font = GameFont.Load(mainExe.AsSpan(layout.Seg3(GameFont.GlyphBlobSite), GameFont.GlyphBlobLength),
                                  mainExe.AsSpan(layout.Seg3(GameFont.XlatSite), GameFont.XlatLength));

        sources = new SlabSources(archive, entries, mainExe, layout, art, donor, creditsPalette, font);
        reason = null;
        return true;
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

    private static bool FontBlobIsIntact(byte[] mainExe, MainExeLayout layout, byte[] working)
    {
        int blobAt = layout.Seg3(GameFont.GlyphBlobSite);
        if (mainExe.Length < blobAt + GameFont.GlyphBlobLength) return false;
        var (_, chunks) = FrameContainer.Split(mainExe.AsSpan(blobAt, GameFont.GlyphBlobLength));
        return chunks.Count == 1 && FrameCodec.IsIntact(chunks[0], working);
    }

    private static byte[] Concat(List<byte[]> pages)
    {
        var strip = new byte[pages.Count * FrameCodec.FrameBytes];
        for (int i = 0; i < pages.Count; i++) pages[i].CopyTo(strip, i * FrameCodec.FrameBytes);
        return strip;
    }
}
