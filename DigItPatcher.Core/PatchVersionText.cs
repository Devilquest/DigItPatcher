using System.Reflection;
using System.Text.RegularExpressions;
using DigItPatcher.Core.Text;

namespace DigItPatcher.Core;

/// <summary>The patch's own two-part version, hand-edited and read by both the slab and the menu line so the two
/// can never disagree about what a copy carries.</summary>
public sealed partial record PatchVersionText(string Version)
{
    private const string VersionSection = "Version";
    private static readonly string[] Sections = [VersionSection];

    private const string ResourceName = "DigItPatcher.Core.Content.Patch.txt";

    /// <summary>Reads the tool's own embedded copy of the patch's version.</summary>
    public static PatchVersionText Embedded()
    {
        using var stream = typeof(PatchVersionText).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"No '{ResourceName}' is embedded.");

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    internal static PatchVersionText Parse(string contents)
    {
        var sections = SectionedText.Read("Patch.txt", Sections, contents);
        var version = sections[VersionSection];

        if (!VersionShape().IsMatch(version))
            throw new InvalidOperationException($"'Patch.txt' has version '{version}', not the 'major.minor' shape.");

        return new PatchVersionText(version);
    }

    [GeneratedRegex(@"^\d+\.\d+$")]
    private static partial Regex VersionShape();
}
