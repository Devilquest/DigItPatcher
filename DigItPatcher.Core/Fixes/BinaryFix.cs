using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.Fixes;

/// <summary>Base implementation of <see cref="IFix"/> managing stream validation and byte patching.</summary>
internal abstract class BinaryFix : IFix
{
    /// <inheritdoc />
    public abstract string Id { get; }

    /// <inheritdoc />
    public abstract string TargetFile { get; }

    /// <inheritdoc />
    public abstract IReadOnlyList<string> DerivedFor { get; }

    /// <inheritdoc />
    public virtual FixOrigin Origin => FixOrigin.File;

    /// <inheritdoc />
    public abstract long Offset { get; }

    /// <inheritdoc />
    public abstract ReadOnlyMemory<byte> OriginalBytes { get; }

    /// <inheritdoc />
    public abstract ReadOnlyMemory<byte> PatchedBytes { get; }

    /// <summary>Determines the patch state of the specified stream at this fix's own site.</summary>
    public FixState GetState(Stream stream)
    {
        if (!TryLocate(stream, out long site)) return FixState.SiteNotFound;

        int length = OriginalBytes.Length;
        if (stream.Length < site + length)
        {
            return FixState.FileTooSmall;
        }

        stream.Seek(site, SeekOrigin.Begin);
        byte[] actual = new byte[length];
        stream.ReadExactly(actual, 0, length);

        if (actual.AsSpan().SequenceEqual(OriginalBytes.Span))
        {
            return FixState.Original;
        }
        if (actual.AsSpan().SequenceEqual(PatchedBytes.Span))
        {
            return FixState.Patched;
        }
        return FixState.Modified;
    }

    /// <summary>Resolves this fix's site to a file offset, which for a segment address depends on the build in hand.</summary>
    public bool TryLocate(Stream stream, out long site)
    {
        if (Origin == FixOrigin.File)
        {
            site = Offset;
            return true;
        }

        site = 0;
        if (!MainExeLayout.TryRead(stream, out var layout)) return false;

        site = layout.Seg3((int)Offset);
        return true;
    }

    /// <summary>Writes patched bytes to the stream.</summary>
    public void Apply(Stream stream)
    {
        FixState state = GetState(stream);
        if (state != FixState.Original)
        {
            throw new InvalidOperationException(
                $"Cannot apply patch '{Id}': expected state {FixState.Original}, but current state is {state}.");
        }

        WriteAt(stream, PatchedBytes.Span);
    }

    /// <summary>Restores original bytes to the stream.</summary>
    public void Revert(Stream stream)
    {
        FixState state = GetState(stream);
        if (state != FixState.Patched)
        {
            throw new InvalidOperationException(
                $"Cannot revert patch '{Id}': expected state {FixState.Patched}, but current state is {state}.");
        }

        WriteAt(stream, OriginalBytes.Span);
    }

    // Reached only from Apply and Revert, both of which have already had GetState locate the site.
    private void WriteAt(Stream stream, ReadOnlySpan<byte> bytes)
    {
        TryLocate(stream, out long site);
        stream.Seek(site, SeekOrigin.Begin);
        stream.Write(bytes);
    }
}
