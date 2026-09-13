namespace DigItPatcher.Core.Formats;

/// <summary>The 256-color palette one of the game's screens carries.</summary>
public static class GamePalette
{
    /// <summary>Colors a palette entry holds.</summary>
    public const int ColorCount = 256;

    /// <summary>Reads 256 RGB triples from a palette entry's raw bytes, widening each 6-bit channel by replicating its high bits into the low ones.</summary>
    public static (byte R, byte G, byte B)[] Read(ReadOnlySpan<byte> blob)
    {
        if (blob.Length != ColorCount * 3) throw new ArgumentException($"expected {ColorCount * 3} bytes, got {blob.Length}", nameof(blob));

        var palette = new (byte R, byte G, byte B)[ColorCount];
        for (int i = 0; i < ColorCount; i++)
            palette[i] = (Widen(blob[i * 3]), Widen(blob[(i * 3) + 1]), Widen(blob[(i * 3) + 2]));

        return palette;
    }

    private static byte Widen(byte sixBit) => (byte)((sixBit << 2) | (sixBit >> 4));
}
