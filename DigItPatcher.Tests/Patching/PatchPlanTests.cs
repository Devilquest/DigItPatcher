using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Patching;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Tests;

/// <summary>Tests covering how a scan and the fixes checkbox become the operations and files a run needs.</summary>
public class PatchPlanTests
{
    private static FixScan Scan(IFix fix, FixOutcome outcome) => new(fix, FixState.Original, outcome);

    [Fact]
    public void NotCheckingFixesProducesAnEmptyPlan()
    {
        var scan = new ScanResult("C:\\Game", [], [], [Scan(new NamedTargetFix("MAIN.EXE"), FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: false, applyFixes: false);

        Assert.True(plan.IsEmpty);
        Assert.Empty(plan.FilesToBackup);
    }

    [Fact]
    public void CheckingFixesIncludesOnlyTheSitesNotYetApplied()
    {
        var pending = new NamedTargetFix("MAIN.EXE");
        var already = new NamedTargetFix("DIGIT.EXE");
        var scan = new ScanResult("C:\\Game", [], [], [Scan(pending, FixOutcome.NotApplied), Scan(already, FixOutcome.AlreadyFixed)], null);

        var plan = PatchPlan.From(scan, applyRepairs: false, applyFixes: true);

        Assert.Equal([pending], plan.Fixes);
    }

    [Fact]
    public void TheBackupSetCoversEveryFileThePlanWritesPlusWhatASlabMightRewrite()
    {
        var first = new NamedTargetFix("MAIN.EXE");
        var second = new NamedTargetFix("DIGIT.EXE");
        var scan = new ScanResult("C:\\Game", [], [], [Scan(first, FixOutcome.NotApplied), Scan(second, FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: false, applyFixes: true);

        Assert.Equal(["MAIN.EXE", "DIGIT.EXE", "DIGIT0.XRS"], plan.FilesToBackup);
    }

    [Fact]
    public void TwoFixesOverTheSameFileBackUpThatFileOnce()
    {
        var first = new NamedTargetFix("MAIN.EXE");
        var second = new NamedTargetFix("MAIN.EXE");
        var scan = new ScanResult("C:\\Game", [], [], [Scan(first, FixOutcome.NotApplied), Scan(second, FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: false, applyFixes: true);

        Assert.Equal(["MAIN.EXE", "DIGIT0.XRS"], plan.FilesToBackup);
    }

    [Fact]
    public void AnyFixAddsBothSlabFilesToTheBackupSet()
    {
        var scan = new ScanResult("C:\\Game", [], [], [Scan(new NamedTargetFix("DIGIT.EXE"), FixOutcome.NotApplied)], null);

        var plan = PatchPlan.From(scan, applyRepairs: false, applyFixes: true);

        Assert.Contains("DIGIT0.XRS", plan.FilesToBackup);
        Assert.Contains("MAIN.EXE", plan.FilesToBackup);
    }
}

/// <summary>A fix whose only load-bearing property is which file it targets.</summary>
internal sealed class NamedTargetFix(string targetFile) : IFix
{
    public string Id => $"Fake:{targetFile}";

    public string TargetFile => targetFile;

    public IReadOnlyList<string> DerivedFor => ["full"];

    public FixOrigin Origin => FixOrigin.File;

    public long Offset => 0;

    public ReadOnlyMemory<byte> OriginalBytes => new byte[] { 0 };

    public ReadOnlyMemory<byte> PatchedBytes => new byte[] { 1 };

    public FixState GetState(Stream stream) => FixState.Original;

    public void Apply(Stream stream) => stream.WriteByte(1);

    public void Revert(Stream stream) => stream.WriteByte(0);
}
