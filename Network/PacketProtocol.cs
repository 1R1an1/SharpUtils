/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace SharpUtils.Network;

public enum Endianness { Little, Big }
public enum LengthSize { Int32, Int64 }

/// <summary>
/// Helpers estáticos para leer y escribir tipos básicos sobre un <see cref="Stream"/>,
/// respetando endianness y tamaño de length configurables.
/// </summary>
/// <remarks>
/// No es un protocolo en sí — son piezas para que cada proyecto arme su propio protocolo
/// encima. La configuración (Endian, Length) es global al proceso. Si dos protocolos del
/// mismo proceso necesitan configs distintas, esto no te sirve — pero ese caso casi nunca
/// ocurre porque cada app es un proceso separado.
/// </remarks>
/// <example>
/// <code>
/// public static class MiProtocolo
/// {
///     public static void Send(Stream s, string msg)
///     {
///         lock (s) PacketProtocol.WriteString(s, msg);
///     }
///     public static string Read(Stream s) => PacketProtocol.ReadString(s);
/// }
/// </code>
/// </example>
public static class PacketProtocol
{
    /// <summary>
    /// Endianness usado al escribir y leer enteros. Por defecto Little.
    /// </summary>
    public static Endianness Endian { get; set; } = Endianness.Little;

    /// <summary>
    /// Tamaño del entero del length (4 u 8 bytes). Por defecto Int32.
    /// </summary>
    public static LengthSize Length { get; set; } = LengthSize.Int32;

    /// <summary>
    /// Lee exactamente la cantidad de bytes pedida del stream.
    /// </summary>
    /// <param name="stream">Stream del cual leer.</param>
    /// <param name="buffer">Buffer destino. Se llenan exactamente buffer.Length bytes.</param>
    /// <exception cref="EndOfStreamException">Se lanza si el socket se cierra antes de completar la lectura.</exception>
    /// <remarks>
    /// Es internal porque es un detalle de implementación. Los métodos públicos
    /// (<see cref="ReadInt"/>, <see cref="ReadString"/>, <see cref="ReadBytes"/>) lo usan internamente.
    /// </remarks>
    internal static void ReadExact(Stream stream, Span<byte> buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer[total..]);
            if (read == 0) throw new EndOfStreamException("El socket se cerró.");
            total += read;
        }
    }

    /// <summary>
    /// Escribe un número entero al stream. Usa 4 u 8 bytes según <see cref="Length"/>,
    /// y el endianness según <see cref="Endian"/>.
    /// </summary>
    /// <param name="stream">Stream destino. Escribe directo, no acumula en memoria.</param>
    /// <param name="value">Valor a escribir. Si <see cref="Length"/> es Int32 se trunca a 32 bits.</param>
    public static void WriteInt(Stream stream, long value)
    {
        if (Length == LengthSize.Int32)
        {
            Span<byte> buf = stackalloc byte[4];
            if (Endian == Endianness.Little) BinaryPrimitives.WriteInt32LittleEndian(buf, (int)value);
            else BinaryPrimitives.WriteInt32BigEndian(buf, (int)value);
            stream.Write(buf);
        }
        else
        {
            Span<byte> buf = stackalloc byte[8];
            if (Endian == Endianness.Little) BinaryPrimitives.WriteInt64LittleEndian(buf, value);
            else BinaryPrimitives.WriteInt64BigEndian(buf, value);
            stream.Write(buf);
        }
    }

    /// <summary>
    /// Lee un número entero del stream. Mismo principio que <see cref="WriteInt"/>.
    /// </summary>
    /// <param name="stream">Stream del cual leer.</param>
    /// <returns>El valor leído como long (cabe tanto Int32 como Int64).</returns>
    public static long ReadInt(Stream stream)
    {
        if (Length == LengthSize.Int32)
        {
            Span<byte> buf = stackalloc byte[4];
            ReadExact(stream, buf);
            return Endian == Endianness.Little
                ? BinaryPrimitives.ReadInt32LittleEndian(buf)
                : BinaryPrimitives.ReadInt32BigEndian(buf);
        }
        else
        {
            Span<byte> buf = stackalloc byte[8];
            ReadExact(stream, buf);
            return Endian == Endianness.Little
                ? BinaryPrimitives.ReadInt64LittleEndian(buf)
                : BinaryPrimitives.ReadInt64BigEndian(buf);
        }
    }

    /// <summary>
    /// Escribe un string al stream: primero el largo (con <see cref="WriteInt"/>) y
    /// después los bytes UTF-8.
    /// </summary>
    /// <param name="stream">Stream destino.</param>
    /// <param name="str">String a escribir. El largo respeta la config <see cref="Length"/> (4 u 8 bytes).</param>
    public static void WriteString(Stream stream, string str)
    {
        byte[] data = Encoding.UTF8.GetBytes(str);
        WriteInt(stream, data.Length);
        stream.Write(data);
    }

    /// <summary>
    /// Lee un string del stream. Necesita que se haya escrito con <see cref="WriteString"/>.
    /// </summary>
    /// <param name="stream">Stream del cual leer.</param>
    /// <returns>El string leído.</returns>
    public static string ReadString(Stream stream)
    {
        long len = ReadInt(stream);
        byte[] data = new byte[len];
        ReadExact(stream, data);
        return Encoding.UTF8.GetString(data);
    }

    /// <summary>
    /// Escribe un array de bytes crudo al stream: primero el largo (con <see cref="WriteInt"/>)
    /// y después los bytes. Sirve para mandar archivos binarios, imágenes, lo que sea.
    /// </summary>
    /// <param name="stream">Stream destino.</param>
    /// <param name="data">Bytes a escribir. El largo respeta la config <see cref="Length"/>.</param>
    public static async Task WriteBytesAsync(Stream stream, ReadOnlyMemory<byte> data)
    {
        WriteInt(stream, data.Length);
        await stream.WriteAsync(data);
    }

    /// <summary>
    /// Escribe un array de bytes crudo al stream: primero el largo (con <see cref="WriteInt"/>)
    /// y después los bytes. Sirve para mandar archivos binarios, imágenes, lo que sea.
    /// </summary>
    /// <param name="stream">Stream destino.</param>
    /// <param name="data">Bytes a escribir. El largo respeta la config <see cref="Length"/>.</param>
    public static void WriteBytes(Stream stream, ReadOnlySpan<byte> data)
    {
        WriteInt(stream, data.Length);
        stream.Write(data);
    }

    /// <summary>
    /// Lee un array de bytes crudo escrito con <see cref="WriteBytes"/>.
    /// </summary>
    /// <param name="stream">Stream del cual leer.</param>
    /// <returns>Los bytes leídos.</returns>
    public static byte[] ReadBytes(Stream stream)
    {
        long len = ReadInt(stream);
        byte[] data = new byte[len];
        ReadExact(stream, data);
        return data;
    }
}
