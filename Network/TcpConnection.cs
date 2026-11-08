/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SharpUtils.Utilities;

namespace SharpUtils.Network;

/// <summary>
/// Cliente TCP con handshake de autenticación.
/// </summary>
public static class TcpConnection
{
    /// <summary>
    /// Conecta a un servidor con handshake challenge-response usando password compartido.
    /// </summary>
    /// <param name="password">Si es null, usa <see cref="TcpServer.DefaultPassword"/>.</param>
    /// <param name="enableTLS">Si true, envuelve la conexión en TLS con certificado autofirmado.</param>
    /// <param name="connectionTimeout">Tiempo máximo de espera para establecer la conexión TCP, en milisegundos.</param>
    /// <returns>El <see cref="TcpClient"/> y el <see cref="Stream"/> ya autenticados.</returns>
    /// <exception cref="TimeoutException">Si no se establece la conexión TCP dentro del tiempo límite.</exception>
    /// <exception cref="IOException">Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.</exception>
    public static async Task<(TcpClient Client, Stream Stream)> ConnectAsync(string host, int port, string password = null, bool enableTLS = true, uint connectionTimeout = 10000)
    {
        password ??= TcpServer.DefaultPassword;

        var client = new TcpClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectionTimeout));
        try { await client.ConnectAsync(host, port, timeout.Token); }
        catch (OperationCanceledException) { client.Dispose(); throw new TimeoutException(); }

        Stream stream = client.GetStream();
        if (enableTLS)
        {
            var sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback((_, _, _, _) => true), null);
            await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);
            stream = sslStream;
        }

        PasswordHandShake(client, stream, password);
        return (client, stream);
    }

    /// <summary>
    /// Conecta a un servidor con handshake asimétrico ECDSA P-256 DER ASN.1 usando la clave privada del cliente.
    /// </summary>
    /// <param name="clientFingerprint">SHA-256 de la pública del cliente (32 bytes).</param>
    /// <param name="loadPrivateKey">Devuelve los bytes de la privada en formato ECPrivateKey (SEC1).</param>
    /// <param name="enableTLS">Si true, envuelve la conexión en TLS con certificado autofirmado.</param>
    /// <param name="connectionTimeout">Tiempo máximo de espera para establecer la conexión TCP, en milisegundos.</param>
    /// <returns>El <see cref="TcpClient"/> y el <see cref="Stream"/> ya autenticados.</returns>
    /// <exception cref="TimeoutException">Si no se establece la conexión TCP dentro del tiempo límite.</exception>
    /// <exception cref="IOException">Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.</exception>
    public static async Task<(TcpClient Client, Stream Stream)> ConnectAsync(string host, int port, byte[] clientFingerprint, Func<byte[]> loadPrivateKey, bool enableTLS = true, uint connectionTimeout = 10000)
    {
        var client = new TcpClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectionTimeout));
        try { await client.ConnectAsync(host, port, timeout.Token); }
        catch (OperationCanceledException) { client.Dispose(); throw new TimeoutException(); }

        Stream stream = client.GetStream();
        if (enableTLS)
        {
            var sslStream = new SslStream(stream, false, new RemoteCertificateValidationCallback((_, _, _, _) => true), null);
            await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);
            stream = sslStream;
        }

        KeysHandShake(client, stream, clientFingerprint, loadPrivateKey);
        return (client, stream);
    }

    /// <summary>
    /// Handshake challenge-response con password.
    /// </summary>
    /// <exception cref="IOException">Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.</exception>
    private static void PasswordHandShake(TcpClient client, Stream stream, string password)
    {
        try
        {
            client.ReceiveTimeout = 5000;

            // 1. Leer challenge (16 bytes random del server) sobre el túnel encriptado
            Span<byte> challenge = stackalloc byte[16];
            PacketProtocol.ReadExact(stream, challenge);

            // 2. Calcular y mandar response: SHA-256(challenge + password)
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            Span<byte> expected = stackalloc byte[16 + passwordBytes.Length];
            challenge.CopyTo(expected);
            passwordBytes.AsSpan().CopyTo(expected[16..]);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(expected, hash);
            stream.Write(hash);

            // 3. Leer confirmación (1 byte: 0x01 = OK).
            int b = stream.ReadByte();
            if (b != 0x01)
                throw new AuthenticationException("El servidor cerró la conexión durante el handshake.");

            client.ReceiveTimeout = 0;
        }
        catch
        {
            try { client.ReceiveTimeout = 0; } catch { }
            try { client.Close(); } catch { }
            throw;
        }
    }

    /// <summary>
    /// Handshake asimétrico con ECDSA P-256 DER ASN.1.
    /// </summary>
    /// <param name="clientFingerprint">SHA-256 de la pública del cliente (32 bytes).</param>
    /// <param name="loadPrivateKey">Devuelve los bytes de la privada en formato ECPrivateKey (SEC1).</param>
    /// <exception cref="IOException">Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.</exception>
    private static void KeysHandShake(TcpClient client, Stream stream, byte[] clientFingerprint, Func<byte[]> loadPrivateKey)
    {
        try
        {
            client.ReceiveTimeout = 5000;

            // 1. Mandar fingerprint
            stream.Write(clientFingerprint);

            // 2. Leer challenge (32 bytes random del server) sobre el túnel encriptado
            Span<byte> challenge = stackalloc byte[32];
            PacketProtocol.ReadExact(stream, challenge);

            // 3. Cargar la privada y firmar con manejo seguro de memoria:
            //    - GCHandle.Alloc con Pinned evita que el GC mueva el array (no quedan copias viejas).
            //    - CryptographicOperations.ZeroMemory sobrescribe los bytes antes de soltar el pin.
            //    - El using+Dispose del ECDsa libera el handle interno del SO (CNG/OpenSSL).
            byte[] privKey = loadPrivateKey();
            GCHandle pin = GCHandle.Alloc(privKey, GCHandleType.Pinned);
            byte[] signature;
            try
            {
                using var ecdsa = ECDsa.Create();
                ecdsa.ImportECPrivateKey(privKey, out _);
                signature = ecdsa.SignData(challenge, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(privKey);
                pin.Free();
            }

            // 4. Mandar [4 bytes length little-endian][firma]
            stream.Write(ByteCodec.GetInt32LE(signature.Length));
            stream.Write(signature);

            // 5. Leer confirmación (1 byte: 0x01 = OK)
            int b = stream.ReadByte();
            if (b != 0x01)
                throw new IOException("El servidor cerró la conexión durante el handshake.");

            client.ReceiveTimeout = 0;
        }
        catch
        {
            try { client.ReceiveTimeout = 0; } catch { }
            try { client.Close(); } catch { }
            throw;
        }
    }
}
