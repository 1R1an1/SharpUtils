/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace SharpUtils.Security;

/// <summary>
/// Cifrado AES-CTR para streaming de UDP. 
/// Es de instancia y maneja pérdidas de paquetes automáticamente inyectando un número de secuencia.
/// </summary>
public class AesCTRCipher
{
    private readonly byte[] _key;
    private readonly byte[] _baseIv;
    private readonly ICryptoTransform _ecbEncryptor;
    private ulong _currentSequence = 0; // El contador interno que la clase maneja sola

    // NUEVO: Arrays reutilizables. Se piden a la memoria UNA SOLA VEZ al crear la clase.
    private readonly byte[] _counterBuffer;
    private readonly byte[] _keystreamBuffer;

    public AesCTRCipher(byte[] password, byte[] iv)
    {
        if (iv.Length != 16) throw new ArgumentException("El IV debe ser de 16 bytes.");

        _key = new byte[32];
        SHA256.HashData(password, _key);
        _baseIv = (byte[])iv.Clone();

        _counterBuffer = new byte[16];
        _keystreamBuffer = new byte[16];

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        _ecbEncryptor = aes.CreateEncryptor();
    }

    public AesCTRCipher(string password, byte[] iv) : this(Encoding.UTF8.GetBytes(password), iv) { }

    /// <summary>
    /// Encripta el audio y le inyecta un número de secuencia oculto al principio.
    /// </summary>
    public byte[] Encrypt(ReadOnlySpan<byte> audioPlano)
    {
        // Creamos el paquete final: [4 bytes de Secuencia] + [Audio Encriptado]
        byte[] paqueteFinal = new byte[4 + audioPlano.Length];

        // 1. Escribimos el número de secuencia actual al principio (Big-Endian)
        BinaryPrimitives.WriteUInt32BigEndian(paqueteFinal, (uint)_currentSequence);

        // 2. Encriptamos el audio directamente en el resto del paquete
        ProcessCtr(audioPlano, paqueteFinal.AsSpan(4), _currentSequence);

        // 3. Avanzamos nuestro contador interno para el próximo paquete
        _currentSequence++;

        return paqueteFinal;
    }

    public int Encrypt(ReadOnlySpan<byte> audioPlano, Span<byte> destino)
    {
        int longitudNecesaria = 4 + audioPlano.Length;

        if (destino.Length < longitudNecesaria)
            throw new ArgumentException("El buffer de destino es demasiado pequeño.", nameof(destino));

        BinaryPrimitives.WriteUInt32BigEndian(destino, (uint)_currentSequence);
        ProcessCtr(audioPlano, destino.Slice(4, audioPlano.Length), _currentSequence);
        _currentSequence++;

        return longitudNecesaria;
    }

    /// <summary>
    /// Desencripta el paquete. Lee el número de secuencia oculto y lo desencripta sin importar el orden.
    /// </summary>
    public byte[] Decrypt(ReadOnlySpan<byte> paqueteRecibido)
    {
        if (paqueteRecibido.Length < 4) return Array.Empty<byte>();

        // 1. Leemos el ticket de red
        uint seqRecibida = BinaryPrimitives.ReadUInt32BigEndian(paqueteRecibido);

        // 2. Desencriptamos SIEMPRE usando el ticket de este paquete específico
        // (Así da igual si llegó desordenado, el AES-CTR lo resuelve)
        byte[] audioPlano = new byte[paqueteRecibido.Length - 4];
        ProcessCtr(paqueteRecibido.Slice(4), audioPlano, seqRecibida);

        // 3. Solo avanzamos nuestro reloj si el paquete que llegó es MÁS NUEVO que el que esperábamos
        // Si llega un paquete viejo (atrasado), no movemos el reloj para atrás.
        if (seqRecibida >= _currentSequence)
            _currentSequence = seqRecibida + 1;

        return audioPlano;
    }

    public int Decrypt(ReadOnlySpan<byte> paqueteRecibido, Span<byte> destino)
    {
        if (paqueteRecibido.Length < 4)
            return 0;

        int longitudAudio = paqueteRecibido.Length - 4;

        if (destino.Length < longitudAudio)
            throw new ArgumentException("El buffer de destino es demasiado pequeño.", nameof(destino));

        uint seqRecibida = BinaryPrimitives.ReadUInt32BigEndian(paqueteRecibido);

        ProcessCtr(paqueteRecibido.Slice(4), destino.Slice(0, longitudAudio), seqRecibida);

        if (seqRecibida >= _currentSequence)
            _currentSequence = seqRecibida + 1;

        return longitudAudio;
    }

    /// <summary>
    /// El motor CTR puro. Calcula el bloque inicial basándose en el número de secuencia.
    /// </summary>
    private void ProcessCtr(ReadOnlySpan<byte> input, Span<byte> output, ulong seq)
    {
        // Clonamos el IV base a un array normal para poder modificarlo
        _baseIv.AsSpan().CopyTo(_counterBuffer);

        // Calculamos en qué bloque del contador estamos.
        ulong startBlock = seq * (ulong)(input.Length / 16);

        // Sumamos ese startBlock al contador (escribimos en los últimos 8 bytes en formato Big-Endian)
        BinaryPrimitives.WriteUInt64BigEndian(_counterBuffer.AsSpan(8), startBlock);

        int i = 0;
        while (i < input.Length)
        {
            // 1. Encryptar el contador actual directamente en el array keystream
            _ecbEncryptor.TransformBlock(_counterBuffer, 0, 16, _keystreamBuffer, 0);

            // 2. Mezclar (XOR) el ruido con tus datos
            int chunkSize = Math.Min(16, input.Length - i);
            for (int j = 0; j < chunkSize; j++)
            {
                output[i + j] = (byte)(input[i + j] ^ _keystreamBuffer[j]);
            }

            // 3. Incrementar el contador
            IncrementCounter(_counterBuffer);
            i += 16;
        }
    }

    private void IncrementCounter(byte[] counter)
    {
        for (int i = 15; i >= 0; i--)
        {
            if (++counter[i] != 0)
                break;
        }
    }
}
