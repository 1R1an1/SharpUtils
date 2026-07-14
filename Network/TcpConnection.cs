using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SharpUtils.Network;

/// <summary>
/// Cliente TCP con handshake de autenticación challenge-response (igual que <see cref="TcpServer"/>).
/// </summary>
/// <remarks>
/// <see cref="ConnectAsync"/> siempre hace el handshake que espera <see cref="TcpServer"/>
/// cuando <see cref="TcpServer.EnableAuth"/> es true. Si no le pasás password, usa
/// <see cref="TcpServer.DefaultPassword"/>.
/// Si el server rechaza (password incorrecta) o si pasa de 5 segundos sin respuesta,
/// lanza <see cref="IOException"/> — porque el server cierra el socket y la próxima
/// lectura falla como si se hubiera cortado la conexión en cualquier otro momento.
/// </remarks>
/// <example>
/// <code>
/// // Con password default:
/// var client = await TcpConnection.ConnectAsync("127.0.0.1", 5502);
///
/// // Con password custom:
/// var client = await TcpConnection.ConnectAsync("127.0.0.1", 5502, "mi-secreto");
/// </code>
/// </example>
public static class TcpConnection
{
    /// <summary>
    /// Conecta a un servidor y hace el handshake challenge-response.
    /// </summary>
    /// <param name="host">Hostname o IP del servidor.</param>
    /// <param name="port">Puerto del servidor.</param>
    /// <param name="password">
    /// Password para el handshake. Si es null, usa <see cref="TcpServer.DefaultPassword"/>.
    /// </param>
    /// <returns>El <see cref="TcpClient"/> ya autenticado y listo para usar.</returns>
    /// <exception cref="IOException">
    /// Si el server rechaza el handshake (password incorrecta) o si pasa de 5s sin respuesta.
    /// </exception>
    public static async Task<TcpClient> ConnectAsync(string host, int port, string password = null)
    {
        if (password == null)
            password = TcpServer.DefaultPassword;

        var client = new TcpClient();
        await client.ConnectAsync(host, port);

        NetworkStream stream = client.GetStream();
        client.ReceiveTimeout = 5000;

        try
        {
            // 1. Leer challenge (16 bytes random del server)
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
            // Si el server rechazó, cierra el socket y ReadByte devuelve -1
            // o lanza IOException → cae al catch y relanza.
            int b = stream.ReadByte();
            if (b != 0x01)
                throw new IOException("El servidor cerró la conexión durante el handshake.");

            client.ReceiveTimeout = 0;
            return client;
        }
        catch
        {
            try { client.ReceiveTimeout = 0; } catch { }
            try { client.Close(); } catch { }
            throw;
        }
    }
}
