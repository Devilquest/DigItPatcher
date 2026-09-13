namespace DigItPatcher.Core.Fixes;

/// <summary>Makes the Setup screen's lowest sound-effects setting silence the effects instead of playing them at full volume.</summary>
internal sealed class SoundEffectsVolumeFix : BinaryFix
{
    /// <inheritdoc />
    public override string Id => "SoundEffectsVolume";

    /// <inheritdoc />
    public override string TargetFile => "DIGIT.EXE";

    /// <inheritdoc />
    public override IReadOnlyList<string> DerivedFor => ["full"];

    /// <inheritdoc />
    public override long Offset => 0x2C64;

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> OriginalBytes { get; } = new byte[] { 0x74, 0x05 };

    /// <inheritdoc />
    public override ReadOnlyMemory<byte> PatchedBytes { get; } = new byte[] { 0x90, 0x90 };
}
