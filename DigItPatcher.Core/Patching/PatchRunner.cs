using System.Security.Cryptography;
using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.MenuLine;
using DigItPatcher.Core.Repairs;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Core.Patching;

/// <summary>What a run produced: the files it put back, the ones it could not, the ones it patched, whether it
/// added a slab or a menu line, or why either was left out.</summary>
public sealed record PatchRunResult(
    bool Success,
    IReadOnlyList<string> Repaired,
    IReadOnlyList<string> Unrepaired,
    IReadOnlyList<string> Written,
    string? FailureReason,
    bool SlabWritten = false,
    string? SlabRefusalReason = null,
    bool MenuLineWritten = false,
    string? MenuLineRefusalReason = null);

/// <summary>Runs a plan: backs up its files once, restores what it can prove, applies every fix in it, then
/// composes a slab and a main menu line from the fixes the copy ends up carrying.</summary>
public sealed class PatchRunner
{
    private readonly ReleaseCatalog _catalog;
    private readonly IReadOnlyList<IRepair> _repairs;
    private readonly IReadOnlyList<IFix> _fixes;

    /// <summary>A runner over the tool's own release list, repair set, and fix set.</summary>
    public PatchRunner() : this(ReleaseCatalog.Embedded, RepairCatalog.All, FixCatalog.All) { }

    /// <summary>A runner over a release list, repair set, and fix set supplied by the caller.</summary>
    public PatchRunner(ReleaseCatalog catalog, IReadOnlyList<IRepair> repairs, IReadOnlyList<IFix>? fixes = null)
    {
        _catalog = catalog;
        _repairs = repairs;
        _fixes = fixes ?? FixCatalog.All;
    }

    /// <summary>Confirms every target file can be written, backs up the plan's files, then repairs, fixes, composes
    /// a slab and composes a menu line.</summary>
    /// <param name="identity">Composed into the slab; a run with none never attempts one, whatever the plan carries.</param>
    /// <param name="token">Watched while candidates are being searched for, and not once writing has started.</param>
    public PatchRunResult Run(GameInstall install, PatchPlan plan, KnownBuild? build, AppIdentity? identity = null, CancellationToken token = default)
    {
        if (plan.IsEmpty) return new PatchRunResult(true, [], [], [], null);

        var unwritable = plan.FilesToBackup.Where(file => !install.CanWriteTo(file)).ToList();
        if (unwritable.Count > 0)
        {
            return new PatchRunResult(false, [], [], [], $"{string.Join(", ", unwritable)} could not be opened for writing.");
        }

        // A repair searches before it writes, so the search runs while the install is still untouched and the backup
        // is taken only once something is going to be written to it.
        var proved = new List<(string FileName, byte[] Contents)>();
        var unrepaired = new List<string>();
        foreach (var finding in plan.Repairs)
        {
            var candidate = Prove(install, finding, token);
            if (candidate is null) unrepaired.Add(finding.FileName);
            else proved.Add((finding.FileName, candidate));
        }

        if (proved.Count == 0 && plan.Fixes.Count == 0)
        {
            return new PatchRunResult(true, [], unrepaired, [], null);
        }

        BackupSet.EnsureCovers(install, plan.FilesToBackup);

        // Repaired files are written before fixes so a fix is always applied on top of the file it was worked out
        // against, never the other way round.
        var repaired = new List<string>();
        foreach (var (fileName, contents) in proved)
        {
            install.WriteAll(fileName, contents);
            repaired.Add(fileName);
        }

        var written = new List<string>();
        foreach (var fix in plan.Fixes)
        {
            using var stream = install.OpenReadWrite(fix.TargetFile);
            fix.Apply(stream);
            written.Add(fix.TargetFile);
        }

        bool slabWritten = false;
        string? slabRefusalReason = null;
        if (plan.Fixes.Count > 0 && identity is not null)
        {
            (slabWritten, slabRefusalReason) = ComposeSlab(install, identity, written);
        }

        bool menuLineWritten = false;
        string? menuLineRefusalReason = null;
        if (plan.Fixes.Count > 0)
        {
            // Composed over whatever the install holds now, so it reads the archive the slab above just wrote
            // and appends after it, rather than the two colliding over the same base.
            (menuLineWritten, menuLineRefusalReason) = ComposeMenuLine(install, written);
        }

        BackupSet.AppendManifestEntry(install, ManifestEntry(plan, build, repaired, slabWritten, menuLineWritten));

        return new PatchRunResult(true, repaired, unrepaired, written, null,
            slabWritten, slabRefusalReason, menuLineWritten, menuLineRefusalReason);
    }

    // The slab lists the fixes the copy now has, not the ones this run happened to change, so every fix's site
    // is read again after the writes above rather than reused from the plan.
    private (bool Written, string? RefusalReason) ComposeSlab(GameInstall install, AppIdentity identity, List<string> written)
    {
        var patched = _fixes.Where(fix => ReadState(install, fix) == FixState.Patched).ToList();
        if (!SlabComposer.TryCompose(install, identity, patched, out var composition, out var refusal))
        {
            return (false, refusal!.Reason);
        }

        install.WriteAll("DIGIT0.XRS", composition!.Archive);
        install.WriteAll("MAIN.EXE", composition.MainExe);
        if (!written.Contains("DIGIT0.XRS", StringComparer.OrdinalIgnoreCase)) written.Add("DIGIT0.XRS");
        if (!written.Contains("MAIN.EXE", StringComparer.OrdinalIgnoreCase)) written.Add("MAIN.EXE");
        return (true, null);
    }

    // No mod carries a name to append after the patch's version, so the mod list is passed empty.
    private static (bool Written, string? RefusalReason) ComposeMenuLine(GameInstall install, List<string> written)
    {
        if (!MenuLineComposer.TryCompose(install, [], out var composition, out var refusal))
        {
            return (false, refusal!.Reason);
        }

        install.WriteAll("DIGIT0.XRS", composition!.Archive);
        install.WriteAll("MAIN.EXE", composition.MainExe);
        if (!written.Contains("DIGIT0.XRS", StringComparer.OrdinalIgnoreCase)) written.Add("DIGIT0.XRS");
        if (!written.Contains("MAIN.EXE", StringComparer.OrdinalIgnoreCase)) written.Add("MAIN.EXE");
        return (true, null);
    }

    private static FixState ReadState(GameInstall install, IFix fix)
    {
        if (!install.Has(fix.TargetFile)) return FixState.FileMissing;

        try
        {
            using var stream = install.OpenRead(fix.TargetFile);
            return fix.GetState(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return FixState.FileMissing;
        }
    }

    // The candidate a repair proposes is worth nothing until it equals a file the tool can name as correct, so the
    // first one whose hash is one of ours is the answer and the rest are never built.
    private byte[]? Prove(GameInstall install, RepairFinding finding, CancellationToken token)
    {
        var repair = _repairs.FirstOrDefault(candidate => candidate.Id == finding.RepairId);
        if (repair is null) return null;

        var clean = _catalog.CleanContentsOf(finding.FileName);
        if (clean.Count == 0) return null;

        return repair.Produce(install, finding, token)
            .FirstOrDefault(candidate => clean.Any(known => string.Equals(known.Sha256, Fingerprint(candidate), StringComparison.OrdinalIgnoreCase)));
    }

    private static string Fingerprint(byte[] contents) => Convert.ToHexStringLower(SHA256.HashData(contents));

    private static string ManifestEntry(PatchPlan plan, KnownBuild? build, IReadOnlyList<string> repaired, bool slabWritten, bool menuLineWritten)
    {
        var release = build?.Name ?? "an unrecognized copy";
        var did = new List<string>();
        if (repaired.Count > 0) did.Add($"Restored: {string.Join(", ", repaired)}");
        if (plan.Fixes.Count > 0) did.Add($"Applied: {string.Join(", ", plan.Fixes.Select(fix => fix.Id))}");
        if (slabWritten) did.Add("Added patch notes to the Credits screen");
        if (menuLineWritten) did.Add("Added a bug-fixes line to the main menu");

        return $"{DateTime.Now:yyyy-MM-dd HH:mm}: {release}. {string.Join(". ", did)}.";
    }
}
