using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.MenuLine;

/// <summary>Rebuilds the copyright strip's archive entry and writes its blit's two immediates into the executable.</summary>
internal static class MenuLineWriter
{
    private const string StripName = "CR.SPF";


    /// <summary>Builds the archive and executable a composed strip implies, touching neither file.</summary>
    public static (byte[] Archive, byte[] MainExe) Build(MenuLineSources sources, byte[] strip, int anchor)
    {
        var entry = sources.Entries.First(candidate => candidate.Name.Equals(StripName, StringComparison.OrdinalIgnoreCase));
        var (header, _) = FrameContainer.Split(sources.Archive.AsSpan(entry.Start, entry.Length));
        var rebuilt = FrameContainer.Build(header, [strip]);
        var archive = XrsWriter.Replace(sources.Archive, sources.Entries, StripName, rebuilt);

        int rows = MenuLineStrip.ReadCell(strip).Height - 2;
        var mainExe = (byte[])sources.MainExe.Clone();
        int siteAt = sources.Layout.Seg3(MenuLineSources.Site);
        mainExe[siteAt + MenuLineSources.BottomRowFromSite] = (byte)rows;
        mainExe[siteAt + MenuLineSources.AnchorFromSite] = (byte)anchor;

        return (archive, mainExe);
    }
}
