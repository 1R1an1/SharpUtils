/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System.IO;
using System.Text.Json;

namespace SharpUtils;

/// <summary>
/// Proporciona utilidades para cargar y guardar objetos como archivos de configuración JSON.
/// </summary>
public static class JsonUtils
{
    /// <summary>
	/// Obtiene o establece las opciones de <see cref="JsonSerializerOptions"/> utilizadas
	/// para serializar y deserializar JSON.
	/// </summary>
    public static JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true
    };

    /// <summary>
    /// Carga un objeto desde un archivo JSON.
    /// Si el archivo no existe, se crea una nueva instancia de <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Tipo del objeto a cargar. Debe tener un constructor sin parámetros.</typeparam>
    /// <param name="filePath">Ruta del archivo JSON.</param>
    /// <returns>El objeto deserializado.</returns>
    public static T LoadJson<T>(string filePath) where T : new()
    {
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, JsonSerializer.Serialize(new T(), JsonOptions));

        string json = File.ReadAllText(filePath);
        var config = JsonSerializer.Deserialize<T>(json, JsonOptions);
        string json2 = JsonSerializer.Serialize(config, JsonOptions);

        if (json != json2)
            File.WriteAllText(filePath, json2);

        return config;
    }

    /// <summary>
    /// Serializa un objeto como JSON y lo guarda en un archivo.
    /// </summary>
    /// <typeparam name="T">Tipo del objeto a serializar.</typeparam>
    /// <param name="obj">Objeto que se va a serializar.</param>
    /// <param name="savePath">Ruta donde se guardará el archivo JSON.</param>
    public static void SaveJson<T>(T obj, string savePath)
    {
        string json = JsonSerializer.Serialize(obj, JsonOptions);
        File.WriteAllText(savePath, json);
    }
}
