using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Core.Patching;

/// <summary>What a run would do: the repairs to attempt, the fixes to apply, and the files that has to be backed up first.</summary>
public sealed record PatchPlan(
    IReadOnlyList<RepairFinding> Repairs,
    IReadOnlyList<IFix> Fixes,
    IReadOnlyList<string> FilesToBackup)
{
    /// <summary>Whether this plan would write anything at all.</summary>
    public bool IsEmpty => Repairs.Count == 0 && Fixes.Count == 0;

    /// <summary>Builds the plan the two checkboxes describe over what the scan read.</summary>
    public static PatchPlan From(ScanResult scan, bool applyRepairs, bool applyFixes)
    {
        var repairs = applyRepairs ? scan.Repairs : [];

        var fixes = applyFixes
            ? scan.Fixes.Where(fix => fix.Outcome == FixOutcome.NotApplied).Select(fix => fix.Fix).ToList()
            : [];

        // A run with any fix may go on to compose a slab, which rewrites both files regardless of which
        // fix's own site triggered it, so the backup has to cover them before the plan knows whether it will.
        string[] slabFiles = fixes.Count > 0 ? ["DIGIT0.XRS", "MAIN.EXE"] : [];

        var files = repairs.Select(repair => repair.FileName)
            .Concat(fixes.Select(fix => fix.TargetFile))
            .Concat(slabFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PatchPlan(repairs, fixes, files);
    }
}
