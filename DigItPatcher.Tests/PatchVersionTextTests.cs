using DigItPatcher.Core;

namespace DigItPatcher.Tests;

/// <summary>Tests holding that the patch's version is there and shaped the way both in-game places assume.</summary>
public class PatchVersionTextTests
{
    [Fact]
    public void TheEmbeddedFileParsesToAMajorMinorVersion()
    {
        var text = PatchVersionText.Embedded();

        Assert.Matches(@"^\d+\.\d+$", text.Version);
    }

    [Fact]
    public void AVersionMatchingTheShapeIsAccepted()
        => Assert.Equal("1.2", PatchVersionText.Parse("# Version\n1.2").Version);

    [Theory]
    [InlineData("1")]
    [InlineData("1.2.3")]
    [InlineData("v1.2")]
    [InlineData("")]
    public void AVersionNotMatchingTheShapeIsRejected(string version)
        => Assert.Throws<InvalidOperationException>(() => PatchVersionText.Parse($"# Version\n{version}"));

    [Fact]
    public void AMissingSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PatchVersionText.Parse(""));

    [Fact]
    public void AnUnknownSectionIsRejected()
        => Assert.Throws<InvalidOperationException>(() => PatchVersionText.Parse("# Version\n1.0\n\n# Extra\nSomething"));
}
