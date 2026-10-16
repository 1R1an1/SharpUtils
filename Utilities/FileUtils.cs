/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.IO;

namespace SharpUtils.Utilities
{
	/// <summary>
	/// Utilidades de archivos.
	/// </summary>
	public static class FileUtils
	{
		private enum SizeUnit { B, KB, MB, GB, TB }

		/// <summary>
		/// Calcula un tamaño de buffer óptimo para leer/escribir un archivo.
		/// </summary>
		public static int CalculateBufferSize(long fileSize)
		{
			int size = (int)(fileSize / 100);
			return Math.Clamp(size, 16 * 1024, 256 * 1024);
		}

		/// <summary>
		/// Formatea una cantidad de bytes como string legible ej: 512 B, 643 KB, 1.50 MB, 1.76 GB, 3.46 TB.
		/// </summary>
		public static string FormatFileSize(long bytes)
		{
			var (unit, value) = GetSizeUnit(bytes);
			return $"{value:F2} {unit}";
		}

		/// <summary>
		/// Formatea una velocidad en bytes/segundo como string legible, ej: 5.65 MB/s, 266 KB/s, 800 B/s.
		/// </summary>
		public static string FormatSpeed(long bytesPerSecond)
			=> $"{FormatFileSize(bytesPerSecond)}/s";

		/// <summary>
		/// Formatea un <see cref="TimeSpan"/> como string compacto, ej: "1h 5m 30s", "5m 30s", "30s"
		/// </summary>
		public static string FormatTime(TimeSpan t)
		{
			if (t.TotalHours >= 1)
				return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
			if (t.TotalMinutes >= 1)
				return $"{t.Minutes}m {t.Seconds}s";
			return $"{t.Seconds}s";
		}

		// -------------------------------------------------------------------------

		private static (SizeUnit unit, double value) GetSizeUnit(long bytes)
		{
			if (bytes >= 1024L * 1024 * 1024 * 1024) return (SizeUnit.TB, bytes / (1024.0 * 1024 * 1024 * 1024));
			if (bytes >= 1024L * 1024 * 1024) return (SizeUnit.GB, bytes / (1024.0 * 1024 * 1024));
			if (bytes >= 1024 * 1024) return (SizeUnit.MB, bytes / (1024.0 * 1024));
			if (bytes >= 1024) return (SizeUnit.KB, bytes / 1024.0);
			return (SizeUnit.B, bytes);
		}

		// -------------------------------------------------------------------------

		/// <summary>
		/// Devuelve una ruta de archivo que no existe todavía, agregando " (1)", " (2)", etc.
		/// antes de la extensión si ya existe.
		/// </summary>
		public static string GetAvailableFileName(string fullPath)
		{
			string directory = Path.GetDirectoryName(fullPath) ?? "";
			string filename = Path.GetFileNameWithoutExtension(fullPath);
			string extension = Path.GetExtension(fullPath);

			string result = fullPath;
			int counter = 1;

			while (File.Exists(result))
			{
				result = Path.Combine(directory, $"{filename} ({counter}){extension}");
				counter++;
			}

			return result;
		}

		/// <summary>
		/// Valida que <paramref name="userFileName"/> caiga dentro de <paramref name="baseFolder"/>
		/// y no escape con ".." o rutas absolutas.
		/// </summary>
		public static bool TryGetSafeFilePath(string baseFolder, string userFileName, out string safePath)
		{
			safePath = null;
			if (Path.IsPathRooted(userFileName)) return false;
			if (string.IsNullOrEmpty(userFileName)) return false;

			string baseFull = Path.GetFullPath(baseFolder); // asegura formato canónico
			string combined = Path.GetFullPath(Path.Combine(baseFull, userFileName));

			// Comparamos prefijo con barra final para evitar falsos positivos como "C:\base2"
			if (!combined.StartsWith(baseFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				return false;

			safePath = combined;
			return true;
		}
	}
}
