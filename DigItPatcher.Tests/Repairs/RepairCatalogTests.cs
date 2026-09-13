using DigItPatcher.Core.Repairs;

namespace DigItPatcher.Tests;

/// <summary>Tests holding the repair roster the tool wires into its scanner and its runner by default.</summary>
public class RepairCatalogTests
{
    [Fact]
    public void TheCatalogHoldsTheRepairsThisToolOffers()
    {
        // Nothing else in the suite reads this list: every test builds its repairs itself, so an empty
        // catalog would leave a tool that repairs nothing looking exactly like a tool with nothing to repair.
        Assert.Equal([DisplacedSpanRepair.Kind], RepairCatalog.All.Select(repair => repair.Id));
    }

    [Fact]
    public void EveryRepairHasAnIdOfItsOwn()
    {
        var ids = RepairCatalog.All.Select(repair => repair.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }
}
