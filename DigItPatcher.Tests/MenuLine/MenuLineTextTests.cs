using DigItPatcher.Core.MenuLine;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the main menu's own line: its wording, and how the version and any mods fill it.</summary>
public class MenuLineTextTests
{
    [Fact]
    public void TheEmbeddedFileParsesAndKeepsItsPlaceholder()
        => Assert.Contains("{version}", MenuLineText.Embedded().Line);

    [Fact]
    public void ComposingWithNoModsFillsOnlyTheVersion()
    {
        var text = MenuLineText.Parse("# Line\nBUG FIXES {version}");

        Assert.Equal("BUG FIXES 1.2", text.Compose("1.2", []));
    }

    [Fact]
    public void ComposingWithModsAppendsEachNameSeparatedByAPlus()
    {
        var text = MenuLineText.Parse("# Line\nBUG FIXES {version}");

        Assert.Equal("BUG FIXES 1.2 + TURBO KIT", text.Compose("1.2", ["TURBO KIT"]));
        Assert.Equal("BUG FIXES 1.2 + TURBO KIT + SUPER JUMP", text.Compose("1.2", ["TURBO KIT", "SUPER JUMP"]));
    }

    [Fact]
    public void AMissingSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => MenuLineText.Parse(""));

    [Fact]
    public void AnUnknownSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => MenuLineText.Parse("# Line\nBUG FIXES {version}\n\n# Extra\nSomething"));
}
