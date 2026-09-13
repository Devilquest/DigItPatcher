using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests covering which candidate paths resolve to an install and which do not.</summary>
public class GameFolderTests : IDisposable
{
    private readonly string _tempDir;

    public GameFolderTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string MakeInstall(string folder, string archiveName = "DIGIT0.XRS")
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, archiveName), []);
        return folder;
    }

    [Fact]
    public void TryResolve_AcceptsTheFolderHoldingTheArchives()
    {
        MakeInstall(_tempDir);

        Assert.True(GameFolder.TryResolve(_tempDir, out var resolved));
        Assert.Equal(_tempDir, resolved);
    }

    [Fact]
    public void TryResolve_AcceptsAParentAndResolvesToTheNestedFolder()
    {
        var nested = MakeInstall(Path.Combine(_tempDir, GameFolder.NestedFolderName));

        Assert.True(GameFolder.TryResolve(_tempDir, out var resolved));
        Assert.Equal(nested, resolved);
    }

    [Fact]
    public void TryResolve_LooksOneLevelDownAndNoFurther()
    {
        MakeInstall(Path.Combine(_tempDir, "Games", GameFolder.NestedFolderName));

        Assert.False(GameFolder.TryResolve(_tempDir, out _));
    }

    [Theory]
    [InlineData("DIGIT0.XRS")]
    [InlineData("DIGITX.XRS")]
    [InlineData("DIGIT4.XRS")]
    public void TryResolve_AcceptsAnySingleArchive(string archiveName)
    {
        var folder = MakeInstall(Path.Combine(_tempDir, archiveName[..6]), archiveName);

        Assert.True(GameFolder.TryResolve(folder, out _));
    }

    [Fact]
    public void TryResolve_RejectsAFolderHoldingNoArchive()
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "MAIN.EXE"), []);

        Assert.False(GameFolder.TryResolve(_tempDir, out var resolved));
        Assert.Equal(string.Empty, resolved);
    }

    [Fact]
    public void TryResolve_RejectsAFolderThatIsNotThere()
        => Assert.False(GameFolder.TryResolve(Path.Combine(_tempDir, "Absent"), out _));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DIGIT")]
    public void TryResolve_RejectsAnythingThatIsNotAnAbsolutePath(string? candidate)
        => Assert.False(GameFolder.TryResolve(candidate, out _));
}
