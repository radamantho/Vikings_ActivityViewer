using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Vikings_ActivityLog
{
    internal static class NativeLoader
    {
        private const int RtldNow = 2;
        private const int RtldGlobal = 0x100;

        [DllImport("kernel32", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr WindowsLoad(string path);

        [DllImport("kernel32", EntryPoint = "GetProcAddress", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr WindowsSymbol(IntPtr module, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        private static extern IntPtr LinuxLoad(string path, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        private static extern IntPtr LinuxSymbol(IntPtr handle, string name);

        [DllImport("libdl.so.2", EntryPoint = "dlerror")]
        private static extern IntPtr LinuxError();

        internal static bool IsWindows => Environment.OSVersion.Platform == PlatformID.Win32NT;

        internal static string SqliteFileName => IsWindows ? "e_sqlite3.dll" : "libe_sqlite3.so";

        internal static IntPtr Load(string directory)
        {
            string path = Path.Combine(directory, SqliteFileName);
            if (!File.Exists(path)) throw new FileNotFoundException("Native SQLite library not found.", path);

            IntPtr handle = IsWindows ? WindowsLoad(path) : LinuxLoad(path, RtldNow | RtldGlobal);
            if (handle == IntPtr.Zero) throw new DllNotFoundException("Could not load " + path + ": " + LastError());
            return handle;
        }

        internal static IntPtr Symbol(IntPtr handle, string name)
        {
            IntPtr symbol = IsWindows ? WindowsSymbol(handle, name) : LinuxSymbol(handle, name);
            if (symbol == IntPtr.Zero) throw new EntryPointNotFoundException("SQLite function not found: " + name);
            return symbol;
        }

        private static string LastError()
        {
            if (IsWindows) return "Win32 error " + Marshal.GetLastWin32Error();
            IntPtr message = LinuxError();
            return message == IntPtr.Zero ? "unknown error" : Marshal.PtrToStringAnsi(message) ?? "unknown error";
        }
    }
}
