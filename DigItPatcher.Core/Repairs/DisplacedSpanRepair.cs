using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.Repairs;

/// <summary>Undoes a byte inserted into an archive and a byte lost further on, which leaves the span between them displaced.</summary>
public sealed class DisplacedSpanRepair : IRepair
{
    /// <summary>The name this damage goes by, in the release list and in the window alike.</summary>
    public const string Kind = "displaced-span";

    private const int SectorSize = 2048;

    // A candidate costs a hash of the whole archive, so an unbounded search would be a window that never comes back.
    private const int CandidateBudget = 5000;

    /// <inheritdoc/>
    public string Id => Kind;

    /// <inheritdoc/>
    public IReadOnlyList<RepairFinding> Diagnose(GameInstall install)
    {
        var findings = new List<RepairFinding>();

        foreach (var name in install.ArchiveNames)
        {
            try
            {
                if (Faults(install.ReadAll(name)).Count > 0) findings.Add(new RepairFinding(Kind, name));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // An archive the tool cannot read is a fact about the install, reported by the scan's own file pass.
            }
        }

        return findings;
    }

    /// <inheritdoc/>
    public IEnumerable<byte[]> Produce(GameInstall install, RepairFinding finding, CancellationToken token)
    {
        var archive = install.ReadAll(finding.FileName);
        var faults = Faults(archive);
        if (faults.Count == 0) yield break;

        var working = new byte[FrameCodec.WorkingSize];
        var first = faults[0];
        var last = faults[^1];

        // An entry whose frames walk again once one byte is gone puts the insertion inside its own fault, so the
        // search is over that entry's bytes rather than over the archive's.
        var removals = new List<int>();
        for (int at = first.Entry.Start + first.Walk.FaultStart; at < first.Entry.Start + first.Walk.FaultEnd; at++)
        {
            token.ThrowIfCancellationRequested();
            if (SheetChain.Walk(WithoutByte(archive, at, first.Entry), working).IsIntact) removals.Add(at);
        }

        if (removals.Count == 0) yield break;

        // Every candidate removal sits before the last faulting entry, so all of them realign its bytes alike and
        // the second half of the search reads the same either way.
        var realigned = WithoutByte(archive, removals[0]);
        var tail = SheetChain.Walk(realigned.AsSpan(last.Entry.Start, last.Entry.Length), working);
        if (tail.IsIntact) yield break;

        int budget = CandidateBudget;
        foreach (int at in InsertionOrder(last.Entry.Start + tail.FaultStart, last.Entry.Start + tail.FaultEnd))
        {
            for (int value = 0; value <= byte.MaxValue; value++)
            {
                token.ThrowIfCancellationRequested();
                if (!SheetChain.Walk(WithByte(realigned, at, (byte)value, last.Entry), working).IsIntact) continue;

                foreach (var removal in removals)
                {
                    if (budget-- <= 0) yield break;
                    yield return Rebuild(archive, removal, at, (byte)value);
                }
            }
        }
    }

    // The one specimen realigns on a sector boundary, so those positions are tried first. Ordering cannot make
    // a wrong answer right: what a candidate is worth is settled by its hash.
    private static IEnumerable<int> InsertionOrder(int from, int to)
    {
        for (int at = from; at <= to; at++)
        {
            if ((at + 1) % SectorSize == 0) yield return at;
        }

        for (int at = from; at <= to; at++)
        {
            if ((at + 1) % SectorSize != 0) yield return at;
        }
    }

    private static List<(XrsEntry Entry, SheetWalk Walk)> Faults(byte[] archive)
    {
        if (!XrsDirectory.TryRead(archive, out var entries)) return [];

        var working = new byte[FrameCodec.WorkingSize];

        return [.. entries
            .Where(entry => entry.IsSheet)
            .Select(entry => (Entry: entry, Walk: SheetChain.Walk(archive.AsSpan(entry.Start, entry.Length), working)))
            .Where(read => !read.Walk.IsIntact)
            .OrderBy(read => read.Entry.Start)];
    }

    /// <summary>An archive with one byte dropped and one byte put back, which is the whole of the transformation.</summary>
    private static byte[] Rebuild(byte[] archive, int removeAt, int insertAt, byte value)
    {
        var candidate = new byte[archive.Length];
        archive.AsSpan(0, removeAt).CopyTo(candidate);
        archive.AsSpan(removeAt + 1, insertAt - removeAt).CopyTo(candidate.AsSpan(removeAt));
        candidate[insertAt] = value;
        archive.AsSpan(insertAt + 1).CopyTo(candidate.AsSpan(insertAt + 1));
        return candidate;
    }

    // The archive keeps its length, the byte the damage lost standing in as a zero at the end: the entry the search
    // is about to walk can be the archive's last one, and a buffer one byte short would not hold it.
    private static byte[] WithoutByte(byte[] archive, int at)
    {
        var bytes = new byte[archive.Length];
        archive.AsSpan(0, at).CopyTo(bytes);
        archive.AsSpan(at + 1).CopyTo(bytes.AsSpan(at));
        return bytes;
    }

    // One entry's bytes as they would read with a byte gone or added, without rebuilding the archive to look at them.
    private static byte[] WithoutByte(byte[] archive, int at, XrsEntry entry)
    {
        var bytes = new byte[entry.Length];
        archive.AsSpan(entry.Start, at - entry.Start).CopyTo(bytes);
        archive.AsSpan(at + 1, entry.End - at).CopyTo(bytes.AsSpan(at - entry.Start));
        return bytes;
    }

    private static byte[] WithByte(byte[] archive, int at, byte value, XrsEntry entry)
    {
        var bytes = new byte[entry.Length];
        archive.AsSpan(entry.Start, at - entry.Start).CopyTo(bytes);
        bytes[at - entry.Start] = value;
        archive.AsSpan(at, entry.End - at - 1).CopyTo(bytes.AsSpan(at - entry.Start + 1));
        return bytes;
    }
}
