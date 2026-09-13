using System.Reflection;
using DigItPatcher.Core.Text;

namespace DigItPatcher.Core.Slab;

/// <summary>The slab's own words, read from the embedded content file before the identity fills the placeholders.</summary>
internal sealed record SlabWordingText(string Heading, string Credit, string Author)
{
    private const string HeadingSection = "Heading";
    private const string CreditSection = "Credit";
    private const string AuthorSection = "Author";
    private static readonly string[] Sections = [HeadingSection, CreditSection, AuthorSection];

    private const string ResourceName = "DigItPatcher.Core.Slab.Content.Slab.txt";

    /// <summary>Reads the tool's own embedded copy of the slab's wording.</summary>
    public static SlabWordingText Embedded()
    {
        using var stream = typeof(SlabWordingText).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"No '{ResourceName}' is embedded.");

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    internal static SlabWordingText Parse(string contents)
    {
        var sections = SectionedText.Read("Slab.txt", Sections, contents);
        return new SlabWordingText(sections[HeadingSection], sections[CreditSection], sections[AuthorSection]);
    }

    /// <summary>Fills <c>{version}</c> with the patch's own version and <c>{author}</c> with the tool's identity.</summary>
    public SlabWording Resolve(AppIdentity identity, string patchVersion) =>
        new(Substitute(Heading, identity, patchVersion), Substitute(Credit, identity, patchVersion),
            Substitute(Author, identity, patchVersion));

    private static string Substitute(string text, AppIdentity identity, string patchVersion) => text
        .Replace("{version}", patchVersion)
        .Replace("{author}", identity.Author);
}
