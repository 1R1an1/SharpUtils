using System;
using K4os.Compression.LZ4;

namespace SharpUtils.Compress;

/// <summary>
/// Compresión y descompresión LZ4 ultra rápida y sin pérdida.
/// Ideal para streaming en tiempo real y archivos genéricos.
/// </summary>
public static class Lz4Codec
{
    /// <summary>
    /// Comprime un array de bytes.
    /// </summary>
    public static byte[] Compress(byte[] data)
    {
        // El tamaño máximo comprimido de LZ4 es original + 4 bytes por cada 255 bytes.
        int maxSize = data.Length + (data.Length / 255) + 16;
        byte[] output = new byte[maxSize];

        int written = LZ4Codec.Encode(data, 0, data.Length, output, 0, maxSize);

        // Recortamos el array al tamaño exacto que se escribió
        Array.Resize(ref output, written);
        return output;
    }

    /// <summary>
    /// Descomprime un array de bytes. 
    /// Requiere saber el tamaño original del dato (se lo tenés que pasar).
    /// </summary>
    public static byte[] Decompress(byte[] compressedData, int originalSize)
    {
        byte[] output = new byte[originalSize];
        LZ4Codec.Decode(compressedData, 0, compressedData.Length, output, 0, originalSize);
        return output;
    }
}