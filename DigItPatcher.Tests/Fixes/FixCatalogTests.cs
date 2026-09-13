using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests holding the properties every registered fix has to have, whatever the set grows to.</summary>
public class FixCatalogTests
{
    [Fact]
    public void TheCatalogHoldsTheFixesThisToolOffers()
    {
        // Every rule below passes on an empty catalog, so without this one a tool that offers nothing reads
        // as a tool with nothing wrong with it.
        Assert.Equal(["CompletionPercentage", "SoundEffectsVolume"], FixCatalog.All.Select(fix => fix.Id));
    }

    [Fact]
    public void EveryFixTargetsAFileTheGameShips()
    {
        var shipped = ReleaseCatalog.Embedded.Files.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(FixCatalog.All, fix => Assert.Contains(fix.TargetFile, shipped));
    }

    [Fact]
    public void EveryFixHasAnIdOfItsOwn()
    {
        var ids = FixCatalog.All.Select(fix => fix.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void AFixWritesAsManyBytesAsItExpectsToFind()
        => Assert.All(FixCatalog.All, fix => Assert.Equal(fix.OriginalBytes.Length, fix.PatchedBytes.Length));

    [Fact]
    public void NoTwoFixesWriteToTheSameBytesOfTheSameFile()
    {
        var pairs =
            from left in FixCatalog.All
            from right in FixCatalog.All
            where !ReferenceEquals(left, right)
                && string.Equals(left.TargetFile, right.TargetFile, StringComparison.OrdinalIgnoreCase)
                && left.Origin == right.Origin
            select (left, right);

        Assert.All(pairs, pair => Assert.True(
            pair.left.Offset + pair.left.OriginalBytes.Length <= pair.right.Offset
                || pair.right.Offset + pair.right.OriginalBytes.Length <= pair.left.Offset,
            $"'{pair.left.Id}' and '{pair.right.Id}' overlap in {pair.left.TargetFile}."));
    }
}
