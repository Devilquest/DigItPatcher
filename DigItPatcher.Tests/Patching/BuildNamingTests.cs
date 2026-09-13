using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.Tests;

/// <summary>Tests naming a build from a whole copy's hashes against the embedded release list, with no copy of the game present.</summary>
public class BuildNamingTests
{
    private const string Absent = "";

    private const string FullDigitExe = "7cfae5ee31298fba2abc4f10b33f983ad419186d3d12e989d2f40bf55c548f5e";
    private const string SharewareDigitExe = "b04a0a1b96535532d016bb4753d29a8f72041781024291491554658e483b366e";

    private const string FullMainExe = "ff8e4da7332e173692d529cd790ac6dc1f1f99bb2ac6a638afe11b64c7230820";
    private const string ManaccomMainExe = "7d151d0db8654f98857a7b5254f23147560d2acc601ca141de86b43d5fa9ec46";
    private const string SharewareMainExe = "acc88a12192229ca6d1466ddba3136a5579a74c1a0138cc2a545425dfded5c21";

    private const string FullDigit0 = "d31be8576962c521e092049b1bdbed91887ac7adbed0f13ad667a00242117e85";
    private const string ManaccomDigit0 = "cc321b2604963f1fcec81b75b77db5b66619e98fd0963e41d21338d586be144e";
    private const string SharewareDigit0 = "6e431174ee598b1938a70442323cd959eb6aceb40283f9ee1ff7e722c916a638";
    private const string DuplicatedSectorsDigit0 = "404550c7887844b350e4550832a42031e0361945e06970b937509eea706ede49";

    private const string FullDigit1 = "3ecf93043e8d4bc33e6ac66db38558ef859e50705bf476c8f884a21dafc12861";
    private const string SharewareDigit1 = "5d0ab379e28ebdff5e308485cff115209e28d05867153aed1b677517679699a1";
    private const string LostSectorsDigit1 = "a4c0478c4b5ae42c0bb6e3388fd40e8d822753462c6972888323a1647efde4a6";

    private const string FullDigit2 = "0cda22fafe842e6013018981632dadd0c29951cee1782311e4f2a4a53e1509ff";
    private const string DisplacedSpanDigit2 = "67199c49aa35d59db1ba358bc4057955f8d8f22497fc50ab5d218d477ccda232";

    private const string FullDigit3 = "c7730fc551ed323dc83d69a6c4d80a0f4dc565399e98e0a3517f4cb0a417be0c";
    private const string FullDigit4 = "e1e1a3322884e6f9e969044fa150dd54a09a55366e59d2a54893903df8a10a3d";

    private const string SharedDigitX = "706cc1135685c957b09dcf4934e7365c700f51879972d681412ee08f80d5b16d";
    private const string SharedDpmi = "afea669e007ff79c8c02b2ae8b3fc908c51b6a86c49f9cef1a9cd3f5ce9635ba";
    private const string SharedRtmExe = "10acd85af3767634450a793f2af1e057de888621cbfc578d35d3fa91abc09661";

    public static TheoryData<string, string[]> Copies => new()
    {
        {
            "full", [FullDigitExe, FullMainExe, FullDigit0, FullDigit1, FullDigit2, FullDigit3, FullDigit4,
                     SharedDigitX, SharedDpmi, SharedRtmExe]
        },
        {
            "full", [FullDigitExe, FullMainExe, FullDigit0, FullDigit1, DisplacedSpanDigit2, FullDigit3,
                     FullDigit4, SharedDigitX, SharedDpmi, SharedRtmExe]
        },
        {
            "full", [FullDigitExe, FullMainExe, FullDigit0, LostSectorsDigit1, FullDigit2, FullDigit3,
                     FullDigit4, SharedDigitX, SharedDpmi, SharedRtmExe]
        },
        {
            "manaccom", [FullDigitExe, ManaccomMainExe, ManaccomDigit0, FullDigit1, FullDigit2, FullDigit3,
                         FullDigit4, SharedDigitX, SharedDpmi, SharedRtmExe]
        },
        {
            "shareware", [SharewareDigitExe, SharewareMainExe, SharewareDigit0, SharewareDigit1, Absent,
                          Absent, Absent, SharedDigitX, SharedDpmi, SharedRtmExe]
        },
        {
            "shareware", [SharewareDigitExe, SharewareMainExe, DuplicatedSectorsDigit0, SharewareDigit1,
                          Absent, Absent, Absent, SharedDigitX, SharedDpmi, SharedRtmExe]
        },
    };

    /// <summary>Every copy of the game the tool knows about resolves to exactly one build.</summary>
    [Theory]
    [MemberData(nameof(Copies))]
    public void EveryKnownCopyNamesExactlyOneBuild(string expectedBuildId, string[] hashes)
        => Assert.Equal(expectedBuildId, NameOf(hashes)?.Id);

    /// <summary>A copy whose files come from more than one build is named as none of them.</summary>
    [Fact]
    public void AMixtureOfTwoBuildsNamesNoBuild()
    {
        string[] mixed =
        [
            FullDigitExe, ManaccomMainExe, FullDigit0, FullDigit1, FullDigit2, FullDigit3, FullDigit4,
            SharedDigitX, SharedDpmi, SharedRtmExe,
        ];

        Assert.Null(NameOf(mixed));
    }

    /// <summary>The Manaccom edition is named by the two files it does not share with the full release.</summary>
    [Fact]
    public void TheManaccomEditionIsNamedByItsOwnExecutableAndArchive()
    {
        var catalog = ReleaseCatalog.Embedded;

        Assert.Equal(["manaccom"], catalog.Identify("MAIN.EXE", ManaccomMainExe)?.Builds);
        Assert.Equal(["manaccom"], catalog.Identify("DIGIT0.XRS", ManaccomDigit0)?.Builds);
    }

    /// <summary>The eight files the Manaccom edition shares with the full release say so, which is what lets the intersection land.</summary>
    [Fact]
    public void EveryFileSharedWithTheFullReleaseCarriesBothBuilds()
    {
        var catalog = ReleaseCatalog.Embedded;
        (string Name, string Sha256)[] shared =
        [
            ("DIGIT.EXE", FullDigitExe), ("DIGIT1.XRS", FullDigit1), ("DIGIT2.XRS", FullDigit2),
            ("DIGIT3.XRS", FullDigit3), ("DIGIT4.XRS", FullDigit4), ("DIGITX.XRS", SharedDigitX),
            ("DPMI16BI.OVL", SharedDpmi), ("RTM.EXE", SharedRtmExe),
        ];

        Assert.All(shared, file =>
        {
            var identified = catalog.Identify(file.Name, file.Sha256);
            Assert.NotNull(identified);
            Assert.Contains("full", identified.Builds);
            Assert.Contains("manaccom", identified.Builds);
        });
    }

    /// <summary>Damage in an archive the two full-release editions share leaves either of them still named.</summary>
    [Theory]
    [InlineData("DIGIT2.XRS", DisplacedSpanDigit2)]
    [InlineData("DIGIT1.XRS", LostSectorsDigit1)]
    public void DamageSharedByBothFullEditionsBelongsToBoth(string fileName, string hash)
    {
        var identified = ReleaseCatalog.Embedded.Identify(fileName, hash);

        Assert.NotNull(identified);
        Assert.Equal(["full", "manaccom"], identified.Builds);
    }

    private static KnownBuild? NameOf(string[] hashes)
    {
        var catalog = ReleaseCatalog.Embedded;
        var files = catalog.Files
            .Select((known, i) => hashes[i].Length == 0
                ? new FileScan(known.Name, Present: false, null, null)
                : new FileScan(known.Name, Present: true, hashes[i], catalog.Identify(known.Name, hashes[i])))
            .ToList();

        return new Scanner(catalog, [], []).NameTheBuild(files);
    }
}
