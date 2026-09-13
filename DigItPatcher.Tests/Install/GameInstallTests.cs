using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests covering how an install resolves its own files and reads them.</summary>
public class GameInstallTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GameInstall _install;

    public GameInstallTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;
        File.WriteAllText(Path.Combine(_tempDir, "DIGIT0.XRS"), "archive");
        Assert.True(GameInstall.TryOpen(_tempDir, out _install));
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void TryOpen_ResolvesTheFolderTheSameWayTheWindowDoes()
    {
        var parent = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;
        var nested = Directory.CreateDirectory(Path.Combine(parent, GameFolder.NestedFolderName)).FullName;
        File.WriteAllText(Path.Combine(nested, "DIGIT0.XRS"), "archive");

        try
        {
            Assert.True(GameInstall.TryOpen(parent, out var install));
            Assert.Equal(nested, install.Folder);
        }
        finally
        {
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void TryOpen_RefusesAFolderHoldingNoGame()
        => Assert.False(GameInstall.TryOpen(Path.GetTempPath(), out _));

    [Fact]
    public void Has_AnswersForAFileThatIsThereAndOneThatIsNot()
    {
        Assert.True(_install.Has("DIGIT0.XRS"));
        Assert.False(_install.Has("DIGIT4.XRS"));
    }

    [Fact]
    public void TryHash_ProducesTheLowercaseSha256OfTheFile()
    {
        Assert.True(_install.TryHash("DIGIT0.XRS", out var hash));

        // SHA-256 of "archive", the fixture's contents, lowercase because that is how the release list writes them.
        Assert.Equal("0eb3e36bfb24dcd9bb1d1bece1531216b59539a8fde17ee80224af0653c92aa3", hash);
    }

    [Fact]
    public void TryHash_ReportsAFileThatIsNotThereRatherThanThrowing()
        => Assert.False(_install.TryHash("DIGIT4.XRS", out _));

    [Fact]
    public void CanWriteTo_IsFalseForAFileThatIsNotThere()
        => Assert.False(_install.CanWriteTo("DIGIT4.XRS"));

    [Fact]
    public void CanWriteTo_IsFalseWhileTheFileIsHeldExclusively()
    {
        using var held = File.Open(_install.PathOf("DIGIT0.XRS"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.False(_install.CanWriteTo("DIGIT0.XRS"));
    }
}
