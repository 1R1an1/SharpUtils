/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpUtils.Network;

/// <summary>
/// Modo de autenticación que exige el <see cref="TcpServer"/> a cada cliente que conecta.
/// </summary>
public enum AuthMode
{
    /// <summary>
    /// No exige handshake. Cualquier cliente que conecta se acepta.
    /// </summary>
    None,
    /// <summary>
    /// Handshake challenge-response con SHA-256 y password compartido.
    /// </summary>
    PasswordChallenge,
    /// <summary>
    /// Handshake asimétrico con firma ECDSA P-256 DER ASN.1. Cada cliente tiene su propia clave privada.
    /// </summary>
    AsymmetricKeys
}

/// <summary>
/// Servidor TCP async con TLS opcional y <see cref="AuthMode"/> configurable.
/// </summary>
public class TcpServer : IDisposable
{
    /// <summary>
    /// Password por defecto compartida por <see cref="TcpServer"/> y <see cref="TcpConnection"/>.
    /// </summary>
    public const string DefaultPassword = "K7$mQ9!xL2#vR8pZ4@wN1cB6&hT3yF0j";

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<IPEndPoint, TcpClient> _clients = new();

    /// <summary>
    /// Asocia cada <see cref="TcpClient"/> con su <see cref="Stream"/> desencriptado. Solo se llena si <see cref="EnableTLS"/> es true.
    /// </summary>
    public readonly ConcurrentDictionary<TcpClient, Stream> SecureStreams = new();

    /// <summary>
    /// Certificado autofirmado en memoria usado para TLS.
    /// </summary>
    private readonly X509Certificate2 _serverCert;

    /// <summary>
    /// Modo de autenticación exigido a cada cliente que conecta.
    /// </summary>
    public AuthMode AuthMode { get; }

    /// <summary>
    /// Si true, la conexión se envuelve en un <see cref="SslStream"/>. Si false, los datos viajan en plano.
    /// </summary>
    public bool EnableTLS { get; }

    /// <summary>
    /// Password usada en <see cref="AuthMode.PasswordChallenge"/>.
    /// </summary>
    public string Password { get; set; } = DefaultPassword;

    /// <summary>
    /// Claves públicas autorizadas para <see cref="AuthMode.AsymmetricKeys"/>.
    /// Clave: hex minúsculo del SHA-256 de la pública del cliente. Valor: SPKI bytes.
    /// </summary>
    public ConcurrentDictionary<string, byte[]> AuthorizedPublicKeys { get; set { if (value == null) return; else field = value; } } = new();

    /// <summary>
    /// Loop de manejo de cada cliente autenticado. Recibe el <see cref="TcpClient"/> y su <see cref="Stream"/>.
    /// </summary>
    public Func<TcpClient, Stream, Task> OnClientAsync;

    /// <summary>
    /// Se dispara cuando un cliente pasa el handshake (o conecta, si <see cref="AuthMode"/> es <see cref="AuthMode.None"/>).
    /// Recibe la IP:Puerto del cliente y su fingerPrintHex si <see cref="AuthMode"/> es <see cref="AuthMode.AsymmetricKeys"/>.
    /// </summary>
    public event Action<IPEndPoint, string> OnClientConnected;

    /// <summary>
    /// Se dispara cuando un cliente se desconecta. Recibe la IP:Puerto del cliente.
    /// </summary>
    public event Action<IPEndPoint> OnClientDisconnected;

    /// <summary>
    /// Se dispara ante errores del server. Recibe la excepción.
    /// </summary>
    public event Action<Exception> OnError;

    /// <summary>
    /// Se dispara cuando el handshake de un cliente falla.
    /// </summary>
    public event Action<TcpClient> OnAuthFail;

    /// <summary>
    /// Crea un nuevo servidor.
    /// </summary>
    /// <param name="enableTLS">Si true, encripta la conexión con TLS.</param>
    /// <param name="authMode">Modo de autenticación exigido a cada cliente.</param>
    public TcpServer(IPAddress ip, int port, bool enableTLS = true, AuthMode authMode = AuthMode.PasswordChallenge)
    {
        _listener = new TcpListener(ip, port);
        AuthMode = authMode;
        EnableTLS = enableTLS;

        // Generar certificado autofirmado al nacer la clase si TLS está activado
        if (enableTLS)
            _serverCert = GenerateSelfSignedCert();
    }

    /// <summary>
    /// Arranca el accept loop en background.
    /// </summary>
    /// <exception cref="InvalidOperationException">Si el modo de auth requiere configuración faltante.</exception>
    public void Start()
    {
        switch (AuthMode)
        {
            case AuthMode.PasswordChallenge when string.IsNullOrEmpty(Password):
                throw new InvalidOperationException("AuthMode es PasswordChallenge pero Password está vacía.");
        }
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
    /// Cierra un cliente.
    /// </summary>
    /// <param name="ip">IP:Puerto del cliente a desconectar.</param>
    /// <returns>True si el cliente existía y fue cerrado, false si no se encontró.</returns>
    public bool TryDisconnectClient(IPEndPoint ip)
    {
        if (_clients.TryGetValue(ip, out var client))
        {
            client.Close();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Obtiene el <see cref="TcpClient"/> de un cliente.
    /// </summary>
    /// <param name="ip">IP:Puerto del cliente.</param>
    /// <param name="client">El TcpClient del cliente, null si no se encontro el cliente.</param>
    /// <returns>True si el cliente existe, false de lo contrario.</returns>
    public bool TryGetClient(IPEndPoint ip, out TcpClient client) => _clients.TryGetValue(ip, out client);

    /// <summary>
    /// Clientes actualmente conectados, como diccionario de solo lectura.
    /// </summary>
    public ReadOnlyDictionary<IPEndPoint, TcpClient> Clients => _clients.AsReadOnly();

    // -----------------------------------------------------------------------

    /// <summary>
    /// Loop de aceptación de conexiones entrantes.
    /// </summary>
    private async Task AcceptLoopAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token);
                ThreadPool.QueueUserWorkItem(n => _ = HandleClientAsync(client));
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException) { break; } // Stop() fue llamado
            catch (Exception ex)
            {
                if (!_cts.Token.IsCancellationRequested)
                    OnError?.Invoke(ex);
                break;
            }
        }
    }

    /// <summary>
    /// Maneja el ciclo de vida de un cliente: TLS, handshake, dispatch a <see cref="OnClientAsync"/>, cleanup.
    /// </summary>
    private async Task HandleClientAsync(TcpClient client)
    {
        IPEndPoint ip = (IPEndPoint)client.Client.RemoteEndPoint ?? null;
        bool connectedEventSent = false;
        client.NoDelay = true;
        client.SendTimeout = 5000;

        try
        {
            var networkStream = client.GetStream();
            Stream stream = networkStream;

            if (EnableTLS)
            {
                stream = new SslStream(networkStream, false);
                // Autenticar como servidor (bloquea hasta que el cliente hace el handshake TLS)
                await (stream as SslStream).AuthenticateAsServerAsync(_serverCert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false);
            }

            // Handshake antes de registrar al cliente o avisar que conectó.
            // Si falla, cerrar y chau — sin OnClientConnected, sin OnError, sin nada.
            string fpHex = null;
            if (AuthMode != AuthMode.None)
            {
                var (success, handshakeFpHex) = DoHandshake(client, stream);
                if (!success)
                {
                    try { client.Close(); } catch { }
                    return;
                }
                fpHex = handshakeFpHex;
            }

            _clients[ip] = client;

            if (EnableTLS)
                SecureStreams[client] = stream;

            OnClientConnected?.Invoke(ip, fpHex);
            connectedEventSent = true;

            if (OnClientAsync != null)
                await OnClientAsync(client, stream);
        }
        catch (Exception ex)
        {
            // EndOfStreamException, IOException, SocketException, etc. → cliente caído
            OnError?.Invoke(ex);
        }
        finally
        {
            try { client.Close(); } catch { }
            _clients.TryRemove(ip, out _);

            if (EnableTLS)
                SecureStreams.TryRemove(client, out _);

            if (connectedEventSent)
                OnClientDisconnected?.Invoke(ip);
        }
    }

    /// <summary>
    /// Ejecuta el handshake según el <see cref="AuthMode"/> configurado.
    /// </summary>
    /// <param name="client">TcpClient para setear el ReceiveTimeout durante el handshake.</param>
    /// <param name="stream">Stream ya encriptado sobre el que leer y escribir.</param>
    /// <returns>True si la autenticación fue exitosa.</returns>
    private (bool success, string fpHEx) DoHandshake(TcpClient client, Stream stream) => AuthMode switch
    {
        AuthMode.PasswordChallenge => (DoPasswordHandshake(client, stream), null),
        AuthMode.AsymmetricKeys => DoAsymmetricHandshake(client, stream),
        AuthMode.None => (true, null),
        _ => throw new NotImplementedException(),
    };

    /// <summary>
    /// Handshake challenge-response con SHA-256 y <see cref="Password"/>.
    /// <code>Wire:
    ///   1. Server → Client: 16 bytes random (challenge)
    ///   2. Client → Server: 32 bytes SHA-256(challenge + password)
    ///   3. Server valida. Si OK → manda 1 byte 0x01. Si NO OK → cierra sin mandar nada.
    /// Timeout: 5s para que el cliente mande la respuesta.
    /// </code>
    /// </summary>
    /// <param name="client">TcpClient para setear el ReceiveTimeout durante el handshake.</param>
    /// <param name="stream">Stream ya encriptado sobre el que leer y escribir.</param>
    /// <returns>True si la autenticación fue exitosa.</returns>
    private bool DoPasswordHandshake(TcpClient client, Stream stream)
    {
        try
        {
            client.ReceiveTimeout = 5000;

            // 1. Generar y mandar challenge (16 bytes random)
            Span<byte> challenge = stackalloc byte[16];
            RandomNumberGenerator.Fill(challenge);
            stream.Write(challenge);

            // 2. Leer respuesta (32 bytes SHA-256)
            Span<byte> response = stackalloc byte[32];
            stream.ReadExactly(response);

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
                throw new AuthenticationException();

            // 5. Mandar OK (1 byte) y dejar el socket abierto
            stream.WriteByte(0x01);
            client.ReceiveTimeout = 0;
            return true;
        }
        catch
        {
            client.ReceiveTimeout = 0;
            OnAuthFail?.Invoke(client);
            return false;
        }
    }

    /// <summary>
    /// Handshake asimétrico con ECDSA P-256 DER ASN.1 usando <see cref="AuthorizedPublicKeys"/>.
    /// <code> Wire:
    ///   1. Client → Server: 32 bytes (SHA-256 de la pública del cliente = fingerprint)
    ///   2. Server busca la pública en AuthorizedPublicKeys. Si no está → cierra sin mandar nada.
    ///   3. Server → Client: 32 bytes random (challenge)
    ///   4. Client → Server: [4 bytes length little-endian][firma del challenge con la privada del cliente]
    ///   5. Server valida la firma con la pública. Si OK → manda 1 byte 0x01. Si NO OK → cierra.
    /// Timeout: 5s para que el cliente mande fingerprint y firma.
    /// </code>
    /// </summary>
    /// <param name="client">TcpClient para setear el ReceiveTimeout durante el handshake.</param>
    /// <param name="stream">Stream ya encriptado sobre el que leer y escribir.</param>
    /// <returns>True si la autenticación fue exitosa.</returns>
    private (bool, string) DoAsymmetricHandshake(TcpClient client, Stream stream)
    {
        if (AuthorizedPublicKeys.Count < 1)
        {
            OnAuthFail?.Invoke(client);
            return (false, null);
        }
        try
        {
            client.ReceiveTimeout = 5000;

            // 1. Leer fingerprint del cliente (32 bytes)
            Span<byte> fingerprint = stackalloc byte[32];
            stream.ReadExactly(fingerprint);

            // 2. Resolver pública por fingerprint
            string fpHex = Convert.ToHexStringLower(fingerprint);
            if (!AuthorizedPublicKeys.TryGetValue(fpHex, out byte[] pubKeyBytes))
                throw new AuthenticationException();

            // 3. Generar y mandar challenge (32 bytes random)
            Span<byte> challenge = stackalloc byte[32];
            RandomNumberGenerator.Fill(challenge);
            stream.Write(challenge);

            // 4. Leer [4 bytes length little-endian][firma]
            Span<byte> lenBuf = stackalloc byte[4];
            stream.ReadExactly(lenBuf);
            int sigLen = BinaryPrimitives.ReadInt32LittleEndian(lenBuf);
            if (sigLen is <= 0 or > 256)
                throw new AuthenticationException();

            byte[] signature = new byte[sigLen];
            stream.ReadExactly(signature);

            // 5. Validar firma con la pública del cliente
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportSubjectPublicKeyInfo(pubKeyBytes, out _);
            bool ok = ecdsa.VerifyData(challenge, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

            if (!ok)
                throw new AuthenticationException();

            // 6. Mandar OK (1 byte)
            stream.WriteByte(0x01);
            client.ReceiveTimeout = 0;
            return (true, fpHex);
        }
        catch
        {
            client.ReceiveTimeout = 0;
            OnAuthFail?.Invoke(client);
            return (false, null);
        }
    }

    /// <summary>
    /// Genera un certificado X509 autofirmado en memoria, válido por 1 año.
    /// </summary>
    /// <returns><see cref="X509Certificate2"/> para usar con <see cref="SslStream"/>.</returns>
    private static X509Certificate2 GenerateSelfSignedCert()
    {
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest("CN=SharpUtils", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        // Válido por 1 año
        return req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    /// <summary>
    /// Cierra todas las conexiones y streams de todos los clientes, detiene el accept loop y libera los recursos.
    /// </summary>
    public void Dispose()
    {
        Stop();
        foreach (var client in _clients.Values)
        {
            client.Close();
            client.Dispose();
        }
        _clients.Clear();

        foreach (var stream in SecureStreams.Values)
        {
            stream?.Close();
            stream?.Dispose();
        }
        SecureStreams.Clear();

        _listener.Dispose();
        _cts.Dispose();
        _serverCert?.Dispose();
    }
}
