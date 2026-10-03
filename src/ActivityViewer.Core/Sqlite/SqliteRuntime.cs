using System;
using System.IO;
using System.Security.Cryptography;

using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Sqlite
{
    internal static class SqliteRuntime
    {
        private const string ResourceName = "e_sqlite3.dll";
        private static readonly object Gate = new object();

        internal static string? LoadedFrom { get; private set; }

        internal static void EnsureLoaded()
        {
            lock (Gate)
            {
                if (SqliteNative.IsLoaded) return;
                string path = Extract(AppPaths.LocalDataRoot);
                SqliteNative.Load(path);
                LoadedFrom = path;
            }
        }

        internal static string Extract(string root)
        {
            byte[] bytes = ReadResource();
            string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            string directory = Path.Combine(root, "native", hash);
            string path = Path.Combine(directory, ResourceName);
            if (File.Exists(path) && new FileInfo(path).Length == bytes.Length) return path;

            Directory.CreateDirectory(directory);
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, bytes);
            try
            {
                File.Move(temp, path, true);
            }
            catch (IOException) when (File.Exists(path))
            {
                File.Delete(temp);
            }
            catch (UnauthorizedAccessException) when (File.Exists(path))
            {
                File.Delete(temp);
            }
            return path;
        }

        private static byte[] ReadResource()
        {
            using Stream? stream = typeof(SqliteRuntime).Assembly.GetManifestResourceStream(ResourceName);
            if (stream == null) throw new InvalidOperationException(Lang.T("sqlite.missingResource"));
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }
    }
}
