using System.Security.Cryptography;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the blank-slab derivation against the user's own copy of the game.</summary>
public class SlabBlankTests
{
    // The prototype's own derivation over a verified DIGIT0.XRS hashes to this value; matching it is
    // what validates the port.
    private const string ExpectedStripSha256 = "c96054bdf346e13d3e77bd438e2c5a7f7621d036e0371838ee3e649b7a978ae0";

    [RequiresGameFact]
    public void TheDerivedStripMatchesThePrototypesOutputExactly()
    {
        var strip = Derive();
        Assert.Equal(ExpectedStripSha256, Sha256Of(strip));
    }

    [RequiresGameFact]
    public void DerivingFromAnAlreadyPatchedCopyIsByteForByteIdenticalToDerivingFromAClean()
    {
        var clean = Derive();
        var patchedArt = (byte[])clean.Clone(); // stands in for a copy this tool already wrote a slab into

        var donor = LoadStrip("SLB00F.MPF");
        var palette = LoadPalette("SLB01.PAL");
        var second = SlabBlank.Derive(patchedArt, donor, palette);

        Assert.Equal(clean, second);
    }

    private static byte[] Derive()
    {
        var art = LoadStrip("SLB01F.MPF");
        var donor = LoadStrip("SLB00F.MPF");
        var palette = LoadPalette("SLB01.PAL");
        return SlabBlank.Derive(art, donor, palette);
    }

    private static byte[] LoadStrip(string entryName)
    {
        var entry = FindEntry(entryName);
        var (_, chunks) = FrameContainer.Split(Archive.AsSpan(entry.Start, entry.Length));
        var pages = FrameContainer.DecodePages(chunks);

        var strip = new byte[pages.Count * FrameCodec.FrameBytes];
        for (int i = 0; i < pages.Count; i++)
            pages[i].CopyTo(strip, i * FrameCodec.FrameBytes);
        return strip;
    }

    private static (byte R, byte G, byte B)[] LoadPalette(string entryName)
    {
        var entry = FindEntry(entryName);
        return GamePalette.Read(Archive.AsSpan(entry.Start, entry.Length));
    }

    private static XrsEntry FindEntry(string name)
    {
        Assert.True(XrsDirectory.TryRead(Archive, out var entries));
        return entries.First(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] Archive
    {
        get
        {
            Assert.True(TestPaths.TryGetGameDir(out var gameDir));
            Assert.True(GameInstall.TryOpen(gameDir, out var install));
            return install.ReadAll("DIGIT0.XRS");
        }
    }

    private static string Sha256Of(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
