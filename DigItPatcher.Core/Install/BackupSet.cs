namespace DigItPatcher.Core.Install;

/// <summary>The backup folder beside an install: taken whole before the first write, and never overwritten.</summary>
public static class BackupSet
{
    /// <summary>Folder name for the backup, created beside the game's own files.</summary>
    public const string FolderName = "DigItPatcher_Backup";

    /// <summary>File name of the plain-text record of what the backup holds and what was done.</summary>
    public const string ManifestFileName = "backup.txt";

    /// <summary>Path to the backup folder beside <paramref name="install"/>.</summary>
    public static string FolderPath(GameInstall install) => Path.Combine(install.Folder, FolderName);

    /// <summary>Whether a backup folder already exists beside <paramref name="install"/>.</summary>
    public static bool Exists(GameInstall install) => Directory.Exists(FolderPath(install));

    /// <summary>Copies every named file into the backup folder, leaving any file already covered untouched.</summary>
    public static void EnsureCovers(GameInstall install, IReadOnlyList<string> files)
    {
        var folder = FolderPath(install);
        Directory.CreateDirectory(folder);

        foreach (var file in files)
        {
            var backupPath = Path.Combine(folder, file);
            if (File.Exists(backupPath)) continue;

            File.Copy(install.PathOf(file), backupPath);
        }
    }

    /// <summary>Appends one plain-text record to the backup's manifest, creating it on the first call.</summary>
    public static void AppendManifestEntry(GameInstall install, string entry)
        => File.AppendAllText(Path.Combine(FolderPath(install), ManifestFileName), entry + Environment.NewLine + Environment.NewLine);

    /// <summary>The file names the backup holds, which is what it promises to restore, excluding its own manifest.</summary>
    public static IReadOnlyList<string> CoveredFiles(GameInstall install)
    {
        var folder = FolderPath(install);
        if (!Directory.Exists(folder)) return [];

        return [.. Directory.GetFiles(folder)
            .Select(Path.GetFileName)
            .Where(name => name is not null && name != ManifestFileName)
            .Cast<string>()];
    }

    /// <summary>Hashes one of the files the backup holds, or reports that it could not be read.</summary>
    public static bool TryHash(GameInstall install, string fileName, out string sha256)
        => Hashing.TryHash(Path.Combine(FolderPath(install), fileName), out sha256);

    /// <summary>Copies every backed-up file back over the install, whole, unless one of them cannot be written.</summary>
    public static bool TryRestore(GameInstall install, out string? failureReason)
    {
        var names = CoveredFiles(install);

        var unwritable = names.Where(name => install.Has(name) && !install.CanWriteTo(name)).ToList();
        if (unwritable.Count > 0)
        {
            failureReason = $"{string.Join(", ", unwritable)} could not be opened for writing.";
            return false;
        }

        foreach (var name in names) File.Copy(Path.Combine(FolderPath(install), name), install.PathOf(name), overwrite: true);

        failureReason = null;
        return true;
    }
}
