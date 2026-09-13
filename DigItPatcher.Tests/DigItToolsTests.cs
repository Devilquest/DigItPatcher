using DigItPatcher.Core;

namespace DigItPatcher.Tests;

/// <summary>Tests holding that the tool list is complete, in order, and marks the application showing it.</summary>
public class DigItToolsTests
{
    /// <summary>The header reads as one sentence, whatever the link does to it in the markup.</summary>
    [Fact]
    public void TheHeaderStillReadsAsOneSentenceAroundTheAuthor()
    {
        Assert.Equal(
            "Free, unofficial tools for Dig It!, all made by Devilquest.",
            DigItTools.HeaderLead + DigItTools.Author + DigItTools.HeaderEnd);
    }

    [Fact]
    public void TheToolsAreListedInTheAgreedOrder()
    {
        Assert.Equal(
            ["Dig It! Explorer", "Dig It! Atlas", "Dig It! Patcher"],
            DigItTools.All.Select(tool => tool.Name));
    }

    [Fact]
    public void EveryToolStatesWhatItIsWhatItDoesAndWhereItLives()
    {
        Assert.All(DigItTools.All, tool =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tool.Name));
            Assert.False(string.IsNullOrWhiteSpace(tool.Kind));
            Assert.False(string.IsNullOrWhiteSpace(tool.Summary));
            Assert.True(tool.Link.IsAbsoluteUri);
        });
    }

    /// <summary>Each row reaches its own tool, so a placeholder standing in for a real address is a failure.</summary>
    [Fact]
    public void NoRowStandsOnTheAuthorProfileAndNoTwoShareALink()
    {
        Assert.All(DigItTools.All, tool => Assert.NotEqual(DigItTools.AuthorProfile, tool.Link));

        Assert.Equal(DigItTools.All.Count, DigItTools.All.Select(tool => tool.Link).Distinct().Count());
    }

    [Fact]
    public void ExactlyOneRowIsTheApplicationShowingTheWindow()
    {
        var here = Assert.Single(DigItTools.All, tool => tool.IsThisApp);

        Assert.Equal("Dig It! Patcher", here.Name);
    }

    /// <summary>The window is a signpost, so a summary says what a tool does and never how good it is.</summary>
    [Theory]
    [InlineData("best")]
    [InlineData("easy")]
    [InlineData("powerful")]
    [InlineData("simply")]
    [InlineData("ultimate")]
    public void NoSummarySellsAnything(string word)
    {
        Assert.All(DigItTools.All, tool => Assert.DoesNotContain(word, tool.Summary, StringComparison.OrdinalIgnoreCase));
    }
}
