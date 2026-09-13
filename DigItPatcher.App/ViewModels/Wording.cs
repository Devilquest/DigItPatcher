using DigItPatcher.Core.Patching;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.App.ViewModels;

/// <summary>The sentences the window says about a scan.</summary>
internal static class Wording
{
    /// <summary>What the scan concluded about the copy, reporting our list rather than judging the user's files.</summary>
    internal static string IdentificationLine(ScanResult scan)
    {
        var release = scan.Build is null ? "Unknown release" : scan.Build.Name;

        if (scan.Damaged.Count > 0) return $"{release}. {Names(scan.Damaged.Select(file => file.Name))} {(scan.Damaged.Count == 1 ? "is" : "are")} damaged.";
        if (scan.AlreadyFixed) return $"{release}. The files match the verified version and all patches are applied.";
        if (scan.Unrecognized.Count > 0) return $"{release}. {Count(scan.Unrecognized.Count, "file")} could not be identified.";

        // Every file identified and no single release covering them all: the copy is a mixture, and saying
        // it matches a verified version would be false where saying nothing about it would be evasive.
        return scan.Build is null
            ? $"{release}. Its files come from more than one release."
            : $"{release}. This copy matches the verified version.";
    }

    /// <summary>Which outcome the repair is offering, since restore and mitigate are different promises.</summary>
    internal static string RepairSubtitle(ScanResult scan)
    {
        // A copy carrying both damages is repaired in part, so the sentence names what is left behind as
        // well as what is put back: a subtitle mentioning only the repair would imply the copy came out whole.
        var rebuilds = scan.Repairs.Count > 0
            ? $"Rebuilds {Count(scan.Repairs.Count, "file")} and only writes {(scan.Repairs.Count == 1 ? "it" : "them")} if {(scan.Repairs.Count == 1 ? "it matches" : "they match")} the verified version exactly."
            : null;

        var lost = BeyondRepair(scan) is { Count: > 0 } beyond
            ? $"The damage in {Names(beyond.Select(file => file.Name))} cannot be repaired: the original data is lost."
            : null;

        return string.Join(" ", new[] { rebuilds, lost }.Where(sentence => sentence is not null))
            is { Length: > 0 } subtitle ? subtitle : "No damage found in this copy.";
    }

    // Damage the release list records for a file no repair offered to attempt, which is the case the data is gone.
    private static IReadOnlyList<FileScan> BeyondRepair(ScanResult scan)
        => [.. scan.Damaged.Where(file => !scan.Repairs.Any(finding => string.Equals(finding.FileName, file.Name, StringComparison.OrdinalIgnoreCase)))];

    /// <summary>The receipt of the scan, which records what happened rather than repeating what the sections show.</summary>
    internal static IEnumerable<LogEntry> ScanLog(ScanResult scan)
    {
        var present = scan.Files.Count(file => file.Present);
        var matched = scan.Files.Count(file => file.IsRecognized);
        yield return new LogEntry(LogLevel.Info, $"{Count(present, "file")} read; {matched} matched the verified version.");

        foreach (var finding in scan.Repairs)
        {
            yield return new LogEntry(LogLevel.Warning, $"{finding.FileName} does not match its expected structure.");
            yield return new LogEntry(LogLevel.Info, "The damage can be repaired. The result is checked against the verified version before anything is written.");
        }

        foreach (var damaged in BeyondRepair(scan))
        {
            yield return new LogEntry(LogLevel.Warning, $"{damaged.Name} does not match the verified version.");
            yield return new LogEntry(LogLevel.Info, "The missing data cannot be reconstructed, so this damage cannot be repaired.");
        }

        foreach (var withheld in scan.Fixes.Where(fix => !fix.IsKnown))
        {
            yield return withheld.Outcome == FixOutcome.NotAvailable
                ? new LogEntry(LogLevel.Warning, $"No fix is available for {withheld.Fix.TargetFile} in this release.")
                : new LogEntry(LogLevel.Error, $"{withheld.Fix.TargetFile} contains unexpected bytes where the fix would be applied.");
        }

        yield return Conclusion(scan);
    }

    private static LogEntry Conclusion(ScanResult scan)
    {
        if (scan.AlreadyFixed)
        {
            return new LogEntry(LogLevel.Success, $"{Count(scan.Fixes.Count, "fix", "fixes")} {(scan.Fixes.Count == 1 ? "is" : "are")} already in place.");
        }

        if (!scan.FixesOffered) return new LogEntry(LogLevel.Info, "No fixes need to be applied to this copy.");

        return new LogEntry(LogLevel.Success, $"{Count(scan.Fixes.Count, "fix", "fixes")} can be applied.");
    }

    /// <summary>What a run is about to do, named before it starts because the repair's search is not a quick one.</summary>
    internal static string ApplyingLine(PatchPlan plan)
    {
        if (plan.Repairs.Count == 0) return "Applying fixes...";

        return plan.Fixes.Count == 0
            ? $"Rebuilding {Names(plan.Repairs.Select(repair => repair.FileName))}..."
            : $"Rebuilding {Names(plan.Repairs.Select(repair => repair.FileName))} and applying fixes...";
    }

    /// <summary>What a run produced, since the window only ever shows a state it read back off the files.</summary>
    internal static IEnumerable<LogEntry> ApplyResult(PatchRunResult result)
    {
        if (!result.Success)
        {
            yield return new LogEntry(LogLevel.Error, result.FailureReason!);
            yield break;
        }

        // A rebuilt file is the published file, so this is the one outcome the tool may call a restoration.
        if (result.Repaired.Count > 0)
        {
            yield return new LogEntry(LogLevel.Success, $"{Names(result.Repaired)} now {(result.Repaired.Count == 1 ? "matches" : "match")} the verified version, byte for byte.");
        }

        foreach (var fileName in result.Unrepaired)
        {
            yield return new LogEntry(LogLevel.Warning, $"{fileName} was left unchanged: nothing the repair built matched the verified version.");
        }

        if (result.Written.Count > 0)
        {
            yield return new LogEntry(LogLevel.Success, $"{Names(result.Written)} {(result.Written.Count == 1 ? "was" : "were")} patched.");
        }

        if (result.Repaired.Count == 0 && result.Written.Count == 0 && result.Unrepaired.Count == 0)
        {
            yield return new LogEntry(LogLevel.Info, "Nothing needed to be written.");
        }

        if (result.SlabWritten)
        {
            yield return new LogEntry(LogLevel.Success, "The in-game Credits screen now lists what was fixed.");
        }
        else if (result.SlabRefusalReason is not null)
        {
            yield return new LogEntry(LogLevel.Info, $"The game will not show the patch notes on its Credits screen: {result.SlabRefusalReason}.");
        }
    }

    /// <summary>What a restore produced, or why it did not run.</summary>
    internal static LogEntry RestoreResult(bool success, string? failureReason)
        => success ? new LogEntry(LogLevel.Success, "The backup was restored.") : new LogEntry(LogLevel.Error, failureReason!);

    /// <summary>What the rescan found that contradicts what the action just promised, silent when the two agree.</summary>
    internal static IEnumerable<LogEntry> Discrepancies(IReadOnlyList<Discrepancy> discrepancies)
    {
        if (discrepancies.Count == 0) yield break;

        foreach (var discrepancy in discrepancies) yield return new LogEntry(LogLevel.Warning, DiscrepancyLine(discrepancy));

        yield return new LogEntry(LogLevel.Info,
            "The sections above show the current state of the copy. Check whether another process is modifying files in this folder.");
    }

    private static string DiscrepancyLine(Discrepancy discrepancy) => discrepancy.Kind switch
    {
        DiscrepancyKind.FixNotReadBack => $"{discrepancy.FileName} was patched, but reading it back does not show the change.",
        DiscrepancyKind.RepairNotReadBack => $"{discrepancy.FileName} was restored, but reading it back no longer matches the verified version.",
        DiscrepancyKind.ChangedWithoutWriting => $"{discrepancy.FileName} changed on disk during an operation that wrote nothing.",
        DiscrepancyKind.BackupMismatch => $"{discrepancy.FileName} does not match the backup it was just restored from.",
        _ => throw new ArgumentOutOfRangeException(nameof(discrepancy)),
    };

    private static string Names(IEnumerable<string> names)
    {
        var list = names.ToList();

        return list.Count switch
        {
            0 => string.Empty,
            1 => list[0],
            2 => $"{list[0]} and {list[1]}",
            _ => $"{string.Join(", ", list[..^1])}, and {list[^1]}",
        };
    }

    private static string Count(int count, string singular, string? plural = null)
        => count == 1 ? $"1 {singular}" : $"{count} {plural ?? singular + "s"}";
}
