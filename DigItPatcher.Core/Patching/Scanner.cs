using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Core.Patching;

/// <summary>Reads an install and reports what is there, without writing anything or deciding anything.</summary>
public sealed class Scanner
{
    private readonly ReleaseCatalog _catalog;
    private readonly IReadOnlyList<IFix> _fixes;
    private readonly IReadOnlyList<IRepair> _repairs;

    /// <summary>A scanner over the tool's own release list, fix set, and repair set.</summary>
    public Scanner() : this(ReleaseCatalog.Embedded, FixCatalog.All, RepairCatalog.All) { }

    /// <summary>A scanner over a release list, fix set, and repair set supplied by the caller.</summary>
    public Scanner(ReleaseCatalog catalog, IReadOnlyList<IFix> fixes, IReadOnlyList<IRepair> repairs)
    {
        _catalog = catalog;
        _fixes = fixes;
        _repairs = repairs;
    }

    /// <summary>Fingerprints every shipped file, diagnoses every repair and reads every fix at its own site.</summary>
    public ScanResult Scan(GameInstall install)
    {
        var files = ScanFiles(install);
        var build = NameTheBuild(files);

        var repairs = _repairs
            .SelectMany(repair => repair.Diagnose(install))
            .Where(finding => IsWorthAttempting(files, finding))
            .ToList();

        var fixes = _fixes
            .Select(fix => ReadSite(install, fix))
            .Select(read => new FixScan(read.Fix, read.State, OutcomeOf(read.Fix, read.State, build)))
            .ToList();

        return new ScanResult(install.Folder, files, repairs, fixes, build);
    }

    // Recognizing the contents narrows which target is worth trying and nothing more: a file recorded as carrying
    // damage of another kind is left alone, and a file on no list is attempted like any other.
    private static bool IsWorthAttempting(IEnumerable<FileScan> files, RepairFinding finding)
    {
        var file = files.FirstOrDefault(read => string.Equals(read.Name, finding.FileName, StringComparison.OrdinalIgnoreCase));
        return file?.Identity is null || file.Damage == finding.RepairId;
    }

    // Internal so a test can name the build of a folder without paying for the repair and fix passes beside it.
    internal IReadOnlyList<FileScan> ScanFiles(GameInstall install)
        => [.. _catalog.Files.Select(known => ScanFile(install, known.Name))];

    private FileScan ScanFile(GameInstall install, string fileName)
    {
        if (!install.Has(fileName)) return new FileScan(fileName, Present: false, null, null);
        if (!install.TryHash(fileName, out var sha256)) return new FileScan(fileName, Present: true, null, null);

        return new FileScan(fileName, Present: true, sha256, _catalog.Identify(fileName, sha256));
    }

    private static (IFix Fix, FixState State) ReadSite(GameInstall install, IFix fix)
    {
        if (!install.Has(fix.TargetFile)) return (fix, FixState.FileMissing);

        try
        {
            using var stream = install.OpenRead(fix.TargetFile);
            return (fix, fix.GetState(stream));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (fix, FixState.FileMissing);
        }
    }

    // Naming a build we recognize and have not solved puts the limitation on us; anything else means
    // something has been in that file, which is a fact about the file rather than about which release it is.
    private static FixOutcome OutcomeOf(IFix fix, FixState state, KnownBuild? build) => state switch
    {
        FixState.Original => FixOutcome.NotApplied,
        FixState.Patched => FixOutcome.AlreadyFixed,
        _ when build is not null && !fix.DerivedFor.Contains(build.Id, StringComparer.OrdinalIgnoreCase)
            => FixOutcome.NotAvailable,
        _ => FixOutcome.UnexpectedBytes,
    };

    // The install-level word is derived from the per-file verdicts, never the other way round: a copy this
    // tool has patched holds files that match nothing, and the archives beside them still name their build.
    // Internal so a test can name a build from hashes alone, with no copy of the game present.
    internal KnownBuild? NameTheBuild(IEnumerable<FileScan> files)
    {
        var identified = files.Where(file => file.IsRecognized).Select(file => file.Identity!).ToList();
        if (identified.Count == 0) return null;

        var shared = identified
            .Select(contents => contents.Builds.ToHashSet(StringComparer.OrdinalIgnoreCase))
            .Aggregate((left, right) => [.. left.Intersect(right, StringComparer.OrdinalIgnoreCase)]);

        return shared.Count == 1 ? _catalog.Builds.FirstOrDefault(build => build.Id == shared.Single()) : null;
    }
}
