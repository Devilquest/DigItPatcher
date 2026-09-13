using System.Security.Cryptography;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the repair that undoes a byte inserted into an archive and a byte lost further on.</summary>
public class DisplacedSpanRepairTests : IDisposable
{
    private const string ArchiveName = "DIGIT2.XRS";

    private readonly string _tempDir;
    private readonly DisplacedSpanRepair _repair = new();

    public DisplacedSpanRepairTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static byte[] CleanArchive() => SheetArchiveBuilder.Archive(
        ("LVL001F.MPF", SheetArchiveBuilder.Sheet(2)),
        ("LVL002F.MPF", SheetArchiveBuilder.Sheet(2, fill: 30)));

    // The damage: one byte more inside the first entry's frames, one byte fewer inside the second entry's.
    private static byte[] Displace(byte[] clean, out int inserted, out int lost)
    {
        XrsDirectory.TryRead(clean, out var entries);
        inserted = entries[0].Start + 772 + 3;
        lost = entries[1].Start + 772 + 3;

        var damaged = SheetArchiveBuilder.WithByteInserted(clean, inserted, 0xF4);
        return SheetArchiveBuilder.WithByteRemoved(damaged, lost + 1);
    }

    private GameInstall InstallHolding(byte[] archive)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [0]);
        File.WriteAllBytes(Path.Combine(_tempDir, ArchiveName), archive);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    [Fact]
    public void AnArchiveThatAgreesWithItselfReportsNothing()
        => Assert.Empty(_repair.Diagnose(InstallHolding(CleanArchive())));

    [Fact]
    public void ADisplacedSpanIsReportedAgainstTheArchiveItIsIn()
    {
        var findings = _repair.Diagnose(InstallHolding(Displace(CleanArchive(), out _, out _)));

        Assert.Equal(ArchiveName, Assert.Single(findings).FileName);
        Assert.Equal(DisplacedSpanRepair.Kind, findings[0].RepairId);
    }

    [Fact]
    public void OneOfTheCandidatesItBuildsIsTheArchiveTheDamageStartedFrom()
    {
        var clean = CleanArchive();
        var install = InstallHolding(Displace(clean, out _, out _));
        var finding = _repair.Diagnose(install).Single();

        var restored = _repair.Produce(install, finding, CancellationToken.None)
            .FirstOrDefault(candidate => Fingerprint(candidate) == Fingerprint(clean));

        Assert.NotNull(restored);
    }

    [Fact]
    public void EveryCandidateIsTheSameSizeAsTheArchiveItCameFrom()
    {
        var install = InstallHolding(Displace(CleanArchive(), out _, out _));
        var finding = _repair.Diagnose(install).Single();
        int size = install.ReadAll(ArchiveName).Length;

        var candidates = _repair.Produce(install, finding, CancellationToken.None).Take(20).ToList();

        Assert.NotEmpty(candidates);
        Assert.All(candidates, candidate => Assert.Equal(size, candidate.Length));
    }

    [Fact]
    public void ProducingNothingIsTheAnswerForAnArchiveWithNothingWrongWithIt()
    {
        var install = InstallHolding(CleanArchive());

        Assert.Empty(_repair.Produce(install, new RepairFinding(DisplacedSpanRepair.Kind, ArchiveName), CancellationToken.None));
    }

    [Fact]
    public void CancelingTheSearchStopsIt()
    {
        var install = InstallHolding(Displace(CleanArchive(), out _, out _));
        var finding = _repair.Diagnose(install).Single();

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => _repair.Produce(install, finding, canceled.Token).ToList());
    }

    private static string Fingerprint(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));
}
