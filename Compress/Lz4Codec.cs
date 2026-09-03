using System;
using System.Buffers.Binary;
using K4os.Compression.LZ4;

namespace SharpUtils.Compress;

/// <summary>
/// Compresión y descompresión LZ4 ultra rápida y sin pérdida.
/// </summary>
public static class Lz4Codec
{
    private const int HeaderSize = 4;

    /// <summary>
    /// Comprime datos usando LZ4.
    /// Los primeros 4 bytes contienen el tamaño original.
    /// </summary>
    public static byte[] Compress(ReadOnlySpan<byte> data, LZ4Level level = LZ4Level.L00_FAST)
    {
        int maxSize = LZ4Codec.MaximumOutputSize(data.Length);

        byte[] output = new byte[HeaderSize + maxSize];

        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, HeaderSize), data.Length);
        int written = LZ4Codec.Encode(data, output.AsSpan(HeaderSize), level);

        Array.Resize(ref output, HeaderSize + written);
        return output;
    }

    /// <summary>
    /// Descomprime datos LZ4.
    /// </summary>
    public static byte[] Decompress(ReadOnlySpan<byte> compressedData)
    {
        int originalSize = BinaryPrimitives.ReadInt32LittleEndian(compressedData[..HeaderSize]);
        byte[] output = new byte[originalSize];

        LZ4Codec.Decode(compressedData[HeaderSize..], output);
        return output;
    }
}
