using System.Reflection;
using DigItPatcher.Core.Text;

namespace DigItPatcher.Core.MenuLine;

/// <summary>The main menu's own line, read from its embedded template before the patch's version and any active
/// mods fill it in.</summary>
public sealed record MenuLineText(string Line)
{
    private const string LineSection = "Line";
    private static readonly string[] Sections = [LineSection];

    private const string ResourceName = "DigItPatcher.Core.MenuLine.Content.MenuLine.txt";
    private const string VersionPlaceholder = "{version}";
    private const string ModSeparator = " + ";

    /// <summary>Reads the tool's own embedded copy of the menu line's wording.</summary>
    public static MenuLineText Embedded()
    {
        using var stream = typeof(MenuLineText).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"No '{ResourceName}' is embedded.");

        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    internal static MenuLineText Parse(string contents)
    {
        var sections = SectionedText.Read("MenuLine.txt", Sections, contents);
        return new MenuLineText(sections[LineSection]);
    }

    /// <summary>Fills <c>{version}</c> with the patch's version and appends each active mod's name after it,
    /// each introduced by a <c>" + "</c>, in the order given.</summary>
    public string Compose(string patchVersion, IReadOnlyList<string> modNames)
    {
        var line = Line.Replace(VersionPlaceholder, patchVersion);
        return modNames.Count == 0 ? line : line + ModSeparator + string.Join(ModSeparator, modNames);
    }
}
