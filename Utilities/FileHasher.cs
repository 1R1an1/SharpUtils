using System;
using System.IO;
using System.Security.Cryptography;

namespace SharpUtils.Utilities
{
	public static class FileHasher
	{
		public static string CalculateMd5(string filePath, long length = -1)
		{
			using (var md5 = MD5.Create())
			using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				byte[] buffer = new byte[256 * 1024];
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
