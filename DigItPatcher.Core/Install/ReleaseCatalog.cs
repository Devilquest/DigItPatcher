using System.Reflection;
using System.Text.Json;

namespace DigItPatcher.Core.Install;

/// <summary>A build of the game the tool knows about.</summary>
public sealed record KnownBuild(string Id, string Name);

/// <summary>A file whose contents the tool recognizes, and what its being there means.</summary>
public sealed record KnownFileContents(string Sha256, IReadOnlyList<string> Builds, string? Damage)
{
    /// <summary>Whether these contents are a verified copy of the file, rather than a damaged one.</summary>
    public bool IsClean => Damage is null;
}

/// <summary>One of the game's shipped files and every set of contents recorded for it.</summary>
public sealed record KnownFile(string Name, IReadOnlyList<KnownFileContents> Known, IReadOnlyList<string> AbsentIn);

/// <summary>The builds of the game the tool knows and the fingerprints that tell them apart.</summary>
public sealed class ReleaseCatalog
{
    private readonly Dictionary<string, KnownFile> _byName;

    private ReleaseCatalog(IReadOnlyList<KnownBuild> builds, IReadOnlyList<KnownFile> files)
    {
        Builds = builds;
        Files = files;
        _byName = files.ToDictionary(file => file.Name, StringComparer.OrdinalIgnoreCase);
    }

    // Lets a test scan an install built from files it wrote itself, so no test needs a real copy of the game.
    internal static ReleaseCatalog From(IReadOnlyList<KnownBuild> builds, IReadOnlyList<KnownFile> files) => new(builds, files);

    /// <summary>The catalog read from the embedded release list, built once.</summary>
    public static ReleaseCatalog Embedded { get; } = LoadEmbedded();

    /// <summary>The builds, in the order the release list gives them.</summary>
    public IReadOnlyList<KnownBuild> Builds { get; }

    /// <summary>The game's shipped files, in the order the release list gives them.</summary>
    public IReadOnlyList<KnownFile> Files { get; }

    /// <summary>Recognizes a file by name and hash, or reports that these contents are on no list.</summary>
    public KnownFileContents? Identify(string fileName, string sha256)
        => _byName.TryGetValue(fileName, out var file)
            ? file.Known.FirstOrDefault(known => string.Equals(known.Sha256, sha256, StringComparison.OrdinalIgnoreCase))
            : null;

    /// <summary>The verified contents recorded for a file, which is what a repaired candidate has to equal.</summary>
    public IReadOnlyList<KnownFileContents> CleanContentsOf(string fileName)
        => _byName.TryGetValue(fileName, out var file) ? [.. file.Known.Where(known => known.IsClean)] : [];

    /// <summary>Whether a build ships this file at all, which is what separates a missing file from an absent one.</summary>
    public bool IsShippedBy(string fileName, string buildId)
        => _byName.TryGetValue(fileName, out var file) && !file.AbsentIn.Contains(buildId);

    private static ReleaseCatalog LoadEmbedded()
    {
        using var stream = typeof(ReleaseCatalog).Assembly
            .GetManifestResourceStream("DigItPatcher.Core.Install.Content.releases.json")
            ?? throw new InvalidOperationException("The embedded release list is missing from the build.");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var list = JsonSerializer.Deserialize<ReleaseList>(stream, options)
            ?? throw new InvalidOperationException("The embedded release list could not be read.");

        return new ReleaseCatalog(
            [.. list.Builds.Select(build => new KnownBuild(build.Id, build.Name))],
            [.. list.Files.Select(file => new KnownFile(
                file.Name,
                [.. file.Known.Select(known => new KnownFileContents(known.Sha256, known.Builds, known.Damage))],
                file.AbsentIn))]);
    }

    internal sealed class ReleaseList
    {
        public List<BuildEntry> Builds { get; init; } = [];

        public List<FileEntry> Files { get; init; } = [];
    }

    internal sealed class BuildEntry
    {
        public string Id { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;
    }

    internal sealed class FileEntry
    {
        public string Name { get; init; } = string.Empty;

        public List<ContentsEntry> Known { get; init; } = [];

        public List<string> AbsentIn { get; init; } = [];
    }

    internal sealed class ContentsEntry
    {
        public string Sha256 { get; init; } = string.Empty;

        public List<string> Builds { get; init; } = [];

        public string? Damage { get; init; }
    }
}
