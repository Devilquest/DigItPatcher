using DigItPatcher.Core.Install;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the backup folder's own rules: taken once, and never overwritten after that.</summary>
public class BackupSetTests : IDisposable
{
    private readonly string _tempDir;

    public BackupSetTests() => _tempDir = Directory.CreateTempSubdirectory("DigItPatcherTests_").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private GameInstall InstallHolding(params (string Name, byte[] Contents)[] files)
    {
        File.WriteAllBytes(Path.Combine(_tempDir, "DIGIT0.XRS"), [0]);
        foreach (var (name, contents) in files) File.WriteAllBytes(Path.Combine(_tempDir, name), contents);

        Assert.True(GameInstall.TryOpen(_tempDir, out var install));
        return install;
    }

    [Fact]
    public void DoesNotExistBesideAFreshInstall()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));

        Assert.False(BackupSet.Exists(install));
    }

    [Fact]
    public void EnsureCoversCopiesEveryNamedFile()
    {
        var install = InstallHolding(("MAIN.EXE", [1]), ("DIGIT.EXE", [2]));

        BackupSet.EnsureCovers(install, ["MAIN.EXE", "DIGIT.EXE"]);

        Assert.True(BackupSet.Exists(install));
        Assert.Equal(1, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), "MAIN.EXE")).Single());
        Assert.Equal(2, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), "DIGIT.EXE")).Single());
    }

    [Fact]
    public void EnsureCoversLeavesAnAlreadyBackedUpFileUntouched()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        File.WriteAllBytes(install.PathOf("MAIN.EXE"), [9]);

        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);

        Assert.Equal(1, File.ReadAllBytes(Path.Combine(BackupSet.FolderPath(install), "MAIN.EXE")).Single());
    }

    [Fact]
    public void RestoreCopiesEveryBackedUpFileOverTheInstall()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        File.WriteAllBytes(install.PathOf("MAIN.EXE"), [9]);

        Assert.True(BackupSet.TryRestore(install, out var failureReason));

        Assert.Null(failureReason);
        Assert.Equal(1, File.ReadAllBytes(install.PathOf("MAIN.EXE")).Single());
    }

    [Fact]
    public void RestoreDoesNotWriteTheManifestBackAsAGameFile()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        BackupSet.AppendManifestEntry(install, "note");

        BackupSet.TryRestore(install, out _);

        Assert.False(install.Has(BackupSet.ManifestFileName));
    }

    [Fact]
    public void AnUnwritableTargetFailsBeforeAnythingIsRestored()
    {
        var install = InstallHolding(("MAIN.EXE", [1]));
        BackupSet.EnsureCovers(install, ["MAIN.EXE"]);
        File.WriteAllBytes(install.PathOf("MAIN.EXE"), [9]);

        bool restored;
        string? failureReason;
        using (File.Open(install.PathOf("MAIN.EXE"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            restored = BackupSet.TryRestore(install, out failureReason);
        }

        Assert.False(restored);
        Assert.NotNull(failureReason);
        Assert.Equal(9, File.ReadAllBytes(install.PathOf("MAIN.EXE")).Single());
    }
}
