using DigItPatcher.Core;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the backup-then-write contract, over installs built from files the test wrote itself.</summary>
public class PatchRunnerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PatchRunner _runner = new();

    public PatchRunnerTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private GameInstall InstallHolding(params (string Name, byte[] Contents)[] files)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [0]);
        foreach (var (name, contents) in files) File.WriteAllBytes(Path.Combine(_tempDir, name), contents);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    [Fact]
    public void AnEmptyPlanWritesAndBacksUpNothing()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));

        var result = _runner.Run(install, new PatchPlan([], [], []), build: null);

        Assert.True(result.Success);
        Assert.Empty(result.Written);
        Assert.False(BackupSet.Exists(install));
    }

    [Fact]
    public void ApplyingAFixBacksUpItsTargetBeforeWritingIt()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = PatchPlan.From(new ScanResult(install.Folder, [], [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null), applyRepairs: false, applyFixes: true);

        var result = _runner.Run(install, plan, build: null);

        Assert.True(result.Success);
        Assert.Equal(["MAIN.EXE"], result.Written);
        Assert.Equal(1, File.ReadAllBytes(install.PathOf("MAIN.EXE")).Single());
        Assert.Equal(0, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), "MAIN.EXE")).Single());
    }

    [Fact]
    public void TheManifestRecordsWhatWasApplied()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = PatchPlan.From(new ScanResult(install.Folder, [], [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null), applyRepairs: false, applyFixes: true);

        _runner.Run(install, plan, new KnownBuild("full", "Full release"));

        var manifest = File.ReadAllText(Path.Combine(BackupSet.FolderPath(install), BackupSet.ManifestFileName));
        Assert.Contains("Full release", manifest);
        Assert.Contains(fix.Id, manifest);
    }

    [Fact]
    public void ASecondRunNeverOverwritesTheFirstRunsBackup()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = new PatchPlan([], [fix], ["MAIN.EXE"]);

        _runner.Run(install, plan, build: null);
        _runner.Run(install, plan, build: null);

        Assert.Equal(0, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), "MAIN.EXE")).Single());
    }

    [Fact]
    public void ARunWithNoIdentityNeverAttemptsASlab()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = PatchPlan.From(new ScanResult(install.Folder, [], [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null), applyRepairs: false, applyFixes: true);

        var result = _runner.Run(install, plan, build: null);

        Assert.False(result.SlabWritten);
        Assert.Null(result.SlabRefusalReason);
    }

    [Fact]
    public void ARunWithAnIdentityRecordsWhyTheGuardRefusedTheSlab()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = PatchPlan.From(new ScanResult(install.Folder, [], [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null), applyRepairs: false, applyFixes: true);

        var result = _runner.Run(install, plan, build: null, new AppIdentity("Dig It! Patcher", "1.0.0-test", "Devilquest"));

        Assert.False(result.SlabWritten);
        Assert.NotNull(result.SlabRefusalReason);

        var manifest = File.ReadAllText(Path.Combine(BackupSet.FolderPath(install), BackupSet.ManifestFileName));
        Assert.DoesNotContain("patch-notes slab", manifest);
    }

    [Fact]
    public void AnUnwritableTargetFailsBeforeAnythingIsBackedUp()
    {
        var install = InstallHolding(("MAIN.EXE", [0]));
        var fix = new WritableFix("MAIN.EXE", original: 0, patched: 1);
        var plan = PatchPlan.From(new ScanResult(install.Folder, [], [], [new FixScan(fix, FixState.Original, FixOutcome.NotApplied)], null), applyRepairs: false, applyFixes: true);

        using (File.Open(install.PathOf("MAIN.EXE"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = _runner.Run(install, plan, build: null);

            Assert.False(result.Success);
            Assert.NotNull(result.FailureReason);
        }

        Assert.False(BackupSet.Exists(install));
    }
}

/// <summary>A single-byte fix that actually writes, unlike ScannerTests' read-only FakeFix.</summary>
internal sealed class WritableFix(string targetFile, byte original, byte patched) : IFix
{
    public string Id => $"Writable:{targetFile}";

    public string TargetFile => targetFile;

    public IReadOnlyList<string> DerivedFor => ["full"];

    public FixOrigin Origin => FixOrigin.File;

    public long Offset => 0;

    public ReadOnlyMemory<byte> OriginalBytes => new[] { original };

    public ReadOnlyMemory<byte> PatchedBytes => new[] { patched };

    public FixState GetState(Stream stream)
    {
        var actual = stream.ReadByte();
        stream.Seek(0, SeekOrigin.Begin);

        if (actual == original) return FixState.Original;
        return actual == patched ? FixState.Patched : FixState.Modified;
    }

    public void Apply(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        stream.WriteByte(patched);
    }

    public void Revert(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        stream.WriteByte(original);
    }
}
