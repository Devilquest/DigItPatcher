namespace DigItPatcher.Core.Fixes;

/// <summary>The fixes the tool offers, in the order the window lists them.</summary>
public static class FixCatalog
{
    /// <summary>Every registered fix.</summary>
    public static IReadOnlyList<IFix> All { get; } = [new CompletionPercentageFix(), new SoundEffectsVolumeFix()];
}
