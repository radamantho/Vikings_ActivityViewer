using System;
using System.IO;
using ActivityViewer.Core;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Check(bool condition, string label)
        {
            if (condition)
            {
                _passed++;
                Console.WriteLine("PASS " + label);
            }
            else
            {
                _failed++;
                Console.WriteLine("FAIL " + label);
            }
        }

        private static string TempDir(string name)
        {
            string path = Path.Combine(Path.GetTempPath(), "ActivityViewer_Tests", name);
            if (Directory.Exists(path)) Directory.Delete(path, true);
            Directory.CreateDirectory(path);
            return path;
        }

        private static int Main()
        {
            string root = TempDir("approot");
            AppPaths.Override(Path.Combine(root, "roaming"), Path.Combine(root, "local"));
            ActivityViewer.Core.Sqlite.SqliteRuntime.EnsureLoaded();
            Vikings_ActivityLog.SqliteNative.Load(Path.GetDirectoryName(ActivityViewer.Core.Sqlite.SqliteRuntime.LoadedFrom)!);
            RunAll();
            Console.WriteLine(_failed == 0 ? _passed + " checks passed." : _failed + " checks FAILED, " + _passed + " passed.");
            return _failed == 0 ? 0 : 1;
        }
    }
}
