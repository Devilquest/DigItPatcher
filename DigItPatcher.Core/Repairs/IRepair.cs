using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.Repairs;

/// <summary>One file a repair believes it can rebuild, which is a report about the file and not yet a promise.</summary>
/// <param name="RepairId">The repair that found it.</param>
/// <param name="FileName">The game file the damage is in.</param>
public sealed record RepairFinding(string RepairId, string FileName);

/// <summary>A kind of damage the tool can undo, which is free to look at a copy and conclude that it cannot.</summary>
public interface IRepair
{
    /// <summary>Stable identifier used to look this repair up and to name it in the backup's own record.</summary>
    string Id { get; }

    /// <summary>Reports every file in the install carrying damage of this kind, writing nothing.</summary>
    IReadOnlyList<RepairFinding> Diagnose(GameInstall install);

    /// <summary>Builds whole candidate files in memory, best first, for the caller to weigh against a published hash.</summary>
    IEnumerable<byte[]> Produce(GameInstall install, RepairFinding finding, CancellationToken token);
}
