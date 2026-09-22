using System;
using System.IO;

namespace SharpUtils.Utilities
{
	public static class FileUtils
	{
		// Unidades de tamaño — privado porque solo se usa internamente
		private enum SizeUnit { B, KB, MB, GB }

		public static int CalculateBufferSize(long fileSize)
		{
			int size = (int)(fileSize / 100);
			return Clamp(size, 16 * 1024, 256 * 1024);
		}

		public static string FormatFileSize(long bytes)
		{
			var (unit, value) = GetSizeUnit(bytes);
			return $"{value:F2} {unit}";
		}

		public static string FormatSpeed(double bytesPerSecond)
		{
			if (bytesPerSecond >= 1024 * 1024)
				return $"{bytesPerSecond / (1024.0 * 1024):F2} MB/s";
			return $"{bytesPerSecond / 1024.0:F2} KB/s";
		}

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
			if (bytes >= 1024L * 1024 * 1024) return (SizeUnit.GB, bytes / (1024.0 * 1024 * 1024));
			if (bytes >= 1024 * 1024) return (SizeUnit.MB, bytes / (1024.0 * 1024));
			if (bytes >= 1024) return (SizeUnit.KB, bytes / 1024.0);
			return (SizeUnit.B, bytes);
		}

		private static int Clamp(int value, int min, int max)
		{
			if (value < min) return min;
			if (value > max) return max;
			return value;
		}

		// -------------------------------------------------------------------------

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
