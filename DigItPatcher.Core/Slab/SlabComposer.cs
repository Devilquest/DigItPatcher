using DigItPatcher.Core.Fixes;
using DigItPatcher.Core.Install;

namespace DigItPatcher.Core.Slab;

/// <summary>The two files a slab composition rewrites, ready to be written over an install.</summary>
public sealed record SlabComposition(byte[] Archive, byte[] MainExe);

/// <summary>Why a composition was not attempted, naming the piece the guard could not verify.</summary>
public sealed record SlabRefusal(string Reason);

/// <summary>The one thing a run calls: verifies the pieces, derives the blank, paints the notes, writes neither
/// file itself.</summary>
public static class SlabComposer
{
    private const int TargetSlab = 3;

    /// <summary>Composes the credits screen's third slab out of <paramref name="fixes"/>' own words, or refuses.</summary>
    public static bool TryCompose(GameInstall install, AppIdentity identity, IReadOnlyList<IFix> fixes,
        out SlabComposition? composition, out SlabRefusal? refusal)
    {
        composition = null;

        if (!SlabSources.Verify(install, out var sources, out var reason))
        {
            refusal = new SlabRefusal(reason!);
            return false;
        }

        var blank = SlabBlank.Derive(sources!.Art, sources.Donor, sources.Palette);
        var wording = SlabWordingText.Embedded().Resolve(identity, PatchVersionText.Embedded().Version);
        var lines = fixes.Select(fix => FixText.For(fix.Id).SlabLine).ToList();

        try
        {
            SlabNotes.Paint(blank, sources.Font, TargetSlab, wording, lines);
        }
        catch (InvalidOperationException ex)
        {
            // A line too wide for the stone is only measurable once it is laid out, one step past the guard.
            refusal = new SlabRefusal(ex.Message);
            return false;
        }

        var (archive, mainExe) = SlabWriter.Build(sources, blank);
        composition = new SlabComposition(archive, mainExe);
        refusal = null;
        return true;
    }
}
