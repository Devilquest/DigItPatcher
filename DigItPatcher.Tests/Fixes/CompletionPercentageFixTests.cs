using System.Security.Cryptography;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the completion-percentage fix's own bytes and the states a stream reads back as.</summary>
public class CompletionPercentageFixTests
{
    private static readonly CompletionPercentageFix Patch = new();

    private static readonly byte[] ExpectedOriginalBytes =
    {
        0x8B, 0x46, 0xF8, 0x99, 0xB9, 0x05, 0x00, 0xF7, 0xF9, 0x92, 0x8B, 0xD0, 0xB8, 0x01, 0x00,
        0x8B, 0xCA, 0xD3, 0xE0, 0x8B, 0x3E, 0xA2, 0x12, 0xD1, 0xE7, 0x0B, 0x85, 0xA0, 0x13, 0x8B,
        0x3E, 0xA2, 0x12, 0xD1, 0xE7, 0x89, 0x85, 0xA0, 0x13
    };

    private static readonly byte[] ExpectedPatchedBytes =
    {
        0xA1, 0x9E, 0x12, 0xC1, 0xE0, 0x04, 0x03, 0x06, 0xA2, 0x12, 0x89, 0xC7, 0xD1, 0xE7, 0x8B,
        0x46, 0xF8, 0x99, 0xB9, 0x0A, 0x00, 0xF7, 0xF9, 0x89, 0xD1, 0xB8, 0x01, 0x00, 0xD3, 0xE0,
        0x0B, 0x85, 0xA0, 0x13, 0x89, 0x85, 0xA0, 0x13, 0x90
    };

    [Fact]
    public void Offset_IsTheSiteWithinSeg3()
    {
        Assert.Equal(FixOrigin.Seg3, Patch.Origin);
        Assert.Equal(0xDD3B, Patch.Offset);
    }

    /// <summary>The same seg3 address is a different file offset in each build, and both are the documented one.</summary>
    [Theory]
    [InlineData(FakeMainExe.FullSeg3, 0x1893B)]
    [InlineData(FakeMainExe.ManaccomSeg3, 0x18B3B)]
    public void TheSiteResolvesToEachBuildsOwnFileOffset(int seg3, long expected)
    {
        using var stream = new MemoryStream(FakeMainExe.WithSegments(seg3, seg3 + 0x15500, seg3 + 0x20000));

        Assert.True(Patch.TryLocate(stream, out long site));
        Assert.Equal(expected, site);
    }

    [Fact]
    public void BothFullReleaseEditionsAreDeclared()
    {
        Assert.Equal(["full", "manaccom"], Patch.DerivedFor);
    }

    [Fact]
    public void OriginalBytes_MatchGroundTruth()
    {
        Assert.Equal(ExpectedOriginalBytes, Patch.OriginalBytes.ToArray());
    }

    [Fact]
    public void PatchedBytes_MatchGroundTruth()
    {
        Assert.Equal(ExpectedPatchedBytes, Patch.PatchedBytes.ToArray());
    }

    [Fact]
    public void OriginalAndPatchedBytes_AreSameLength()
    {
        Assert.Equal(Patch.OriginalBytes.Length, Patch.PatchedBytes.Length);
    }

    [Fact]
    public void StateRoundTrip_OriginalApplyPatchedRevertOriginal()
    {
        using var stream = BuildStreamWithBytesAt(Patch.Offset, Patch.OriginalBytes.Span);

        Assert.Equal(FixState.Original, Patch.GetState(stream));

        Patch.Apply(stream);
        Assert.Equal(FixState.Patched, Patch.GetState(stream));

        Patch.Revert(stream);
        Assert.Equal(FixState.Original, Patch.GetState(stream));
    }

    [Fact]
    public void GetState_UnknownBytes_ReturnsModified()
    {
        byte[] garbage = new byte[Patch.OriginalBytes.Length];
        Array.Fill(garbage, (byte)0xCC);
        using var stream = BuildStreamWithBytesAt(Patch.Offset, garbage);

        Assert.Equal(FixState.Modified, Patch.GetState(stream));
    }

    [Fact]
    public void GetState_StreamTooShort_ReturnsFileTooSmall()
    {
        using var stream = new MemoryStream(FakeMainExe.FullRelease(0x1000));

        Assert.Equal(FixState.FileTooSmall, Patch.GetState(stream));
    }

    [Fact]
    public void GetState_NoSegmentTable_ReturnsSiteNotFound()
    {
        using var stream = new MemoryStream(new byte[0x20000]);

        Assert.Equal(FixState.SiteNotFound, Patch.GetState(stream));
    }

    [Fact]
    public void Apply_WhenNotOriginal_Throws()
    {
        using var stream = BuildStreamWithBytesAt(Patch.Offset, Patch.PatchedBytes.Span);

        Assert.Throws<InvalidOperationException>(() => Patch.Apply(stream));
    }

    [Fact]
    public void Revert_WhenNotPatched_Throws()
    {
        using var stream = BuildStreamWithBytesAt(Patch.Offset, Patch.OriginalBytes.Span);

        Assert.Throws<InvalidOperationException>(() => Patch.Revert(stream));
    }

    /// <summary>The site is found in the copy in hand, and applying then reverting leaves the file as it was.</summary>
    [Trait("Category", "RequiresGame")]
    [RequiresGameFact(GameNeeds.SupportedBuild)]
    public void TheSiteIsFoundInTheInstalledCopyAndTheRoundTripLeavesItUnchanged()
    {
        Assert.True(TestPaths.TryGetGameDir(out var gameDir));
        Assert.True(GameInstall.TryOpen(gameDir, out var install));

        // Written to a copy, never to the folder the tests were pointed at.
        var scratch = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        File.WriteAllBytes(scratch, install.ReadAll("MAIN.EXE"));

        try
        {
            var before = Sha256Of(scratch);
            using (var stream = File.Open(scratch, FileMode.Open, FileAccess.ReadWrite))
            {
                Assert.Equal(FixState.Original, Patch.GetState(stream));
                Patch.Apply(stream);
                Assert.Equal(FixState.Patched, Patch.GetState(stream));
            }

            Assert.NotEqual(before, Sha256Of(scratch));

            using (var stream = File.Open(scratch, FileMode.Open, FileAccess.ReadWrite))
            {
                Patch.Revert(stream);
                Assert.Equal(FixState.Original, Patch.GetState(stream));
            }

            Assert.Equal(before, Sha256Of(scratch));
        }
        finally
        {
            File.Delete(scratch);
        }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    // A full-release-shaped executable holding nothing but a segment table and these bytes at the fix's site.
    private static MemoryStream BuildStreamWithBytesAt(long offset, ReadOnlySpan<byte> bytes)
    {
        int site = FakeMainExe.FullSeg3 + (int)offset;
        var buffer = FakeMainExe.FullRelease(site + bytes.Length);
        bytes.CopyTo(buffer.AsSpan(site));
        return new MemoryStream(buffer) { Position = 0 };
    }
}
