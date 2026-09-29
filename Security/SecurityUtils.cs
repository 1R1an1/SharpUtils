/* SPDX-License-Identifier: MPL-2.0
 * Copyright (c) 2026 1R1an1 */
using System;
using System.Security.Cryptography;
using System.Text;

namespace SharpUtils.Security;

public static class SecurityUtils
{
    private static readonly byte[] Salt =
        Encoding.UTF8.GetBytes("D4taW0rm.PasswordKdf.salt");

    public static byte[] DerivePassword(ReadOnlySpan<byte> password)
        => Rfc2898DeriveBytes.Pbkdf2(password, Salt, 7000, HashAlgorithmName.SHA256, 32);

    public static byte[] DerivePassword(string password)
    => Rfc2898DeriveBytes.Pbkdf2(password, Salt, 7000, HashAlgorithmName.SHA256, 32);
}
