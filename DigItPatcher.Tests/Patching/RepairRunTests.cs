using System.Security.Cryptography;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the rule that a repair is licensed by its result and never by the diagnosis behind it.</summary>
public class RepairRunTests : IDisposable
{
    private const string ArchiveName = "DIGIT2.XRS";

    private static readonly byte[] Clean = [1, 2, 3, 4];
    private static readonly byte[] Damaged = [1, 2, 3, 5];
    private static readonly byte[] Wrong = [9, 9, 9, 9];

    private readonly string _tempDir;

    public RepairRunTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static string HashOf(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));

    private static ReleaseCatalog Catalog { get; } = ReleaseCatalog.From(
        [new KnownBuild("full", "Full release")],
        [new KnownFile(ArchiveName, [new KnownFileContents(HashOf(Clean), ["full"], null)], [])]);

    private GameInstall InstallHolding(byte[] archive)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [0]);
        File.WriteAllBytes(Path.Combine(_tempDir, ArchiveName), archive);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    private static PatchPlan RepairOnlyPlan()
        => new([new RepairFinding("fake", ArchiveName)], [], [ArchiveName]);

    private static PatchRunner RunnerProposing(params byte[][] candidates)
        => new(Catalog, [new FakeRepair(candidates)]);

    [Fact]
    public void ACandidateThatMatchesTheVerifiedVersionIsWritten()
    {
        var install = InstallHolding(Damaged);

        var result = RunnerProposing(Clean).Run(install, RepairOnlyPlan(), build: null);

        Assert.True(result.Success);
        Assert.Equal([ArchiveName], result.Repaired);
        Assert.Equal(Clean, install.ReadAll(ArchiveName));
    }

    [Fact]
    public void ACandidateThatDoesNotMatchIsRefusedAndTheFileIsLeftAlone()
    {
        var install = InstallHolding(Damaged);

        var result = RunnerProposing(Wrong).Run(install, RepairOnlyPlan(), build: null);

        Assert.True(result.Success);
        Assert.Empty(result.Repaired);
        Assert.Equal([ArchiveName], result.Unrepaired);
        Assert.Equal(Damaged, install.ReadAll(ArchiveName));
    }

    [Fact]
    public void ARefusedRepairLeavesNoBackupBehind()
    {
        var install = InstallHolding(Damaged);

        RunnerProposing(Wrong).Run(install, RepairOnlyPlan(), build: null);

        Assert.False(BackupSet.Exists(install));
    }

    [Fact]
    public void TheBackupHoldsTheDamagedArchiveTheRepairReplaced()
    {
        var install = InstallHolding(Damaged);

        RunnerProposing(Clean).Run(install, RepairOnlyPlan(), build: null);

        Assert.Equal(Damaged, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), ArchiveName)));
    }

    [Fact]
    public void TheSearchStopsAtTheFirstCandidateThatMatches()
    {
        var install = InstallHolding(Damaged);
        var repair = new FakeRepair([Wrong, Clean, Wrong]);

        new PatchRunner(Catalog, [repair]).Run(install, RepairOnlyPlan(), build: null);

        Assert.Equal(2, repair.Built);
    }

    [Fact]
    public void ARepairOnlyPlanBacksUpTheArchiveItTargetsAndNothingElse()
    {
        var scan = new ScanResult("C:\\Game", [], [new RepairFinding("fake", ArchiveName)],
            [new FixScan(new NamedTargetFix("MAIN.EXE"), FixState.Original, FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: true, applyFixes: false);

        Assert.Equal([ArchiveName], plan.FilesToBackup);
    }

    [Fact]
    public void APlanDoingBothBacksUpEveryFileEitherHalfWritesPlusWhatASlabMightRewrite()
    {
        var scan = new ScanResult("C:\\Game", [], [new RepairFinding("fake", ArchiveName)],
            [new FixScan(new NamedTargetFix("MAIN.EXE"), FixState.Original, FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: true, applyFixes: true);

        Assert.Equal([ArchiveName, "MAIN.EXE", "DIGIT0.XRS"], plan.FilesToBackup);
    }
}

/// <summary>A repair that proposes whatever the test handed it, and counts how many of those were asked for.</summary>
internal sealed class FakeRepair(IReadOnlyList<byte[]> candidates) : IRepair
{
    /// <summary>How many candidates the caller pulled before it stopped.</summary>
    public int Built { get; private set; }

    public string Id => "fake";

    public IReadOnlyList<RepairFinding> Diagnose(GameInstall install) => [new RepairFinding(Id, "DIGIT2.XRS")];

    public IEnumerable<byte[]> Produce(GameInstall install, RepairFinding finding, CancellationToken token)
    {
        foreach (var candidate in candidates)
        {
            Built++;
            yield return candidate;
        }
    }
}
