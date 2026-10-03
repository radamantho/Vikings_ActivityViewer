using System;
using System.IO;
using ActivityViewer.Core;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunSqlite()
        {
            Sqlite_RuntimeExtractsAndLoads();
            Sqlite_ExtractIsIdempotent();
            Sqlite_ReadOnlyReadsTypesAndRefusesWrites();
        }

        private static void Sqlite_RuntimeExtractsAndLoads()
        {
            SqliteRuntime.EnsureLoaded();
            Check(SqliteNative.IsLoaded, "Native SQLite is loaded");
            string loaded = SqliteRuntime.LoadedFrom ?? "";
            Check(File.Exists(loaded), "Extracted library exists");
            Check(loaded.StartsWith(Path.Combine(AppPaths.LocalDataRoot, "native"), StringComparison.OrdinalIgnoreCase), "Library extracted under LocalDataRoot\\native");
        }

        private static void Sqlite_ExtractIsIdempotent()
        {
            string first = SqliteRuntime.Extract(AppPaths.LocalDataRoot);
            string second = SqliteRuntime.Extract(AppPaths.LocalDataRoot);
            Check(first == second, "Extract returns the same path twice");
            Check(new FileInfo(first).Length == 1759232, "Extracted library has the expected size");
        }

        private static void Sqlite_ReadOnlyReadsTypesAndRefusesWrites()
        {
            string path = Path.Combine(TempDir("sqlite_ro"), "t.db");
            using (var writer = new SqliteDatabase(path, readOnly: false))
            {
                writer.Execute("CREATE TABLE t (i INTEGER, d REAL, n REAL, s TEXT);");
                writer.Execute("INSERT INTO t VALUES (7, 2.5, NULL, 'Þór 🙂');");
            }

            using var reader = new SqliteDatabase(path, readOnly: true);
            using (SqliteStatement statement = reader.Prepare("SELECT i, d, n, s FROM t WHERE i = ?;"))
            {
                statement.Bind(1, (object)7L);
                Check(statement.Step(), "Row found with object binding");
                Check(statement.ColumnInt64(0) == 7, "Integer column read");
                Check(Math.Abs(statement.ColumnDouble(1) - 2.5) < 1e-9, "Double column read");
                Check(statement.ColumnIsNull(2), "Null column detected");
                Check(!statement.ColumnIsNull(1), "Non-null column not reported as null");
                Check(statement.ColumnText(3) == "Þór 🙂", "UTF-8 text read");
            }

            bool refused = false;
            try { reader.Execute("INSERT INTO t VALUES (1, 1, 1, 'x');"); }
            catch (SqliteException) { refused = true; }
            Check(refused, "Read-only connection refuses writes");
        }
    }
}
