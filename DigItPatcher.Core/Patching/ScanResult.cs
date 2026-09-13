using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Core.Patching;

/// <summary>What the scan read about one of the game's files.</summary>
public sealed record FileScan(string Name, bool Present, string? Sha256, KnownFileContents? Identity)
{
    /// <summary>Whether these contents appear on the tool's list at all.</summary>
    public bool IsRecognized => Identity is not null;

    /// <summary>The damage recorded for these contents, or none if the file is a verified copy.</summary>
    public string? Damage => Identity?.Damage;
}

/// <summary>What a row of the fix list has to say about the copy, which is nothing in the ordinary case.</summary>
public enum FixOutcome
{
    /// <summary>The site holds the game's own bytes and the fix has not been applied.</summary>
    NotApplied,

    /// <summary>The site holds the bytes this tool writes.</summary>
    AlreadyFixed,

    /// <summary>A build we recognize and have not worked this fix out for.</summary>
    NotAvailable,

    /// <summary>The site holds neither sequence, in a copy that is not a build left unsolved.</summary>
    UnexpectedBytes,
}

/// <summary>What the scan read at one fix's own site.</summary>
public sealed record FixScan(IFix Fix, FixState State, FixOutcome Outcome)
{
    /// <summary>Whether the site reads a value the tool put there or expected to find, which is what licenses the fix.</summary>
    public bool IsKnown => Outcome is FixOutcome.NotApplied or FixOutcome.AlreadyFixed;
}

/// <summary>Everything one pass over an install read, which is what the window's two sections render.</summary>
public sealed record ScanResult(
    string Folder,
    IReadOnlyList<FileScan> Files,
    IReadOnlyList<RepairFinding> Repairs,
    IReadOnlyList<FixScan> Fixes,
    KnownBuild? Build)
{
    /// <summary>Whether anything in this copy is damage a repair is willing to attempt.</summary>
    public bool RepairsOffered => Repairs.Count > 0;

    /// <summary>Whether the copy could be named as one of the builds the tool knows.</summary>
    public bool IsRecognized => Build is not null;

    /// <summary>The files present but on no list, which is the ordinary shape of a copy this tool has patched.</summary>
    public IReadOnlyList<FileScan> Unrecognized => [.. Files.Where(file => file.Present && !file.IsRecognized)];

    /// <summary>The files whose contents are recorded as damaged.</summary>
    public IReadOnlyList<FileScan> Damaged => [.. Files.Where(file => file.Damage is not null)];

    /// <summary>Fixes are all or nothing, so one site reading an unknown value withdraws the whole block.</summary>
    public bool FixesOffered => Fixes.Count > 0 && Fixes.All(fix => fix.IsKnown);

    /// <summary>Whether every site already reads the bytes this tool writes.</summary>
    public bool AlreadyFixed => Fixes.Count > 0 && Fixes.All(fix => fix.Outcome == FixOutcome.AlreadyFixed);
}
