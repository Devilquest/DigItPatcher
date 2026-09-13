using System.Security.Cryptography;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.Tests;

/// <summary>Tests covering what one pass over an install reports, built from files the test wrote itself.</summary>
public class ScannerTests : IDisposable
{
    private const string Marker = "DIGIT0.XRS";
    private const string Executable = "MAIN.EXE";
    private const string OtherArchive = "DIGIT2.XRS";

    private static readonly byte[] FullContents = [1, 2, 3, 4];
    private static readonly byte[] SharewareContents = [9, 9, 9, 9];
    private static readonly byte[] DamagedContents = [1, 2, 3, 5];
    private static readonly byte[] SharedContents = [7, 7];

    private readonly string _tempDir;

    public ScannerTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static string HashOf(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));

    private static ReleaseCatalog Catalog { get; } = ReleaseCatalog.From(
        [new KnownBuild("full", "Full release"), new KnownBuild("shareware", "Shareware release")],
        [
            new KnownFile(Marker, [new KnownFileContents(HashOf(SharedContents), ["full", "shareware"], null)], []),
            new KnownFile(Executable,
            [
                new KnownFileContents(HashOf(FullContents), ["full"], null),
                new KnownFileContents(HashOf(SharewareContents), ["shareware"], null),
            ], []),
            new KnownFile(OtherArchive,
            [
                new KnownFileContents(HashOf(FullContents), ["full"], null),
                new KnownFileContents(HashOf(DamagedContents), ["full"], "displaced-span"),
            ], ["shareware"]),
        ]);

    private GameInstall InstallHolding(params (string Name, byte[] Contents)[] files)
    {
        foreach (var (name, contents) in files) File.WriteAllBytes(Path.Combine(_tempDir, name), contents);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    private static ScanResult ScanOf(GameInstall install, params IFix[] fixes)
        => new Scanner(Catalog, fixes, []).Scan(install);

    [Fact]
    public void AFullyCleanCopyIsNamedAndOffersItsFixes()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, FullContents), (OtherArchive, FullContents));

        var scan = ScanOf(install, new FakeFix(Executable, FullContents[..2], [8, 8]));

        Assert.Equal("full", scan.Build?.Id);
        Assert.Empty(scan.Unrecognized);
        Assert.Empty(scan.Damaged);
        Assert.True(scan.FixesOffered);
        Assert.False(scan.AlreadyFixed);
    }

    [Fact]
    public void AFileThisToolWroteLeavesTheBuildNamedByTheFilesBesideIt()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, [4, 3, 2, 1]), (OtherArchive, FullContents));

        var scan = ScanOf(install);

        Assert.Equal("full", scan.Build?.Id);
        Assert.Equal([Executable], scan.Unrecognized.Select(file => file.Name));
    }

    [Fact]
    public void DamageIsReportedAgainstTheFileThatCarriesIt()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, FullContents), (OtherArchive, DamagedContents));

        var scan = ScanOf(install);

        Assert.Equal("full", scan.Build?.Id);
        Assert.Equal([OtherArchive], scan.Damaged.Select(file => file.Name));
        Assert.Equal("displaced-span", scan.Damaged.Single().Damage);
    }

    [Fact]
    public void ACopyHoldingOnlyFilesSharedByEveryBuildIsNotNamed()
    {
        var install = InstallHolding((Marker, SharedContents));

        Assert.Null(ScanOf(install).Build);
    }

    [Fact]
    public void TheSharewareIsNamedByItsOwnExecutable()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, SharewareContents));

        Assert.Equal("shareware", ScanOf(install).Build?.Id);
    }

    [Fact]
    public void ABuildWeKnowAndHaveNotSolvedPutsTheLimitationOnUs()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, SharewareContents));

        var scan = ScanOf(install, new FakeFix(Executable, FullContents[..2], [8, 8], "full"));

        Assert.Equal("shareware", scan.Build?.Id);
        Assert.Equal(FixOutcome.NotAvailable, scan.Fixes.Single().Outcome);
        Assert.False(scan.FixesOffered);
    }

    [Fact]
    public void ABuildWeDidSolveReadingNeitherTableIsUnexpectedBytes()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, FullContents), (OtherArchive, FullContents));

        var scan = ScanOf(install, new FakeFix(Executable, [200, 201], [8, 8], "full"));

        Assert.Equal("full", scan.Build?.Id);
        Assert.Equal(FixOutcome.UnexpectedBytes, scan.Fixes.Single().Outcome);
    }

    [Fact]
    public void OneSiteReadingAnUnknownValueWithdrawsTheWholeBlock()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, FullContents));

        var scan = ScanOf(install,
            new FakeFix(Executable, FullContents[..2], [8, 8]),
            new FakeFix(Executable, [200, 201], [8, 8]));

        Assert.False(scan.FixesOffered);
        Assert.Contains(scan.Fixes, fix => fix.State == FixState.Modified);
    }

    [Fact]
    public void ASiteWhoseFileIsNotThereIsReportedRatherThanRead()
    {
        var install = InstallHolding((Marker, SharedContents));

        var scan = ScanOf(install, new FakeFix(Executable, FullContents[..2], [8, 8]));

        Assert.Equal(FixState.FileMissing, scan.Fixes.Single().State);
        Assert.False(scan.FixesOffered);
    }

    [Fact]
    public void AnInstallAlreadyHoldingTheFixedBytesSaysSo()
    {
        var install = InstallHolding((Marker, SharedContents), (Executable, FullContents));

        var scan = ScanOf(install, new FakeFix(Executable, [9, 9], FullContents[..2]));

        Assert.True(scan.AlreadyFixed);
        Assert.True(scan.FixesOffered);
    }

    [Fact]
    public void AMissingFileIsReportedAsAbsentRatherThanUnrecognized()
    {
        var install = InstallHolding((Marker, SharedContents));

        var scan = ScanOf(install);

        Assert.False(scan.Files.Single(file => file.Name == Executable).Present);
        Assert.Empty(scan.Unrecognized);
    }
}

/// <summary>A fix over bytes the test chose, so no test needs a real game file.</summary>
internal sealed class FakeFix(string targetFile, byte[] original, byte[] patched, params string[] derivedFor) : IFix
{
    public string Id => $"Fake:{targetFile}:{Convert.ToHexStringLower(original)}";

    public string TargetFile => targetFile;

    public IReadOnlyList<string> DerivedFor => derivedFor.Length > 0 ? derivedFor : ["full"];

    public FixOrigin Origin => FixOrigin.File;

    public long Offset => 0;

    public ReadOnlyMemory<byte> OriginalBytes => original;

    public ReadOnlyMemory<byte> PatchedBytes => patched;

    public FixState GetState(Stream stream)
    {
        if (stream.Length < original.Length) return FixState.FileTooSmall;

        var actual = new byte[original.Length];
        stream.ReadExactly(actual);

        if (actual.AsSpan().SequenceEqual(original)) return FixState.Original;
        return actual.AsSpan().SequenceEqual(patched) ? FixState.Patched : FixState.Modified;
    }

    public void Apply(Stream stream) => throw new NotSupportedException();

    public void Revert(Stream stream) => throw new NotSupportedException();
}
