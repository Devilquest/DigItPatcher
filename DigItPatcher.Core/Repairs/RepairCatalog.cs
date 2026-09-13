namespace DigItPatcher.Core.Repairs;

/// <summary>The repairs the tool offers, in the order it attempts them.</summary>
public static class RepairCatalog
{
    /// <summary>Every registered repair.</summary>
    public static IReadOnlyList<IRepair> All { get; } = [new DisplacedSpanRepair()];
}
