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
    private long _currentSequence = 0; // El contador interno que la clase maneja sola

    public AesCTRCipher(byte[] password, byte[] iv)
    {
        if (iv.Length != 16) throw new ArgumentException("El IV debe ser de 16 bytes.");

        _key = new byte[32];
        SHA256.HashData(password, _key);
        _baseIv = (byte[])iv.Clone();

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
    public byte[] Encrypt(byte[] audioPlano)
    {
        // Creamos el paquete final: [4 bytes de Secuencia] + [Audio Encriptado]
        byte[] paqueteFinal = new byte[4 + audioPlano.Length];

        // 1. Escribimos el número de secuencia actual al principio (Big-Endian)
        BinaryPrimitives.WriteInt32BigEndian(paqueteFinal, (int)_currentSequence);

        // 2. Encriptamos el audio directamente en el resto del paquete
        ProcessCtr(audioPlano, paqueteFinal.AsSpan(4), _currentSequence);

        // 3. Avanzamos nuestro contador interno para el próximo paquete
        _currentSequence++;

        return paqueteFinal;
    }

    /// <summary>
    /// Desencripta el paquete. Lee el número de secuencia oculto y lo desencripta sin importar el orden.
    /// </summary>
    public byte[] Decrypt(byte[] paqueteRecibido)
    {
        if (paqueteRecibido.Length < 4) return Array.Empty<byte>();

        // 1. Leemos el ticket de red
        int seqRecibida = BinaryPrimitives.ReadInt32BigEndian(paqueteRecibido);

        // 2. Desencriptamos SIEMPRE usando el ticket de este paquete específico
        // (Así da igual si llegó desordenado, el AES-CTR lo resuelve)
        byte[] audioPlano = new byte[paqueteRecibido.Length - 4];
        ProcessCtr(paqueteRecibido.AsSpan(4), audioPlano, seqRecibida);

        // 3. Solo avanzamos nuestro reloj si el paquete que llegó es MÁS NUEVO que el que esperábamos
        // Si llega un paquete viejo (atrasado), no movemos el reloj para atrás.
        if (seqRecibida >= _currentSequence)
        {
            _currentSequence = seqRecibida + 1;
        }

        return audioPlano;
    }

    /// <summary>
    /// El motor CTR puro. Calcula el bloque inicial basándose en el número de secuencia.
    /// </summary>
    private void ProcessCtr(ReadOnlySpan<byte> input, Span<byte> output, long seq)
    {
        Span<byte> counter = stackalloc byte[16];
        _baseIv.AsSpan().CopyTo(counter);

        // Calculamos en qué bloque del contador estamos. 
        // Si cada paquete tiene 4096 bytes, cada paquete avanza 256 bloques (4096 / 16).
        long startBlock = seq * (input.Length / 16);

        // Sumamos ese startBlock al contador base
        BinaryPrimitives.WriteInt64BigEndian(counter.Slice(8), startBlock);

        Span<byte> keystream = stackalloc byte[16];
        byte[] counterArray = counter.ToArray(); // ECB de .NET a veces pide array

        int i = 0;
        while (i < input.Length)
        {
            _ecbEncryptor.TransformBlock(counterArray, 0, 16, keystream.ToArray(), 0);

            int chunkSize = Math.Min(16, input.Length - i);
            for (int j = 0; j < chunkSize; j++)
            {
                output[i + j] = (byte)(input[i + j] ^ keystream[j]);
            }

            IncrementCounter(counterArray);
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