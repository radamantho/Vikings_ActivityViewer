using System;
using System.IO;
using ActivityViewer.Core;
using ActivityViewer.Core.Cache;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Settings;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunCacheMaintenance()
        {
            Cache_RootIsConfigurable();
            Cache_MeasureAndDeleteWorld();
            Cache_DeleteAllKeepsForeignFiles();
            Cache_MoveAllMovesOnlyCopies();
            Cache_MoveRefusesNestedFolders();
            Settings_CacheRootRoundTrip();
        }

        private static string SeedCopy(string root, string profile)
        {
            CachePaths.UseRoot(root);
            string folder = CachePaths.WorldFolder(profile, "world.db");
            DownloadResult result = Download(LiveRemote(), folder);
            if (!result.Success) throw new InvalidOperationException("seed download failed");
            return folder;
        }

        private static void Cache_RootIsConfigurable()
        {
            CachePaths.UseRoot(null);
            Check(CachePaths.Root == Path.Combine(AppPaths.LocalDataRoot, "cache"), "Cache: default root under LocalDataRoot");
            string custom = TempDir("cache_custom");
            CachePaths.UseRoot(custom);
            Check(CachePaths.ProfileFolder("Servidor").StartsWith(custom, StringComparison.OrdinalIgnoreCase), "Cache: custom root is used for new copies");
            CachePaths.UseRoot(" ");
            Check(CachePaths.Root == CachePaths.DefaultRoot, "Cache: blank root falls back to the default");
        }

        private static void Cache_MeasureAndDeleteWorld()
        {
            string root = TempDir("cache_measure");
            string first = SeedCopy(root, "Servidor A");
            SeedCopy(root, "Servidor B");
            CacheStats stats = CacheMaintenance.Measure(root);
            Check(stats.Worlds == 2 && stats.Bytes > 0, "Cache: measure counts worlds and bytes");

            CacheMaintenance.DeleteWorld(first);
            Check(!Directory.Exists(first) && CachePaths.CachedWorlds("Servidor A").Count == 0, "Cache: clear current copy removes only that world");
            Check(CacheMaintenance.Measure(root).Worlds == 1, "Cache: other worlds are kept");
            CachePaths.UseRoot(null);
        }

        private static void Cache_DeleteAllKeepsForeignFiles()
        {
            string root = TempDir("cache_deleteall");
            SeedCopy(root, "Servidor A");
            SeedCopy(root, "Servidor B");
            File.WriteAllText(Path.Combine(root, "notas.txt"), "não apagar");
            Directory.CreateDirectory(Path.Combine(root, "Fotos"));
            File.WriteAllText(Path.Combine(root, "Fotos", "foto.txt"), "não apagar");
            Directory.CreateDirectory(Path.Combine(root, "Servidor A", "outra pasta"));

            int removed = CacheMaintenance.DeleteAll(root);
            Check(removed == 2 && CacheMaintenance.Measure(root).Worlds == 0, "Cache: clear all removes every downloaded world");
            Check(File.Exists(Path.Combine(root, "notas.txt")) && File.Exists(Path.Combine(root, "Fotos", "foto.txt")), "Cache: clear all never touches foreign files");
            Check(Directory.Exists(Path.Combine(root, "Servidor A", "outra pasta")), "Cache: clear all never touches foreign folders");
            CachePaths.UseRoot(null);
        }

        private static void Cache_MoveAllMovesOnlyCopies()
        {
            string from = TempDir("cache_move_from");
            string to = TempDir("cache_move_to");
            SeedCopy(from, "Servidor A");
            File.WriteAllText(Path.Combine(from, "notas.txt"), "fica aqui");

            int moved = CacheMaintenance.MoveAll(from, to);
            Check(moved == 1 && CacheMaintenance.Measure(from).Worlds == 0 && CacheMaintenance.Measure(to).Worlds == 1, "Cache: move transfers downloaded worlds");
            Check(File.Exists(Path.Combine(from, "notas.txt")) && !File.Exists(Path.Combine(to, "notas.txt")), "Cache: move leaves foreign files where they are");

            CachePaths.UseRoot(to);
            string folder = CachePaths.WorldFolder("Servidor A", "world.db");
            CachedCopy? copy = CachedCopy.Load(folder);
            bool opens = false;
            if (copy != null)
            {
                using ActivityDatabase db = ActivityDatabase.Open(copy.DatabasePathIn(folder));
                opens = db.Players().Count == 1;
            }
            Check(opens, "Cache: moved copy opens from the new folder");
            CachePaths.UseRoot(null);
        }

        private static void Cache_MoveRefusesNestedFolders()
        {
            string root = TempDir("cache_nested");
            bool inside = false;
            try { CacheMaintenance.MoveAll(root, Path.Combine(root, "sub")); }
            catch (ArgumentException) { inside = true; }
            bool outside = false;
            try { CacheMaintenance.MoveAll(Path.Combine(root, "sub"), root); }
            catch (ArgumentException) { outside = true; }
            Check(inside && outside, "Cache: moving into or out of a nested folder is refused");
            Check(CacheMaintenance.MoveAll(root, root + Path.DirectorySeparatorChar) == 0, "Cache: moving to the same folder does nothing");
        }

        private static void Settings_CacheRootRoundTrip()
        {
            var store = new SettingsStore(Path.Combine(TempDir("settings_cache"), "settings.json"));
            store.Save(new AppSettings { Language = Language.English, CacheRoot = @"D:\Copias" });
            AppSettings loaded = store.Load(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
            Check(loaded.CacheRoot == @"D:\Copias" && loaded.Language == Language.English, "Settings: cache folder round trip");
            Check(new AppSettings().CacheRoot == "", "Settings: empty cache folder means default");
        }
    }
}
