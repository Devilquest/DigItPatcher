using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.Patching;

/// <summary>The ways a rescan can contradict what a run or a restore just promised.</summary>
public enum DiscrepancyKind
{
    /// <summary>A fix the run applied does not read back as applied.</summary>
    FixNotReadBack,

    /// <summary>A file the run repaired does not read back as a clean, undamaged copy.</summary>
    RepairNotReadBack,

    /// <summary>A file the run did not write changed on disk anyway.</summary>
    ChangedWithoutWriting,

    /// <summary>A file the backup covers does not match the copy it was just restored from.</summary>
    BackupMismatch,
}

/// <summary>One file whose rescan contradicts what the run or restore just promised.</summary>
public sealed record Discrepancy(string FileName, DiscrepancyKind Kind);

/// <summary>Compares a rescan against what a run or a restore promised, reporting only where they disagree.</summary>
public static class RunVerification
{
    /// <summary>Checks a rescan against a run: files it wrote should read back as written, everything else unchanged.</summary>
    public static IReadOnlyList<Discrepancy> AfterRun(PatchPlan plan, PatchRunResult result, ScanResult before, ScanResult after)
    {
        var discrepancies = new List<Discrepancy>();
        var written = result.Repaired.Concat(result.Written).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A slab or a menu line rewrites both files regardless of which fix's own site triggered it, so
        // whether they belong on the "changed" side of the rule below comes from the result, not from
        // which fix targets them.
        if (result.SlabWritten || result.MenuLineWritten)
        {
            written.Add("DIGIT0.XRS");
            written.Add("MAIN.EXE");
        }

        foreach (var fileName in result.Repaired)
        {
            var read = FindFile(after, fileName);
            if (read is null || !read.IsRecognized || read.Damage is not null)
                discrepancies.Add(new Discrepancy(fileName, DiscrepancyKind.RepairNotReadBack));
        }

        foreach (var fix in plan.Fixes)
        {
            if (!result.Written.Contains(fix.TargetFile, StringComparer.OrdinalIgnoreCase)) continue;

            var read = after.Fixes.FirstOrDefault(scan => scan.Fix.Id == fix.Id);
            if (read is null || read.Outcome != FixOutcome.AlreadyFixed)
                discrepancies.Add(new Discrepancy(fix.TargetFile, DiscrepancyKind.FixNotReadBack));
        }

        foreach (var file in before.Files)
        {
            if (written.Contains(file.Name)) continue;

            var read = FindFile(after, file.Name);
            if (read is null || read.Present != file.Present || read.Sha256 != file.Sha256)
                discrepancies.Add(new Discrepancy(file.Name, DiscrepancyKind.ChangedWithoutWriting));
        }

        return discrepancies;
    }

    /// <summary>Checks a rescan against a restore: every file the backup covers should match its saved copy again.</summary>
    public static IReadOnlyList<Discrepancy> AfterRestore(GameInstall install, ScanResult after)
    {
        var discrepancies = new List<Discrepancy>();

        foreach (var fileName in BackupSet.CoveredFiles(install))
        {
            if (!BackupSet.TryHash(install, fileName, out var backedUp)) continue;

            var current = FindFile(after, fileName)?.Sha256;
            if (current is null && install.TryHash(fileName, out var rehashed)) current = rehashed;

            if (!string.Equals(current, backedUp, StringComparison.Ordinal))
                discrepancies.Add(new Discrepancy(fileName, DiscrepancyKind.BackupMismatch));
        }

        return discrepancies;
    }

    private static FileScan? FindFile(ScanResult scan, string fileName)
        => scan.Files.FirstOrDefault(file => string.Equals(file.Name, fileName, StringComparison.OrdinalIgnoreCase));
}
