using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Cache
{
    public sealed class CacheStats
    {
        public CacheStats(int worlds, long bytes)
        {
            Worlds = worlds;
            Bytes = bytes;
        }

        public int Worlds { get; }

        public long Bytes { get; }
    }

    public static class CacheMaintenance
    {
        private const string CurrentFile = "current.json";

        public static IReadOnlyList<string> WorldFolders(string root)
        {
            var worlds = new List<string>();
            if (!Directory.Exists(root)) return worlds;
            foreach (string profile in Directory.GetDirectories(root))
                foreach (string world in Directory.GetDirectories(profile))
                    if (IsCopyFolder(world)) worlds.Add(world);
            return worlds;
        }

        public static CacheStats Measure(string root)
        {
            IReadOnlyList<string> worlds = WorldFolders(root);
            long bytes = worlds.Sum(world => Directory.EnumerateFiles(world, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length));
            return new CacheStats(worlds.Count, bytes);
        }

        public static void DeleteWorld(string worldFolder)
        {
            if (!Directory.Exists(worldFolder) || !IsCopyFolder(worldFolder)) return;
            Directory.Delete(worldFolder, true);
            RemoveIfEmpty(Path.GetDirectoryName(worldFolder));
        }

        public static int DeleteAll(string root)
        {
            IReadOnlyList<string> worlds = WorldFolders(root);
            foreach (string world in worlds) DeleteWorld(world);
            return worlds.Count;
        }

        public static int MoveAll(string fromRoot, string toRoot)
        {
            string from = Normalize(fromRoot);
            string to = Normalize(toRoot);
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase)) return 0;
            if (IsInside(from, to) || IsInside(to, from)) throw new ArgumentException(Lang.T("cache.nested"));

            IReadOnlyList<string> worlds = WorldFolders(from);
            foreach (string world in worlds)
            {
                string profileName = Path.GetFileName(Path.GetDirectoryName(world)!);
                string target = Path.Combine(to, profileName, Path.GetFileName(world));
                if (Directory.Exists(target)) Directory.Delete(target, true);
                CopyDirectory(world, target);
                Directory.Delete(world, true);
                RemoveIfEmpty(Path.GetDirectoryName(world));
            }
            return worlds.Count;
        }

        private static bool IsCopyFolder(string folder) =>
            File.Exists(Path.Combine(folder, CurrentFile)) || Directory.GetDirectories(folder, "copy-*").Length > 0;

        private static string Normalize(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static bool IsInside(string parent, string child) =>
            child.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (string folder in Directory.GetDirectories(source)) CopyDirectory(folder, Path.Combine(target, Path.GetFileName(folder)));
        }

        private static void RemoveIfEmpty(string? folder)
        {
            if (folder == null || !Directory.Exists(folder)) return;
            if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
    }
}
