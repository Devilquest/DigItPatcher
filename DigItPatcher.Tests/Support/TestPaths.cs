using System.Text.Json;
using DigItPatcher.Core.Formats;
using DigItPatcher.Core.Install;
using DigItPatcher.Core.Patching;

namespace DigItPatcher.Tests;

/// <summary>Locates the user's copy of the game (<c>DIGIT/</c>) for tests that need it, searching no further than this repository.</summary>
internal static class TestPaths
{
    private const string ConfigFileName = "digit.tests.local.json";
    private const string SolutionFileName = "DigItPatcher.slnx";

    /// <summary>Whether a copy of the game can be located, without failing under <c>DIGIT_TESTS_STRICT</c>.</summary>
    internal static bool HasGameDir() => TryGetGameDir(out _, reportIfStrict: false);

    /// <summary>The reason a game-dependent test is skipped, or <c>null</c> when it has to run.</summary>
    internal static string? GameMissingSkipReason() => SkipReason(HasGameDir(), IsStrict);

    /// <summary>Under <c>DIGIT_TESTS_STRICT</c> a missing copy of the game is a failure, so the test runs and fails rather than reporting itself skipped.</summary>
    internal static string? SkipReason(bool gameFound, bool strict)
        => gameFound || strict ? null : "no game folder found; set DIGIT_GAME_DIR, add digit.tests.local.json, or place DIGIT/ inside the repository";

    /// <summary>The builds every install-backed test here was derived against.</summary>
    internal static readonly string[] SupportedBuilds = ["full", "manaccom"];

    /// <summary>The reason a test needing <paramref name="needs"/> is skipped, or <c>null</c> when it has to run.</summary>
    internal static string? SkipReasonFor(GameNeeds needs)
    {
        if (IsStrict) return null;
        if (SkipReason(HasGameDir(), strict: false) is { } missing) return missing;

        if (!needs.HasFlag(GameNeeds.SupportedBuild)) return null;

        return UnsupportedBuildSkipReason(InstalledBuild.Value) ?? UnreadableExecutableSkipReason(ExecutableReads.Value);
    }

    /// <summary>The reason a <c>MAIN.EXE</c> cannot be read from, or <c>null</c> when it can.</summary>
    internal static string? UnreadableExecutableSkipReason(bool readsAsAnExecutable)
        => readsAsAnExecutable ? null : "MAIN.EXE in this copy of the game is missing or does not read as an executable";

    /// <summary>The reason a build cannot stand in for one this tool was derived against, or <c>null</c> when it can.</summary>
    internal static string? UnsupportedBuildSkipReason(KnownBuild? build)
    {
        if (build is null) return "the game folder found is not a build this tool recognizes";

        return SupportedBuilds.Contains(build.Id, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"the game folder found is the {build.Name}, which is not a build this tool was derived against";
    }

    // Hashing every shipped file costs more than a test attribute should, and the answer cannot change
    // mid-run, so the whole discovery pass shares one naming.
    private static readonly Lazy<KnownBuild?> InstalledBuild = new(NameInstalledBuild);

    // The archives alone name a build, so a copy missing or truncating MAIN.EXE is named like a whole one.
    private static readonly Lazy<bool> ExecutableReads = new(ReadsTheExecutable);

    private static bool ReadsTheExecutable()
    {
        if (!TryGetGameDir(out var gameDir, reportIfStrict: false)) return false;
        if (!GameInstall.TryOpen(gameDir, out var install) || !install.Has("MAIN.EXE")) return false;

        using var stream = install.OpenRead("MAIN.EXE");
        if (!MainExeLayout.TryRead(stream, out var layout)) return false;

        // Reading the table is not proof the file holds what it points at: it sits in the first few hundred
        // bytes, and the data segment is the furthest offset these tests reach.
        return stream.Length > layout.DGroup(0);
    }

    private static KnownBuild? NameInstalledBuild()
    {
        if (!TryGetGameDir(out var gameDir, reportIfStrict: false)) return null;
        if (!GameInstall.TryOpen(gameDir, out var install)) return null;

        var scanner = new Scanner(ReleaseCatalog.Embedded, [], []);
        return scanner.NameTheBuild(scanner.ScanFiles(install));
    }

    /// <summary>Resolves the game directory from <c>DIGIT_GAME_DIR</c>, then the local config file, then a <c>DIGIT/</c> folder in the search scope.</summary>
    public static bool TryGetGameDir(out string gameDir) => TryGetGameDir(out gameDir, reportIfStrict: true);

    private static bool TryGetGameDir(out string gameDir, bool reportIfStrict)
    {
        var envDir = Environment.GetEnvironmentVariable("DIGIT_GAME_DIR");
        if (!string.IsNullOrEmpty(envDir) && IsGameDir(envDir))
        {
            gameDir = envDir;
            return true;
        }

        if (TryGetConfiguredGameDir(out var configuredDir) && IsGameDir(configuredDir))
        {
            gameDir = configuredDir;
            return true;
        }

        foreach (var dir in SearchScope())
        {
            var candidate = Path.Combine(dir.FullName, "DIGIT");
            if (IsGameDir(candidate))
            {
                gameDir = candidate;
                return true;
            }
        }

        gameDir = "";
        if (reportIfStrict) FailIfStrict("no game install found (checked DIGIT_GAME_DIR, " + ConfigFileName + ", and DIGIT/ inside the repository)");
        return false;
    }

    /// <summary>The test binary's directory and its parents up to the repository root, or none when the binary sits outside the repository.</summary>
    internal static IReadOnlyList<DirectoryInfo> SearchScope()
    {
        // Bounded at the repository root so a clone never reads the folders above it.
        var scope = new List<DirectoryInfo>();
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            scope.Add(dir);
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return scope;
        }
        return [];
    }

    private static bool IsGameDir(string candidate)
        => Directory.Exists(candidate) && Directory.EnumerateFiles(candidate, "*.XRS").Any();

    /// <summary>Reads <c>gameDir</c> out of a <c>digit.tests.local.json</c> in the search scope, resolving a relative value against that file's own folder.</summary>
    private static bool TryGetConfiguredGameDir(out string gameDir)
    {
        foreach (var dir in SearchScope())
        {
            var candidate = Path.Combine(dir.FullName, ConfigFileName);
            if (!File.Exists(candidate))
                continue;

            // This file is hand-edited from the .example, so malformed JSON is a realistic mistake:
            // fall through to the remaining resolution steps instead of throwing an opaque
            // JsonException out of every game-dependent test.
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(File.ReadAllText(candidate));
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("gameDir", out var value)
                    && value.ValueKind == JsonValueKind.String)
                {
                    var configured = value.GetString() ?? "";
                    gameDir = configured.Length == 0 ? "" : Path.GetFullPath(Path.Combine(dir.FullName, configured));
                    return gameDir.Length != 0;
                }
            }
        }

        gameDir = "";
        return false;
    }

    private static bool IsStrict => Environment.GetEnvironmentVariable("DIGIT_TESTS_STRICT") == "1";

    private static void FailIfStrict(string reason)
    {
        if (IsStrict)
            throw new InvalidOperationException($"DIGIT_TESTS_STRICT=1 is set but {reason}. Fix the path or unset DIGIT_TESTS_STRICT to allow skipping.");
    }
}
