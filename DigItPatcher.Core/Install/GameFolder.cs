namespace DigItPatcher.Core.Install;

/// <summary>Decides whether a folder holds a copy of the game, and which folder that is.</summary>
public static class GameFolder
{
    /// <summary>The asset archives the game ships, any one of which marks a folder as an install.</summary>
    public static readonly string[] ArchiveNames =
        ["DIGIT0.XRS", "DIGIT1.XRS", "DIGIT2.XRS", "DIGIT3.XRS", "DIGIT4.XRS", "DIGITX.XRS"];

    /// <summary>The folder an install is nested in when the candidate is its parent.</summary>
    public const string NestedFolderName = "DIGIT";

    /// <summary>Resolves a candidate to an install: the folder holding the archives, or a parent of one that does.</summary>
    public static bool TryResolve(string? candidate, out string gameFolder)
    {
        gameFolder = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate) || !Path.IsPathFullyQualified(candidate)) return false;

        try
        {
            if (HoldsArchives(candidate))
            {
                gameFolder = candidate;
                return true;
            }

            var nested = Path.Combine(candidate, NestedFolderName);
            if (HoldsArchives(nested))
            {
                gameFolder = nested;
                return true;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            gameFolder = string.Empty;
        }

        return false;
    }

    // One archive is enough: this answers whether a game is here, never which release it is or whether it can be patched.
    private static bool HoldsArchives(string folder)
        => Directory.Exists(folder) && ArchiveNames.Any(name => File.Exists(Path.Combine(folder, name)));
}
