using System;
using System.Collections.Generic;
using System.IO;
using ActivityViewer.Core.Sqlite;

using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Data
{
    public sealed class InvalidActivityDatabaseException : Exception
    {
        public InvalidActivityDatabaseException(string message) : base(message) { }
    }

    public sealed class PlayerInfo
    {
        public PlayerInfo(string platformId, string name)
        {
            PlatformId = platformId;
            Name = name;
        }

        public string PlatformId { get; }

        public string Name { get; }

        public string Display => Name + " (" + PlatformId + ")";

        public override string ToString() => Display;
    }

    public sealed class ActivityDatabase : IDisposable
    {
        public const int SupportedSchemaVersion = 1;

        private readonly SqliteDatabase _db;
        private readonly object _gate = new object();

        private ActivityDatabase(string path, SqliteDatabase db)
        {
            Path = path;
            _db = db;
        }

        public string Path { get; }

        public static ActivityDatabase Open(string path)
        {
            SqliteRuntime.EnsureLoaded();
            if (!File.Exists(path)) throw new FileNotFoundException(Lang.T("db.notFound"), path);

            SqliteDatabase db;
            try
            {
                db = new SqliteDatabase(path, readOnly: true);
            }
            catch (SqliteException error)
            {
                throw new InvalidActivityDatabaseException(Lang.F("db.notSqlite", error.Message));
            }

            try
            {
                long tables;
                try
                {
                    tables = db.ScalarInt64("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_info';");
                }
                catch (SqliteException error)
                {
                    throw new InvalidActivityDatabaseException(Lang.F("db.notSqlite", error.Message));
                }

                if (tables == 0) throw new InvalidActivityDatabaseException(Lang.T("db.notActivity"));
                long version = db.ScalarInt64("SELECT version FROM schema_info LIMIT 1;");
                if (version > SupportedSchemaVersion) throw new InvalidActivityDatabaseException(Lang.T("db.newer"));
                return new ActivityDatabase(path, db);
            }
            catch
            {
                db.Dispose();
                throw;
            }
        }

        public string QuickCheck() => Run(db => db.ScalarText("PRAGMA quick_check;"));

        public IReadOnlyList<PlayerInfo> Players()
        {
            return Run(db =>
            {
                var players = new List<PlayerInfo>();
                using SqliteStatement statement = db.Prepare("SELECT platform_id, last_name FROM players ORDER BY last_name COLLATE NOCASE, platform_id;");
                while (statement.Step()) players.Add(new PlayerInfo(statement.ColumnText(0), statement.ColumnText(1)));
                return players;
            });
        }

        public IReadOnlyList<string> DistinctTargets(params string[] events)
        {
            SqlBuilder sql = new SqlBuilder("SELECT DISTINCT e.target FROM events e")
                .WhereIn("e.event", events)
                .Where("e.target <> ''")
                .Append("ORDER BY e.target COLLATE NOCASE;");
            return ReadStrings(sql);
        }

        public IReadOnlyList<string> DistinctPrefabs()
        {
            return ReadStrings(new SqlBuilder("SELECT DISTINCT prefab FROM event_items WHERE prefab <> '' ORDER BY prefab COLLATE NOCASE;"));
        }

        internal T Run<T>(Func<SqliteDatabase, T> work)
        {
            lock (_gate) return work(_db);
        }

        public void Dispose()
        {
            lock (_gate) _db.Dispose();
        }

        private IReadOnlyList<string> ReadStrings(SqlBuilder sql)
        {
            return Run(db =>
            {
                var values = new List<string>();
                using SqliteStatement statement = sql.Prepare(db);
                while (statement.Step()) values.Add(statement.ColumnText(0));
                return values;
            });
        }
    }
}
