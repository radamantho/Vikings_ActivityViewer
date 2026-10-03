using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ActivityViewer.Core.Cache
{
    public static class CachePaths
    {
        public static string SafeName(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder();
            foreach (char c in value.Trim()) builder.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            return builder.Length == 0 ? "_" : builder.ToString();
        }

        private static string? _root;

        public static string DefaultRoot => Path.Combine(AppPaths.LocalDataRoot, "cache");

        public static string Root => _root ?? DefaultRoot;

        public static void UseRoot(string? root) => _root = string.IsNullOrWhiteSpace(root) ? null : root.Trim();

        public static string ProfileFolder(string profileName) => Path.Combine(Root, SafeName(profileName));

        public static string WorldFolder(string profileName, string worldFile) =>
            Path.Combine(ProfileFolder(profileName), SafeName(Path.GetFileNameWithoutExtension(worldFile)));

        public static IReadOnlyList<string> CachedWorlds(string profileName)
        {
            var worlds = new List<string>();
            string folder = ProfileFolder(profileName);
            if (!Directory.Exists(folder)) return worlds;

            foreach (string worldFolder in Directory.GetDirectories(folder))
            {
                CachedCopy? copy = CachedCopy.Load(worldFolder);
                if (copy != null) worlds.Add(copy.WorldFile);
            }
            worlds.Sort(StringComparer.OrdinalIgnoreCase);
            return worlds;
        }
    }
}
