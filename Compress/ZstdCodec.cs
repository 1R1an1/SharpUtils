using System;
using ZstdNet;

namespace SharpUtils.Compress;

public static class ZstdCodec
{
    /// <summary>
    /// Comprime un array de bytes usando Zstandard con el nivel especificado.
    /// </summary>
    /// <param name="datos">Array de bytes a comprimir</param>
    /// <param name="nivel">Nivel de compresión (1-22). Por defecto 3. Mayor = más compresión pero más lento.</param>
    /// <returns>Array de bytes comprimido</returns>
    /// <exception cref="ArgumentNullException">Se lanza si datos es null</exception>
    /// <exception cref="ArgumentOutOfRangeException">Se lanza si el nivel no está entre 1 y 22</exception>
    public static byte[] Compress(ReadOnlySpan<byte> datos, int nivel = 3)
    {
        if (nivel < 1 || nivel > 22)
            throw new ArgumentOutOfRangeException(nameof(nivel), "El nivel debe estar entre 1 y 22");

        using (var compresor = new Compressor(new CompressionOptions(nivel)))
            return compresor.Wrap(datos);
    }

    /// <summary>
    /// Descomprime un array de bytes comprimido con Zstandard.
    /// </summary>
    /// <param name="datosComprimidos">Array de bytes comprimido</param>
    /// <returns>Array de bytes descomprimido</returns>
    /// <exception cref="ArgumentNullException">Se lanza si datosComprimidos es null</exception>
    public static byte[] Decompress(ReadOnlySpan<byte> datosComprimidos)
    {
        using (var descompresor = new Decompressor())
            return descompresor.Unwrap(datosComprimidos);
    }
}
