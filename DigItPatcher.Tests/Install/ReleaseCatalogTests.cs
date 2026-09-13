using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests asserting the embedded release list literally, so a silent edit to it fails the build.</summary>
public class ReleaseCatalogTests
{
    private const string CleanDigit2 = "0cda22fafe842e6013018981632dadd0c29951cee1782311e4f2a4a53e1509ff";
    private const string DisplacedSpanDigit2 = "67199c49aa35d59db1ba358bc4057955f8d8f22497fc50ab5d218d477ccda232";
    private const string LostSectorsDigit1 = "a4c0478c4b5ae42c0bb6e3388fd40e8d822753462c6972888323a1647efde4a6";
    private const string DuplicatedSectorsDigit0 = "404550c7887844b350e4550832a42031e0361945e06970b937509eea706ede49";
    private const string SharewareMainExe = "acc88a12192229ca6d1466ddba3136a5579a74c1a0138cc2a545425dfded5c21";
    private const string ManaccomMainExe = "7d151d0db8654f98857a7b5254f23147560d2acc601ca141de86b43d5fa9ec46";
    private const string ManaccomDigit0 = "cc321b2604963f1fcec81b75b77db5b66619e98fd0963e41d21338d586be144e";
    private const string CleanFullDigit0 = "d31be8576962c521e092049b1bdbed91887ac7adbed0f13ad667a00242117e85";
    private const string SharedRtmExe = "10acd85af3767634450a793f2af1e057de888621cbfc578d35d3fa91abc09661";

    private readonly ReleaseCatalog _catalog = ReleaseCatalog.Embedded;

    [Fact]
    public void TheListHoldsThreeBuildsAndTheTenShippedFiles()
    {
        Assert.Equal(["full", "manaccom", "shareware"], _catalog.Builds.Select(build => build.Id));
        Assert.Equal(
            ["DIGIT.EXE", "MAIN.EXE", "DIGIT0.XRS", "DIGIT1.XRS", "DIGIT2.XRS", "DIGIT3.XRS", "DIGIT4.XRS",
             "DIGITX.XRS", "DPMI16BI.OVL", "RTM.EXE"],
            _catalog.Files.Select(file => file.Name));
    }

    [Fact]
    public void CleanContentsAreRecognizedAndCarryTheirBuild()
    {
        var identified = _catalog.Identify("DIGIT2.XRS", CleanDigit2);

        Assert.NotNull(identified);
        Assert.True(identified.IsClean);
        Assert.Equal(["full", "manaccom"], identified.Builds);
    }

    [Fact]
    public void TheThreeFilesSharedByEveryBuildIdentifyNothingOnTheirOwn()
    {
        var identified = _catalog.Identify("RTM.EXE", SharedRtmExe);

        Assert.NotNull(identified);
        Assert.Equal(["full", "manaccom", "shareware"], identified.Builds);
    }

    [Theory]
    [InlineData("DIGIT2.XRS", DisplacedSpanDigit2, "displaced-span", new[] { "full", "manaccom" })]
    [InlineData("DIGIT1.XRS", LostSectorsDigit1, "lost-sectors", new[] { "full", "manaccom" })]
    [InlineData("DIGIT0.XRS", DuplicatedSectorsDigit0, "duplicated-sectors", new[] { "shareware" })]
    public void EachDamagedCopyIsRecognizedAsItsOwnBuildAndNamesItsDamage(string fileName, string hash, string damage, string[] builds)
    {
        var identified = _catalog.Identify(fileName, hash);

        Assert.NotNull(identified);
        Assert.False(identified.IsClean);
        Assert.Equal(damage, identified.Damage);
        Assert.Equal(builds, identified.Builds);
    }

    [Fact]
    public void ContentsOnNoListAreNotRecognized()
        => Assert.Null(_catalog.Identify("DIGIT2.XRS", new string('0', 64)));

    [Fact]
    public void AFileTheToolDoesNotShipIsNotRecognized()
        => Assert.Null(_catalog.Identify("SETUP.EXE", SharedRtmExe));

    [Fact]
    public void CleanContentsExcludeTheDamagedOnes()
    {
        var clean = _catalog.CleanContentsOf("DIGIT2.XRS");

        Assert.Equal([CleanDigit2], clean.Select(contents => contents.Sha256));
    }

    [Fact]
    public void OnlyTheSharewareIsMissingTheLaterArchives()
    {
        Assert.True(_catalog.IsShippedBy("DIGIT2.XRS", "full"));
        Assert.True(_catalog.IsShippedBy("DIGIT2.XRS", "manaccom"));
        Assert.False(_catalog.IsShippedBy("DIGIT2.XRS", "shareware"));
        Assert.True(_catalog.IsShippedBy("DIGIT1.XRS", "shareware"));
    }

    [Fact]
    public void TheSharewareExecutablesAreItsOwnFilesRatherThanTheFullReleases()
    {
        var identified = _catalog.Identify("MAIN.EXE", SharewareMainExe);

        Assert.NotNull(identified);
        Assert.Equal(["shareware"], identified.Builds);
    }

    [Fact]
    public void TheManaccomEditionIsTwoFilesOfItsOwnAndEightOfTheFullReleases()
    {
        Assert.Equal(["manaccom"], _catalog.Identify("MAIN.EXE", ManaccomMainExe)?.Builds);
        Assert.Equal(["manaccom"], _catalog.Identify("DIGIT0.XRS", ManaccomDigit0)?.Builds);
        Assert.Equal(["full"], _catalog.Identify("DIGIT0.XRS", CleanFullDigit0)?.Builds);
    }
}
