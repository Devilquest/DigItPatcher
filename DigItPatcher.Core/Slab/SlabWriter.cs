using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.Slab;

/// <summary>Writes a derived strip into the archive and marks the credits screen as carrying three slabs.</summary>
internal static class SlabWriter
{
    private const string CreditsForeground = "SLB01F.MPF";
    private const string CreditsMask = "SLB01M.MPF";
    private const int Pages = 8;

    // DS:0x10, one word per screen: instructions, then the credits screen this writes.
    private const int SlabCountTable = 0x10;
    private const int CreditsScreenIndex = 1;

    // 3 slabs, stored as slabs - 1.
    private const int ThreeSlabs = 2;

    /// <summary>Rebuilds the foreground, rebuilds the mask for every page the foreground changed, and sets the
    /// slab count, returning the archive and <c>MAIN.EXE</c> this produces without touching either file.</summary>
    public static (byte[] Archive, byte[] MainExe) Build(SlabSources sources, byte[] strip)
    {
        var fgEntry = Entry(sources.Entries, CreditsForeground);
        var maskEntry = Entry(sources.Entries, CreditsMask);

        var (fgHeader, fgChunks) = FrameContainer.Split(sources.Archive.AsSpan(fgEntry.Start, fgEntry.Length));
        var fgPages = FrameContainer.DecodePages(fgChunks);

        int first = Pages;
        for (int p = 0; p < Pages; p++)
        {
            if (!PageMatches(sources.Palette, fgPages[p], strip, p)) { first = p; break; }
        }

        var keptFg = new Dictionary<int, byte[]>();
        for (int p = 0; p < first; p++) keptFg[p] = fgChunks[p];

        var newFgPages = new List<byte[]>(fgPages);
        for (int p = first; p < Pages; p++)
        {
            var page = new byte[FrameCodec.FrameBytes];
            Array.Copy(strip, p * FrameCodec.FrameBytes, page, 0, FrameCodec.FrameBytes);
            newFgPages[p] = page;
        }

        var (maskHeader, maskChunks) = FrameContainer.Split(sources.Archive.AsSpan(maskEntry.Start, maskEntry.Length));
        var maskPages = FrameContainer.DecodePages(maskChunks);
        var keptMask = new Dictionary<int, byte[]>();
        for (int p = 0; p < first; p++) keptMask[p] = maskChunks[p];

        for (int p = first; p < Pages; p++)
        {
            var newFg = newFgPages[p];
            var oldMask = maskPages[p];
            var derived = new byte[FrameCodec.FrameBytes];
            for (int i = 0; i < FrameCodec.FrameBytes; i++)
                derived[i] = newFg[i] != 0 || oldMask[i] == 0 ? (byte)0 : (byte)255;
            maskPages[p] = derived;
        }

        var newFgBytes = FrameContainer.Build(fgHeader, newFgPages, keptFg);
        VerifyWrittenBack(newFgBytes, strip);

        var newMaskBytes = FrameContainer.Build(maskHeader, maskPages, keptMask);

        var archive = XrsWriter.Replace(sources.Archive, sources.Entries, CreditsForeground, newFgBytes);
        XrsDirectory.TryRead(archive, out var afterFirstReplace);
        archive = XrsWriter.Replace(archive, afterFirstReplace, CreditsMask, newMaskBytes);

        var mainExe = (byte[])sources.MainExe.Clone();
        int slabCountAt = sources.Layout.DGroup(SlabCountTable + (CreditsScreenIndex * 2));
        mainExe[slabCountAt] = ThreeSlabs;
        mainExe[slabCountAt + 1] = 0;

        return (archive, mainExe);
    }

    // Whether a page changed is decided on the colors it resolves to, not its indices: the palette holds
    // nine colors at more than one index, and a page can be re-numbered without its picture changing.
    private static bool PageMatches((byte R, byte G, byte B)[] palette, byte[] oldPage, byte[] strip, int page)
    {
        int offset = page * FrameCodec.FrameBytes;
        for (int i = 0; i < FrameCodec.FrameBytes; i++)
            if (!palette[oldPage[i]].Equals(palette[strip[offset + i]])) return false;
        return true;
    }

    private static void VerifyWrittenBack(byte[] rebuiltForeground, byte[] strip)
    {
        var (_, chunks) = FrameContainer.Split(rebuiltForeground);
        var pages = FrameContainer.DecodePages(chunks);
        for (int p = 0; p < pages.Count; p++)
        {
            if (!pages[p].AsSpan().SequenceEqual(strip.AsSpan(p * FrameCodec.FrameBytes, FrameCodec.FrameBytes)))
                throw new InvalidOperationException($"the rebuilt foreground's page {p} does not decode back to the strip it was built from");
        }
    }

    private static XrsEntry Entry(IReadOnlyList<XrsEntry> entries, string name)
        => entries.First(candidate => candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
