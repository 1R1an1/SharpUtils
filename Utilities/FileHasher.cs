/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.IO;
using System.Security.Cryptography;

namespace SharpUtils.Utilities
{
	/// <summary>
	/// Hashing de archivos.
	/// </summary>
	public static class FileHasher
	{
		/// <summary>
		/// Calcula el hash MD5 de un archivo.
		/// </summary>
		/// <param name="filePath">Ruta al archivo a hashear.</param>
		/// <param name="length">Bytes a hashear desde el inicio del archivo. -1 para todo el archivo.</param>
		/// <returns>El hash MD5 en hexadecimal minúsculo.</returns>
		public static string CalculateMd5(string filePath, long length = -1)
		{
			using (var md5 = MD5.Create())
			using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				byte[] buffer = new byte[FileUtils.CalculateBufferSize(fs.Length)];
				long remaining = length == -1 ? fs.Length : length;
				int read;

				while (remaining > 0 && (read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining))) > 0)
				{
					md5.TransformBlock(buffer, 0, read, null, 0);
					remaining -= read;
				}

				md5.TransformFinalBlock(buffer, 0, 0);
				return Convert.ToHexString(md5.Hash).ToLowerInvariant();
			}
		}
	}
}
