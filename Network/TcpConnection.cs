/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpUtils.Network;

/// <summary>
/// Cliente TCP con handshake de autenticación challenge-response (igual que <see cref="TcpServer"/>).
/// </summary>
/// <remarks>
/// Ofrece dos métodos de conexión: <see cref="ConnectAsync"/> (sin encriptar) y 
/// <see cref="ConnectTLSAsync"/> (envolviendo la conexión en TLS para certificados autofirmados).
/// Ambos siempre hacen el handshake que espera <see cref="TcpServer"/> cuando 
/// <see cref="TcpServer.EnableAuth"/> es true. Si no le pasás password, usan
/// <see cref="TcpServer.DefaultPassword"/>.
/// Si el servidor no acepta la conexión dentro del tiempo indicado por
/// <c>connectionTimeout</c>, lanza <see cref="TimeoutException"/>.
/// Si el servidor rechaza el handshake (password incorrecta) o cierra la conexión
/// durante la autenticación, lanza <see cref="IOException"/>.
/// </remarks>
/// <example>
/// <code>
/// // Conexión en plano y password default:
/// var client = await TcpConnection.ConnectAsync("127.0.0.1", 5502);
///
/// // Conexión segura (TLS) y password custom:
/// var (client, stream) = await TcpConnection.ConnectSecureAsync("127.0.0.1", 5502, "mi-secreto");
/// </code>
/// </example>
public static class TcpConnection
{
    /// <summary>
    /// Conecta a un servidor en plano (sin encriptación) y hace el handshake challenge-response.
    /// </summary>
    /// <param name="host">Hostname o IP del servidor.</param>
    /// <param name="port">Puerto del servidor.</param>
    /// <param name="password">
    /// Password para el handshake. Si es null, usa <see cref="TcpServer.DefaultPassword"/>.
    /// </param>
    /// <param name="connectionTimeout">
    /// Tiempo máximo de espera para establecer la conexión TCP, en milisegundos.
    /// Por defecto es 10000 (10 segundos).
    /// </param>
    /// <returns>El <see cref="TcpClient"/> ya autenticado y listo para usar.</returns>
    /// <exception cref="TimeoutException">
    /// Si no se puede establecer la conexión TCP dentro del tiempo límite.
    /// </exception>
    /// <exception cref="IOException">
    /// Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.
    /// </exception>
    public static async Task<TcpClient> ConnectAsync(string host, int port, string password = null, uint connectionTimeout = 10000)
    {
        if (password == null)
            password = TcpServer.DefaultPassword;

        var client = new TcpClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectionTimeout));
        try { await client.ConnectAsync(host, port, timeout.Token); }
        catch (OperationCanceledException) { client.Dispose(); throw new TimeoutException(); }

        NetworkStream stream = client.GetStream();

        HandShake(client, stream, password);
        return client;
    }

    /// <summary>
    /// Conecta a un servidor envolviendo la conexión en TLS (acepta certificados autofirmados) y hace el handshake challenge-response.
    /// </summary>
    /// <param name="host">Hostname o IP del servidor.</param>
    /// <param name="port">Puerto del servidor.</param>
    /// <param name="password">Password para el handshake. Si es null, usa <see cref="TcpServer.DefaultPassword"/>.</param>
    /// <param name="connectionTimeout">
    /// Tiempo máximo de espera para establecer la conexión TCP, en milisegundos.
    /// Por defecto es 10000 (10 segundos).
    /// </param>
    /// <returns>Una tupla con el <see cref="TcpClient"/> y el <see cref="SslStream"/> ya autenticados y encriptados.</returns>
    /// <exception cref="TimeoutException">
    /// Si no se puede establecer la conexión TCP dentro del tiempo límite.
    /// </exception>
    /// <exception cref="IOException">
    /// Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.
    /// </exception>
    public static async Task<(TcpClient Client, Stream Stream)> ConnectTLSAsync(string host, int port, string password = null, uint connectionTimeout = 10000)
    {
        if (password == null)
            password = TcpServer.DefaultPassword;

        var client = new TcpClient();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(connectionTimeout));
        try { await client.ConnectAsync(host, port, timeout.Token); }
        catch (OperationCanceledException) { client.Dispose(); throw new TimeoutException(); }

        // 1. Envolver en TLS inmediatamente. El callback devuelve true siempre para aceptar certificados autofirmados.
        var networkStream = client.GetStream();
        var sslStream = new SslStream(networkStream, false, new RemoteCertificateValidationCallback((s, c, ch, e) => true), null);

        // Autenticar como cliente
        await sslStream.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12 | SslProtocols.Tls13, false);

        HandShake(client, sslStream, password);
        return (client, sslStream);
    }

    /// <summary>
    /// Realiza el handshake challenge-response con el servidor usando la contraseña indicada.
    /// </summary>
    /// <param name="client">Cliente TCP conectado.</param>
    /// <param name="stream">Stream sobre el que se realizará el handshake.</param>
    /// <param name="password">Password utilizada para responder al challenge del servidor.</param>
    /// <exception cref="IOException">
    /// Si el servidor rechaza el handshake o cierra la conexión durante la autenticación.
    /// </exception>
    private static void HandShake(TcpClient client, Stream stream, string password)
    {
        try
        {
            client.ReceiveTimeout = 5000;

            // 2. Leer challenge (16 bytes random del server) sobre el túnel encriptado
            Span<byte> challenge = stackalloc byte[16];
            PacketProtocol.ReadExact(stream, challenge);

            // 3. Calcular y mandar response: SHA-256(challenge + password)
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            Span<byte> expected = stackalloc byte[16 + passwordBytes.Length];
            challenge.CopyTo(expected);
            passwordBytes.AsSpan().CopyTo(expected[16..]);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(expected, hash);
            stream.Write(hash);

            // 4. Leer confirmación (1 byte: 0x01 = OK).
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
