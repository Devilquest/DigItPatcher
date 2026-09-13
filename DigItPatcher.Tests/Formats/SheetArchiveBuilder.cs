using System.Buffers.Binary;
using DigItPatcher.Core.Formats;

namespace DigItPatcher.Tests;

/// <summary>Builds archives and image entries the tests own outright, so no test needs a copy of the game.</summary>
internal static class SheetArchiveBuilder
{
    private const int HeaderSize = 772;

    // The longest run one fill opcode can carry, which is what keeps a whole frame down to a few dozen bytes.
    private const int LongestFill = 0x2000;

    /// <summary>An image entry of <paramref name="frameCount"/> frames, each decoding to one full image.</summary>
    internal static byte[] Sheet(int frameCount, byte fill = 7)
    {
        var frames = Enumerable.Range(0, frameCount).Select(index => Frame((byte)(fill + index))).ToList();

        var sheet = new byte[HeaderSize + frames.Sum(frame => 2 + frame.Length)];
        BinaryPrimitives.WriteUInt16LittleEndian(sheet, (ushort)(frameCount - 1));
        BinaryPrimitives.WriteUInt16LittleEndian(sheet.AsSpan(2), (ushort)(HeaderSize + 2 + frames[0].Length));

        int at = HeaderSize;
        foreach (var frame in frames)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(sheet.AsSpan(at), (ushort)frame.Length);
            frame.CopyTo(sheet.AsSpan(at + 2));
            at += 2 + frame.Length;
        }

        return sheet;
    }

    /// <summary>An archive holding the named entries back to back, in directory order.</summary>
    internal static byte[] Archive(params (string Name, byte[] Contents)[] entries)
    {
        int dataStart = 2 + (entries.Length * 32);
        var archive = new byte[dataStart + entries.Sum(entry => entry.Contents.Length)];
        BinaryPrimitives.WriteUInt16LittleEndian(archive, (ushort)entries.Length);

        int offset = 0;
        for (int i = 0; i < entries.Length; i++)
        {
            var (name, contents) = entries[i];
            var record = archive.AsSpan(2 + (i * 32), 32);
            record[0] = (byte)name.Length;
            for (int c = 0; c < name.Length; c++) record[1 + c] = (byte)name[c];
            BinaryPrimitives.WriteUInt32LittleEndian(record[13..], (uint)offset);
            BinaryPrimitives.WriteUInt32LittleEndian(record[17..], (uint)contents.Length);

            contents.CopyTo(archive.AsSpan(dataStart + offset));
            offset += contents.Length;
        }

        return archive;
    }

    /// <summary>The same bytes with one more in them, which is half of what a displaced span is.</summary>
    internal static byte[] WithByteInserted(byte[] bytes, int at, byte value)
    {
        var result = new byte[bytes.Length + 1];
        bytes.AsSpan(0, at).CopyTo(result);
        result[at] = value;
        bytes.AsSpan(at).CopyTo(result.AsSpan(at + 1));
        return result;
    }

    /// <summary>The same bytes with one of them gone, which is the other half.</summary>
    internal static byte[] WithByteRemoved(byte[] bytes, int at)
    {
        var result = new byte[bytes.Length - 1];
        bytes.AsSpan(0, at).CopyTo(result);
        bytes.AsSpan(at + 1).CopyTo(result.AsSpan(at));
        return result;
    }

    // A run of long fills is the shortest stream that decodes to a whole image, and it uses one opcode of the eight.
    private static byte[] Frame(byte value)
    {
        var stream = new List<byte>();
        for (int written = 0; written < FrameCodec.FrameBytes;)
        {
            int run = Math.Min(LongestFill, FrameCodec.FrameBytes - written);
            stream.Add((byte)(0x80 | ((run - 1) >> 8)));
            stream.Add((byte)((run - 1) & 0xFF));
            stream.Add(value);
            written += run;
        }

        return [.. stream];
    }
}
