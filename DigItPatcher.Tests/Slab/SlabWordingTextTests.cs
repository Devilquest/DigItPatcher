using DigItPatcher.Core;
using DigItPatcher.Core.Slab;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the slab's own wording and the placeholders its identity fills.</summary>
public class SlabWordingTextTests
{
    [Fact]
    public void TheEmbeddedFileParsesAndKeepsItsPlaceholders()
    {
        var text = SlabWordingText.Embedded();

        Assert.Equal("Patch Notes", text.Heading);
        Assert.Contains("{version}", text.Credit);
        Assert.Contains("{author}", text.Author);
    }

    [Fact]
    public void ResolvingFillsEveryPlaceholderFromThePatchVersionAndTheIdentity()
    {
        var text = SlabWordingText.Parse("# Heading\nPatch Notes\n\n# Credit\nDig It! Bug Fixes {version} by\n\n# Author\n{author}");
        var identity = new AppIdentity("Dig It! Patcher", "1.2.3", "Devilquest");

        var wording = text.Resolve(identity, "1.0");

        Assert.Equal("Patch Notes", wording.Heading);
        Assert.Equal("Dig It! Bug Fixes 1.0 by", wording.Credit);
        Assert.Equal("Devilquest", wording.Author);
    }

    [Fact]
    public void AMissingSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => SlabWordingText.Parse("# Heading\nPatch Notes"));
}
