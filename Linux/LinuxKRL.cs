using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpUtils.Linux;

public static class LinuxKRL
{
    private const int AT_FDCWD = -100;
    private const int R_OK = 4;
    private const int AT_SYMLINK_NOFOLLOW = 0x100;
    private const int S_IFMT = 0xF000;
    private const int S_IFREG = 0x8000;
    private const byte DT_DIR = 4;
    private const byte DT_REG = 8;

    private const int OFF_D_RECLEN = 16;
    private const int OFF_D_TYPE = 18;
    private const int OFF_D_NAME = 19;


    [StructLayout(LayoutKind.Sequential)]
    public struct Stat
    {
        public ulong st_dev;
        public ulong st_ino;
        public ulong st_nlink;
        public uint st_mode;
        public uint st_uid;
        public uint st_gid;
        public uint pad0;
        public ulong st_rdev;
        public long st_size;
        public long st_blksize;
        public long st_blocks;
        public long atime_sec;
        public long atime_nsec;
        public long mtime_sec;
        public long mtime_nsec;
        public long ctime_sec;
        public long ctime_nsec;
        public long reserved0;
        public long reserved1;
        public long reserved2;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int faccessat(int dirfd, string pathname, int mode, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int lstat(string pathname, out Stat statbuf);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr opendir(string name);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr readdir(IntPtr dirp);

    [DllImport("libc", SetLastError = true)]
    private static extern int closedir(IntPtr dirp);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct Dirent
    {
        public ulong d_ino;
        public long d_off;
        public ushort d_reclen;
        public byte d_type;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
        public string d_name;
    }

    public static bool CanReadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (faccessat(AT_FDCWD, path, R_OK, AT_SYMLINK_NOFOLLOW) != 0)
            return false;

        if (lstat(path, out Stat st) != 0)
            return false;

        return (st.st_mode & S_IFMT) == S_IFREG;
    }

    public static IReadOnlyList<string> GetReadableFiles(string dir)
    {
        var list = new List<string>();
        Walk(dir, list);
        return list;
    }


    private static string ReadName(IntPtr ep)
    {
        int reclen = Marshal.ReadByte(ep, OFF_D_RECLEN) | (Marshal.ReadByte(ep, OFF_D_RECLEN + 1) << 8);
        int maxLen = reclen - OFF_D_NAME;
        if (maxLen <= 0)
            return string.Empty;

        byte[] buf = new byte[maxLen];
        Marshal.Copy(ep + OFF_D_NAME, buf, 0, maxLen);

        int len = Array.IndexOf(buf, (byte)0);
        return Encoding.UTF8.GetString(buf, 0, len < 0 ? maxLen : len);
    }

    private static void Walk(string dir, List<string> list)
    {
        IntPtr dp = opendir(dir);
        if (dp == IntPtr.Zero)
            return;

        string prefix = dir.EndsWith("/") ? dir : dir + "/";

        try
        {
            IntPtr ep;
            while ((ep = readdir(dp)) != IntPtr.Zero)
            {
                byte dType = Marshal.ReadByte(ep, OFF_D_TYPE);
                if (dType != DT_DIR && dType != DT_REG)
                    continue;

                var name = ReadName(ep);
                if (name == "." || name == "..")
                    continue;

                string full = prefix + name;

                if (dType == DT_DIR)
                    Walk(full, list);
                else if (CanReadFile(full))
                    list.Add(full);
            }
        }
        finally
        {
            closedir(dp);
        }
    }
}
