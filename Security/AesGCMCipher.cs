using System;
using System.Security.Cryptography;
using System.Text;

namespace SharpUtils.Security;

/// <summary>
/// Cifrado AES-GCM. Provee confidencialidad e integridad.
/// Le pasás un string (contraseña) y un array de bytes (datos),
/// y te devuelve los datos encriptados con su sello de seguridad.
/// </summary>
public class AesGCMCipher
{
    /// <summary>
    /// Encripta datos usando AES-GCM y una contraseña.
    /// </summary>
    public static byte[] Encrypt(byte[] password, byte[] datos)
    {
        Span<byte> llave = stackalloc byte[32];
        SHA256.HashData(password, llave);

        // Creamos el array final completo: [Nonce (12)] + [Tag (16)] + [Datos]
        byte[] resultado = new byte[12 + 16 + datos.Length];

        // Cortamos "rebanadas" (Slices) del array final para usarlas como recipientes
        Span<byte> nonce = resultado.AsSpan(0, 12);
        Span<byte> tag = resultado.AsSpan(12, 16);
        Span<byte> datosEncriptados = resultado.AsSpan(28);

        // Llenamos el Nonce con números aleatorios directamente en el array final
        RandomNumberGenerator.Fill(nonce);

        // Encriptamos DIRECTO en el array final
        using var aes = new AesGcm(llave, 16);
        aes.Encrypt(nonce, datos, datosEncriptados, tag);

        return resultado;
    }

    /// <summary>
    /// Encripta datos usando AES-GCM y una contraseña de texto.
    /// </summary>
    public static byte[] Encrypt(string password, byte[] datos)
        => Encrypt(Encoding.UTF8.GetBytes(password), datos);

    /// <summary>
    /// Desencripta los datos verificando el sello (Tag). 
    /// Lanza excepción si la contraseña es incorrecta o si alteraron los datos.
    /// </summary>
    public static byte[] Decrypt(byte[] password, byte[] paqueteEncriptado)
    {
        // Llave en el Stack
        Span<byte> llave = stackalloc byte[32];
        SHA256.HashData(password, llave);

        // Leemos las rebanadas directamente del paquete que nos mandaron (sin copiar a otro array)
        ReadOnlySpan<byte> nonce = paqueteEncriptado.AsSpan(0, 12);
        ReadOnlySpan<byte> tag = paqueteEncriptado.AsSpan(12, 16);
        ReadOnlySpan<byte> datosEncriptados = paqueteEncriptado.AsSpan(28);

        // Único array nuevo que pedimos a la memoria: el de los datos desencriptados
        byte[] datosDesencriptados = new byte[datosEncriptados.Length];

        using var aes = new AesGcm(llave, 16);
        aes.Decrypt(nonce, datosEncriptados, tag, datosDesencriptados);

        return datosDesencriptados;
    }

    /// <summary>
    /// Desencripta los datos verificando el sello (Tag). 
    /// Lanza excepción si la contraseña es incorrecta o si alteraron los datos.
    /// </summary>
    public static byte[] Decrypt(string password, byte[] paqueteEncriptado)
        => Decrypt(Encoding.UTF8.GetBytes(password), paqueteEncriptado);
}
