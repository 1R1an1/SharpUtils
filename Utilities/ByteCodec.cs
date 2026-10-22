/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace SharpUtils.Utilities;

/// <summary>
/// Conversión de bytes a tipos primitivos y viceversa, con endianness explícito.
/// </summary>
public static class ByteCodec
{
    // ============================================================
    //                          Get LE
    // ============================================================

    /// <summary>
    /// Escribe un short little-endian a bytes.
    /// </summary>
    /// <returns>Array de 2 bytes little-endian.</returns>
    public static byte[] GetInt16LE(short value)
    {
        byte[] result = new byte[2];
        BinaryPrimitives.WriteInt16LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un int little-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes little-endian.</returns>
    public static byte[] GetInt32LE(int value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un long little-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes little-endian.</returns>
    public static byte[] GetInt64LE(long value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un ushort little-endian a bytes.
    /// </summary>
    /// <returns>Array de 2 bytes little-endian.</returns>
    public static byte[] GetUInt16LE(ushort value)
    {
        byte[] result = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un uint little-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes little-endian.</returns>
    public static byte[] GetUInt32LE(uint value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un ulong little-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes little-endian.</returns>
    public static byte[] GetUInt64LE(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un float little-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes little-endian.</returns>
    public static byte[] GetSingleLE(float value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un double little-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes little-endian.</returns>
    public static byte[] GetDoubleLE(double value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteDoubleLittleEndian(result, value);
        return result;
    }

    // ============================================================
    //                          Get BE
    // ============================================================

    /// <summary>
    /// Escribe un short big-endian a bytes.
    /// </summary>
    /// <returns>Array de 2 bytes big-endian.</returns>
    public static byte[] GetInt16BE(short value)
    {
        byte[] result = new byte[2];
        BinaryPrimitives.WriteInt16BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un int big-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes big-endian.</returns>
    public static byte[] GetInt32BE(int value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un long big-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes big-endian.</returns>
    public static byte[] GetInt64BE(long value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un ushort big-endian a bytes.
    /// </summary>
    /// <returns>Array de 2 bytes big-endian.</returns>
    public static byte[] GetUInt16BE(ushort value)
    {
        byte[] result = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un uint big-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes big-endian.</returns>
    public static byte[] GetUInt32BE(uint value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un ulong big-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes big-endian.</returns>
    public static byte[] GetUInt64BE(ulong value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un float big-endian a bytes.
    /// </summary>
    /// <returns>Array de 4 bytes big-endian.</returns>
    public static byte[] GetSingleBE(float value)
    {
        byte[] result = new byte[4];
        BinaryPrimitives.WriteSingleBigEndian(result, value);
        return result;
    }

    /// <summary>
    /// Escribe un double big-endian a bytes.
    /// </summary>
    /// <returns>Array de 8 bytes big-endian.</returns>
    public static byte[] GetDoubleBE(double value)
    {
        byte[] result = new byte[8];
        BinaryPrimitives.WriteDoubleBigEndian(result, value);
        return result;
    }

    // ============================================================
    //                          Read LE
    // ============================================================

    /// <summary>
    /// Lee un short little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 2 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 2);
        return BinaryPrimitives.ReadInt16LittleEndian(source.Slice(offset, 2));
    }

    /// <summary>
    /// Lee un int little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadInt32LittleEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un long little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadInt64LittleEndian(source.Slice(offset, 8));
    }

    /// <summary>
    /// Lee un ushort little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 2 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 2);
        return BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(offset, 2));
    }

    /// <summary>
    /// Lee un uint little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadUInt32LittleEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un ulong little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64LE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadUInt64LittleEndian(source.Slice(offset, 8));
    }

    /// <summary>
    /// Lee un float little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingleLE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadSingleLittleEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un double little-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDoubleLE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadDoubleLittleEndian(source.Slice(offset, 8));
    }

    // ============================================================
    //                          Read BE
    // ============================================================

    /// <summary>
    /// Lee un short big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 2 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ReadInt16BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 2);
        return BinaryPrimitives.ReadInt16BigEndian(source.Slice(offset, 2));
    }

    /// <summary>
    /// Lee un int big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ReadInt32BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadInt32BigEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un long big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ReadInt64BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadInt64BigEndian(source.Slice(offset, 8));
    }

    /// <summary>
    /// Lee un ushort big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 2 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort ReadUInt16BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 2);
        return BinaryPrimitives.ReadUInt16BigEndian(source.Slice(offset, 2));
    }

    /// <summary>
    /// Lee un uint big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ReadUInt32BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadUInt32BigEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un ulong big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ReadUInt64BE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadUInt64BigEndian(source.Slice(offset, 8));
    }

    /// <summary>
    /// Lee un float big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 4 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ReadSingleBE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 4);
        return BinaryPrimitives.ReadSingleBigEndian(source.Slice(offset, 4));
    }

    /// <summary>
    /// Lee un double big-endian desde un array de bytes.
    /// </summary>
    /// <param name="offset">Posición desde donde leer.</param>
    /// <exception cref="ArgumentOutOfRangeException">Si <paramref name="offset"/> es negativo o el array no tiene 8 bytes desde <paramref name="offset"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ReadDoubleBE(ReadOnlySpan<byte> source, int offset = 0)
    {
        ValidateRead(source, offset, 8);
        return BinaryPrimitives.ReadDoubleBigEndian(source.Slice(offset, 8));
    }

    // ============================================================
    //                          Validation
    // ============================================================

    private static void ValidateRead(ReadOnlySpan<byte> source, int offset, int count)
    {
        if (source.IsEmpty)
            throw new ArgumentException(nameof(source));
        if (offset < 0 || (uint)offset + (uint)count > (uint)source.Length)
            throw new ArgumentOutOfRangeException(nameof(offset), "El offset no deja suficientes bytes para leer.");
    }
}
