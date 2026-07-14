using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpUtils.Network;

/// <summary>
/// Servidor TCP async con handshake de autenticación opcional (challenge-response con SHA-256).
/// </summary>
/// <remarks>
/// Si <see cref="EnableAuth"/> es true (default), cada cliente que conecta tiene que pasar
/// el handshake antes de que se dispare <see cref="OnClientAsync"/>. Si el handshake falla
/// o pasa de 5 segundos, el server cierra el socket sin mandar nada y sin disparar
/// <see cref="OnClientConnected"/>.
/// </remarks>
/// <example>
/// <code>
/// var server = new TcpServer(IPAddress.Any, 5502);
/// server.Password = "mi-secreto";   // opcional, sino usa la default
/// server.OnClientAsync = async client => { /* tu loop */ };
/// server.Start();
/// </code>
/// </example>
public class TcpServer
{
    /// <summary>
    /// Password por defecto compartida por <see cref="TcpServer"/> y <see cref="TcpConnection"/>.
    /// Es un literal hardcodeado para que ambas partes tengan la misma siempre, sin importar
    /// cuántas veces se ejecute la app. Cambiala por la tuya si querés.
    /// </summary>
    public const string DefaultPassword = "K7$mQ9!xL2#vR8pZ4@wN1cB6&hT3yF0j";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, TcpClient> _clients = new();

    /// <summary>
    /// Si true, cada cliente debe pasar el handshake antes de que se dispare
    /// <see cref="OnClientAsync"/>. Se setea por constructor.
    /// </summary>
    public bool EnableAuth { get; }

    /// <summary>
    /// Password usada por el handshake. Por defecto es <see cref="DefaultPassword"/>.
    /// Cambiala antes de <see cref="Start"/> si querés otra.
    /// </summary>
    public string Password { get; set; } = DefaultPassword;

    /// <summary>
    /// Callback que implementás vos. Recibe el <see cref="TcpClient"/> ya autenticado
    /// (si <see cref="EnableAuth"/> es true). Armás tu loop de lectura/escritura con tu protocolo.
    /// </summary>
    public Func<TcpClient, Task> OnClientAsync;

    /// <summary>
    /// Se dispara cuando un cliente pasa el handshake (o conecta, si <see cref="EnableAuth"/> es false).
    /// Recibe la IP:Puerto del cliente.
    /// </summary>
    public event Action<string> OnClientConnected;

    /// <summary>
    /// Se dispara cuando un cliente se desconecta. Recibe la IP:Puerto del cliente.
    /// </summary>
    public event Action<string> OnClientDisconnected;

    /// <summary>
    /// Se dispara ante errores del server (cliente que cae, excepciones, etc.). Recibe el mensaje.
    /// </summary>
    public event Action<string> OnError;

    /// <summary>
    /// Crea un nuevo servidor.
    /// </summary>
    /// <param name="ip">IP donde escuchar.</param>
    /// <param name="port">Puerto donde escuchar.</param>
    /// <param name="enableAuth">Si true (default), exige handshake a cada cliente.</param>
    public TcpServer(IPAddress ip, int port, bool enableAuth = true)
    {
        _listener = new TcpListener(ip, port);
        EnableAuth = enableAuth;
    }

    /// <summary>
    /// Arranca el accept loop en background. Lanza si EnableAuth es true y Password está vacía.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si EnableAuth es true y Password está vacía.</exception>
    public void Start()
    {
        if (EnableAuth && string.IsNullOrEmpty(Password))
            throw new InvalidOperationException("EnableAuth está en true pero Password está vacía.");
        _listener.Start();
        ThreadPool.QueueUserWorkItem(async _ => await AcceptLoopAsync());
    }

    /// <summary>
    /// Detiene el accept loop y el listener. Los clientes ya conectados siguen su curso.
    /// </summary>
    public void Stop()
    {
        _cts.Cancel();
        _listener.Stop();
    }

    /// <summary>
    /// Cierra un cliente por IP. Dispara <see cref="OnClientDisconnected"/> vía el finally del handler.
    /// </summary>
    /// <param name="ip">IP:Puerto del cliente a desconectar.</param>
    public void DisconnectClient(string ip)
    {
        if (_clients.TryGetValue(ip, out var client))
            client.Close();
    }

    /// <summary>
    /// Obtiene el <see cref="TcpClient"/> de una IP, o null si no está conectado.
    /// </summary>
    /// <param name="ip">IP:Puerto del cliente.</param>
    /// <returns>El TcpClient o null.</returns>
    public TcpClient GetClient(string ip)
    {
        _clients.TryGetValue(ip, out var c);
        return c;
    }

    /// <summary>
    /// IPs (con puerto) de todos los clientes conectados.
    /// </summary>
    public ICollection<string> ClientIps => _clients.Keys;
    public ICollection<TcpClient> Clients => _clients.Values;

    // -----------------------------------------------------------------------

    private async Task AcceptLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; } // Stop() fue llamado
            catch (Exception ex)
            {
                if (!_cts.Token.IsCancellationRequested)
                    OnError?.Invoke($"Error aceptando cliente: {ex.Message}");
                break;
            }

            ThreadPool.QueueUserWorkItem(async _ => await HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        string ip = client.Client.RemoteEndPoint?.ToString() ?? "?";

        // Handshake antes de registrar al cliente o avisar que conectó.
        // Si falla, cerrar y chau — sin OnClientConnected, sin OnError, sin nada.
        if (EnableAuth)
        {
            if (!DoHandshake(client))
            {
                try { client.Close(); } catch { }
                return;
            }
        }

        _clients[ip] = client;
        client.NoDelay = true;
        OnClientConnected?.Invoke(ip);

        try
        {
            if (OnClientAsync != null)
                await OnClientAsync(client);
        }
        catch (Exception ex)
        {
            // EndOfStreamException, IOException, SocketException, etc. → cliente caído
            OnError?.Invoke(ex.Message);
        }
        finally
        {
            try { client.Close(); } catch { }
            _clients.TryRemove(ip, out _);
            OnClientDisconnected?.Invoke(ip);
        }
    }

    /// <summary>
    /// Handshake challenge-response con SHA-256. Devuelve true si OK, false si falló.
    /// Wire:
    ///   1. Server → Client: 16 bytes random (challenge)
    ///   2. Client → Server: 32 bytes SHA-256(challenge + password)
    ///   3. Server valida. Si OK → manda 1 byte 0x01. Si NO OK → cierra sin mandar nada.
    /// Timeout: 5s para que el cliente mande la respuesta.
    /// </summary>
    private bool DoHandshake(TcpClient client)
    {
        NetworkStream stream = null;
        try
        {
            stream = client.GetStream();
            client.ReceiveTimeout = 5000;

            // 1. Generar y mandar challenge (16 bytes random)
            Span<byte> challenge = stackalloc byte[16];
            RandomNumberGenerator.Fill(challenge);
            stream.Write(challenge);

            // 2. Leer respuesta (32 bytes SHA-256)
            Span<byte> response = stackalloc byte[32];
            PacketProtocol.ReadExact(stream, response);

            // 3. Calcular hash esperado: SHA-256(challenge + password)
            byte[] passwordBytes = Encoding.UTF8.GetBytes(Password);
            Span<byte> expected = stackalloc byte[16 + passwordBytes.Length];
            challenge.CopyTo(expected);
            passwordBytes.AsSpan().CopyTo(expected[16..]);
            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(expected, hash);

            // 4. Comparar con FixedTimeEquals para evitar timing attacks
            bool ok = CryptographicOperations.FixedTimeEquals(hash, response);

            if (!ok)
            {
                client.ReceiveTimeout = 0;
                return false;
            }

            // 5. Mandar OK (1 byte) y dejar el socket abierto
            stream.WriteByte(0x01);
            client.ReceiveTimeout = 0;
            return true;
        }
        catch
        {
            // Timeout, socket cerrado, lo que sea → auth falló, cerrar silenciosamente
            try { if (stream != null) stream.ReadTimeout = 0; } catch { }
            return false;
        }
    }
}
