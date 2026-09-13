namespace DigItPatcher.Core.Install;

/// <summary>A copy of the game on disk: the only thing that turns a game-relative name into a stream.</summary>
public sealed class GameInstall
{
    private GameInstall(string folder) => Folder = folder;

    /// <summary>The folder holding the game's files.</summary>
    public string Folder { get; }

    /// <summary>Opens the game folder at a candidate path, or reports that it holds no game.</summary>
    public static bool TryOpen(string? candidate, out GameInstall install)
    {
        install = null!;
        if (!GameFolder.TryResolve(candidate, out var folder)) return false;

        install = new GameInstall(folder);
        return true;
    }

    /// <summary>The full path of one of the game's files, whether or not it is there.</summary>
    public string PathOf(string fileName) => Path.Combine(Folder, fileName);

    /// <summary>Whether the game folder holds this file.</summary>
    public bool Has(string fileName) => File.Exists(PathOf(fileName));

    /// <summary>Opens one of the game's files for reading.</summary>
    public Stream OpenRead(string fileName) => File.OpenRead(PathOf(fileName));

    /// <summary>Opens one of the game's files for reading and writing in place.</summary>
    public Stream OpenReadWrite(string fileName) => File.Open(PathOf(fileName), FileMode.Open, FileAccess.ReadWrite);

    /// <summary>The archives the game holds, which are the files whose contents check themselves.</summary>
    public IReadOnlyList<string> ArchiveNames =>
        [.. Directory.GetFiles(Folder, "*.XRS").Select(Path.GetFileName).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)];

    /// <summary>Reads one of the game's files whole, which is what a repair weighs a candidate against.</summary>
    public byte[] ReadAll(string fileName) => File.ReadAllBytes(PathOf(fileName));

    /// <summary>Replaces one of the game's files with bytes already proved correct.</summary>
    public void WriteAll(string fileName, byte[] contents) => File.WriteAllBytes(PathOf(fileName), contents);

    /// <summary>Hashes one of the game's files, or reports that it could not be read.</summary>
    public bool TryHash(string fileName, out string sha256) => Hashing.TryHash(PathOf(fileName), out sha256);

    /// <summary>Whether a file can be opened for writing, asked before a run rather than during one.</summary>
    public bool CanWriteTo(string fileName)
    {
        try
        {
            using var stream = File.Open(PathOf(fileName), FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
