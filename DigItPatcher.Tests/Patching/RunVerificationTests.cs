using System.Security.Cryptography;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the rescan-versus-promise rule: a file the run did not touch must read back unchanged,
/// and a file it did touch must read back as what the run claims it did.</summary>
public class RunVerificationTests : IDisposable
{
    private const string OtherFile = "DIGIT2.XRS";

    private readonly string _tempDir;

    public RunVerificationTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private static string HashOf(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));

    private static FileScan Recognized(string name, byte[] contents, string? damage = null)
        => new(name, Present: true, HashOf(contents), new KnownFileContents(HashOf(contents), ["full"], damage));

    private static ScanResult ScanWith(params FileScan[] files) => new("C:\\Game", [.. files], [], [], null);

    [Fact]
    public void AFixAppliedAndReadBackAsAppliedIsNoDiscrepancy()
    {
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = new PatchPlan([], [fix], ["MAIN.EXE"]);
        var result = new PatchRunResult(true, [], [], ["MAIN.EXE"], null);
        var before = ScanWith(Recognized("MAIN.EXE", [0]));
        var after = new ScanResult("C:\\Game", before.Files, [], [new FixScan(fix, FixState.Patched, FixOutcome.AlreadyFixed)], null);

        var discrepancies = RunVerification.AfterRun(plan, result, before, after);

        Assert.Empty(discrepancies);
    }

    [Fact]
    public void AFixAppliedButStillReadingAsUnappliedIsFixNotReadBack()
    {
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = new PatchPlan([], [fix], ["MAIN.EXE"]);
        var result = new PatchRunResult(true, [], [], ["MAIN.EXE"], null);
        var before = ScanWith(Recognized("MAIN.EXE", [0]));
        var after = new ScanResult("C:\\Game", before.Files, [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null);

        var discrepancies = RunVerification.AfterRun(plan, result, before, after);

        Assert.Equal([new Discrepancy("MAIN.EXE", DiscrepancyKind.FixNotReadBack)], discrepancies);
    }

    [Fact]
    public void ARepairWrittenAndReadBackCleanIsNoDiscrepancy()
    {
        var plan = new PatchPlan([], [], [OtherFile]);
        var result = new PatchRunResult(true, [OtherFile], [], [], null);
        var before = ScanWith(Recognized(OtherFile, [9], damage: "displaced-span"));
        var after = ScanWith(Recognized(OtherFile, [1, 2, 3, 4]));

        var discrepancies = RunVerification.AfterRun(plan, result, before, after);

        Assert.Empty(discrepancies);
    }

    [Fact]
    public void ARepairWrittenButStillReadingAsDamagedIsRepairNotReadBack()
    {
        var plan = new PatchPlan([], [], [OtherFile]);
        var result = new PatchRunResult(true, [OtherFile], [], [], null);
        var before = ScanWith(Recognized(OtherFile, [9], damage: "displaced-span"));
        var after = ScanWith(Recognized(OtherFile, [9], damage: "displaced-span"));

        var discrepancies = RunVerification.AfterRun(plan, result, before, after);

        Assert.Equal([new Discrepancy(OtherFile, DiscrepancyKind.RepairNotReadBack)], discrepancies);
    }

    [Fact]
    public void ACanceledRunWithNothingChangedIsNoDiscrepancy()
    {
        var plan = new PatchPlan([], [], []);
        var nothingWritten = new PatchRunResult(true, [], [], [], null);
        var before = ScanWith(Recognized("MAIN.EXE", [0]), Recognized(OtherFile, [1, 2, 3, 4]));
        var after = ScanWith(Recognized("MAIN.EXE", [0]), Recognized(OtherFile, [1, 2, 3, 4]));

        var discrepancies = RunVerification.AfterRun(plan, nothingWritten, before, after);

        Assert.Empty(discrepancies);
    }

    [Fact]
    public void ACanceledRunWithAFileChangedBehindItIsChangedWithoutWriting()
    {
        var plan = new PatchPlan([], [], []);
        var nothingWritten = new PatchRunResult(true, [], [], [], null);
        var before = ScanWith(Recognized("MAIN.EXE", [0]));
        var after = ScanWith(Recognized("MAIN.EXE", [1]));

        var discrepancies = RunVerification.AfterRun(plan, nothingWritten, before, after);

        Assert.Equal([new Discrepancy("MAIN.EXE", DiscrepancyKind.ChangedWithoutWriting)], discrepancies);
    }

    [Fact]
    public void AMixedRunOnlyFlagsTheFileThatChangedOnItsOwn()
    {
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = new PatchPlan([], [fix], ["MAIN.EXE"]);
        var result = new PatchRunResult(true, [], [], ["MAIN.EXE"], null);
        var before = ScanWith(Recognized("MAIN.EXE", [0]), Recognized(OtherFile, [1, 2, 3, 4]));
        var after = new ScanResult("C:\\Game",
            [Recognized("MAIN.EXE", [1]), Recognized(OtherFile, [9, 9, 9, 9])],
            [], [new FixScan(fix, FixState.Patched, FixOutcome.AlreadyFixed)], null);

        var discrepancies = RunVerification.AfterRun(plan, result, before, after);

        Assert.Equal([new Discrepancy(OtherFile, DiscrepancyKind.ChangedWithoutWriting)], discrepancies);
    }

    private GameInstall InstallHolding(params (string Name, byte[] Contents)[] files)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [0]);
        foreach (var (name, contents) in files) File.WriteAllBytes(Path.Combine(_tempDir, name), contents);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    [Fact]
    public void ARestoredFileMatchingItsBackupIsNoDiscrepancy()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        var after = ScanWith(Recognized("MAIN.EXE", [1]));

        var discrepancies = RunVerification.AfterRestore(install, after);

        Assert.Empty(discrepancies);
    }

    [Fact]
    public void ARestoredFileNotMatchingItsBackupIsBackupMismatch()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        var after = ScanWith(Recognized("MAIN.EXE", [9]));

        var discrepancies = RunVerification.AfterRestore(install, after);

        Assert.Equal([new Discrepancy("MAIN.EXE", DiscrepancyKind.BackupMismatch)], discrepancies);
    }
}
