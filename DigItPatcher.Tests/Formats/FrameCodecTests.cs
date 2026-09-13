using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Tests covering the encoder against the decoder it has to invert.</summary>
public class FrameCodecTests
{
    private static byte[] DecodeBack(byte[] compressed)
    {
        var working = new byte[FrameCodec.WorkingSize];
        var decode = FrameCodec.Decompress(compressed, working);
        Assert.True(decode.Ok);
        Assert.Equal(FrameCodec.FrameBytes, decode.Produced);
        return working[..FrameCodec.FrameBytes];
    }

    [Fact]
    public void ARunLongerThanAShortFillRoundTrips()
    {
        var plane = new byte[FrameCodec.FrameBytes];
        Array.Fill(plane, (byte)42);

        Assert.Equal(plane, DecodeBack(FrameCodec.Compress(plane)));
    }

    [Fact]
    public void ARunShortEnoughForTheShortFillRoundTrips()
    {
        var plane = new byte[FrameCodec.FrameBytes];
        for (int i = 0; i < plane.Length; i++) plane[i] = (byte)((i / 10) % 2 == 0 ? 3 : 9);

        Assert.Equal(plane, DecodeBack(FrameCodec.Compress(plane)));
    }

    [Fact]
    public void ATailWithNoRunWorthAFillRoundTripsAsLiterals()
    {
        var plane = new byte[FrameCodec.FrameBytes];
        Array.Fill(plane, (byte)42, 0, 60000);
        for (int i = 60000; i < plane.Length; i++) plane[i] = (byte)(i % 251); // no two neighbors ever equal

        Assert.Equal(plane, DecodeBack(FrameCodec.Compress(plane)));
    }

    [Fact]
    public void APlaneWithNoRunAnywhereOverflowsTheSizeLimitInsteadOfTruncating()
    {
        var plane = new byte[FrameCodec.FrameBytes];
        for (int i = 0; i < plane.Length; i++) plane[i] = (byte)(i % 251); // literals alone cost more than the u16 field holds

        Assert.Throws<InvalidOperationException>(() => FrameCodec.Compress(plane));
    }

    [Fact]
    public void ACompressedFrameIsIntactOnceDecoded()
    {
        var plane = new byte[FrameCodec.FrameBytes];
        Array.Fill(plane, (byte)5);

        Assert.True(FrameCodec.IsIntact(FrameCodec.Compress(plane), new byte[FrameCodec.WorkingSize]));
    }

    [Fact]
    public void AWrongSizedPlaneIsRefused()
        => Assert.Throws<ArgumentException>(() => FrameCodec.Compress(new byte[FrameCodec.FrameBytes - 1]));
}
