using System.Reflection;
using DigItPatcher.Core.Text;

namespace DigItPatcher.Core.Fixes;

/// <summary>Every word the tool says about one fix, read from that fix's own embedded text file.</summary>
public sealed record FixText(string Name, string WhatIsWrong, string WhyItHappens, string WhatChanges, string SlabLine)
{
    private const string NameSection = "Name";
    private const string WhatIsWrongSection = "What Is Wrong";
    private const string WhyItHappensSection = "Why It Happens";
    private const string WhatChangesSection = "What Changes";
    private const string SlabLineSection = "Slab Line";

    private static readonly string[] Sections =
        [NameSection, WhatIsWrongSection, WhyItHappensSection, WhatChangesSection, SlabLineSection];

    /// <summary>Reads the text file embedded for a fix, named after the fix's id.</summary>
    public static FixText For(string fixId)
    {
        using var stream = typeof(FixText).Assembly.GetManifestResourceStream(ResourceNameFor(fixId))
            ?? throw new InvalidOperationException($"No text file is embedded for the fix '{fixId}'.");

        using var reader = new StreamReader(stream);
        return Parse(fixId, reader.ReadToEnd());
    }

    internal static string ResourceNameFor(string fixId) => $"DigItPatcher.Core.Fixes.Content.{fixId}.txt";

    internal static FixText Parse(string fixId, string contents)
    {
        var sections = SectionedText.Read($"the fix '{fixId}'", Sections, contents);

        return new FixText(
            sections[NameSection],
            sections[WhatIsWrongSection],
            sections[WhyItHappensSection],
            sections[WhatChangesSection],
            sections[SlabLineSection]);
    }
}
