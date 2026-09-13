using DigItPatcher.Core.Fixes;

namespace DigItPatcher.Tests;

/// <summary>Tests holding that every fix's words are there and that a malformed file fails here, not in a run.</summary>
public class FixTextTests
{
    private const string Complete = """
        # Name
        A name

        # What Is Wrong
        Something.

        # Why It Happens
        A reason.

        # What Changes
        An outcome.

        # Slab Line
        One line
        """;

    [Fact]
    public void EveryRegisteredFixHasAFileWithEverySection()
    {
        Assert.All(FixCatalog.All, fix =>
        {
            var text = FixText.For(fix.Id);

            Assert.All(
                new[] { text.Name, text.WhatIsWrong, text.WhyItHappens, text.WhatChanges, text.SlabLine },
                section => Assert.False(string.IsNullOrWhiteSpace(section)));
        });
    }

    [Fact]
    public void TheSlabLineIsOneLine()
        => Assert.All(FixCatalog.All, fix => Assert.DoesNotContain('\n', FixText.For(fix.Id).SlabLine));

    [Fact]
    public void AParagraphSplitOverSeveralLinesIsReadAsOne()
    {
        var text = FixText.Parse("Test", Complete.Replace("Something.", "Something,\nand more of it."));

        Assert.Equal("Something, and more of it.", text.WhatIsWrong);
    }

    [Theory]
    [InlineData("# Slab Line\nOne line", "missing")]
    [InlineData("# Name\nA name", "missing")]
    public void AFileMissingASectionIsRejected(string contents, string _)
        => Assert.Throws<InvalidOperationException>(() => FixText.Parse("Test", contents));

    [Fact]
    public void AnEmptySectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => FixText.Parse("Test", Complete.Replace("A reason.", "")));

    [Fact]
    public void AnUnknownSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => FixText.Parse("Test", Complete + "\n\n# Extra\nSomething"));

    [Fact]
    public void ARepeatedSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => FixText.Parse("Test", Complete + "\n\n# Name\nAgain"));

    [Fact]
    public void AFixWithNoFileIsReportedByName()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => FixText.For("NoSuchFix"));

        Assert.Contains("NoSuchFix", thrown.Message);
    }
}
