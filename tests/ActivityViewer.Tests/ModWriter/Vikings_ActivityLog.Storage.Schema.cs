namespace Vikings_ActivityLog
{
    internal static class ActivitySchema
    {
        internal const int Version = 2;
        internal const int WalCheckpointPages = 256;
        internal const long WalSizeLimitBytes = 1048576;

        private const string CreateSql =
            "CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);" +
            "INSERT INTO schema_info (version) SELECT 2 WHERE NOT EXISTS (SELECT 1 FROM schema_info);" +
            "CREATE TABLE IF NOT EXISTS players (" +
            "platform_id TEXT PRIMARY KEY, player_id INTEGER NOT NULL DEFAULT 0, last_name TEXT NOT NULL DEFAULT ''," +
            "first_seen_utc INTEGER NOT NULL, last_seen_utc INTEGER NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS characters (" +
            "id INTEGER PRIMARY KEY, platform_id TEXT NOT NULL, player_id INTEGER NOT NULL, name TEXT NOT NULL," +
            "UNIQUE (platform_id, player_id, name));" +
            "CREATE TABLE IF NOT EXISTS event_types (id INTEGER PRIMARY KEY, name TEXT NOT NULL UNIQUE);" +
            "CREATE TABLE IF NOT EXISTS activity (" +
            "id INTEGER PRIMARY KEY, time_utc INTEGER NOT NULL, character INTEGER NOT NULL, event INTEGER NOT NULL," +
            "x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, z REAL NOT NULL DEFAULT 0," +
            "target TEXT NOT NULL DEFAULT '', amount INTEGER NOT NULL DEFAULT 0, details TEXT NOT NULL DEFAULT '');" +
            "CREATE INDEX IF NOT EXISTS ix_activity_time ON activity (time_utc);" +
            "CREATE INDEX IF NOT EXISTS ix_activity_character_time ON activity (character, time_utc);" +
            "CREATE INDEX IF NOT EXISTS ix_activity_event_time ON activity (event, time_utc);" +
            "CREATE VIEW IF NOT EXISTS events AS SELECT a.id AS id, a.time_utc AS time_utc, c.platform_id AS platform_id," +
            "c.name AS player_name, c.player_id AS player_id, t.name AS event, a.x AS x, a.y AS y, a.z AS z," +
            "a.target AS target, a.amount AS amount, a.details AS details " +
            "FROM activity a JOIN characters c ON c.id = a.character JOIN event_types t ON t.id = a.event;" +
            "CREATE TABLE IF NOT EXISTS event_items (" +
            "event_id INTEGER NOT NULL, source TEXT NOT NULL DEFAULT '', prefab TEXT NOT NULL," +
            "count INTEGER NOT NULL, quality INTEGER NOT NULL, crafter_id INTEGER NOT NULL DEFAULT 0," +
            "crafter_name TEXT NOT NULL DEFAULT '', custom_data TEXT NOT NULL DEFAULT '');" +
            "CREATE INDEX IF NOT EXISTS ix_event_items_event ON event_items (event_id);" +
            "CREATE INDEX IF NOT EXISTS ix_event_items_prefab ON event_items (prefab);" +
            "CREATE TABLE IF NOT EXISTS event_damage (" +
            "event_id INTEGER PRIMARY KEY, damage REAL NOT NULL, blunt REAL NOT NULL, slash REAL NOT NULL," +
            "pierce REAL NOT NULL, fire REAL NOT NULL, frost REAL NOT NULL, lightning REAL NOT NULL," +
            "poison REAL NOT NULL, spirit REAL NOT NULL, chop REAL NOT NULL, pickaxe REAL NOT NULL," +
            "total REAL NOT NULL, health_after REAL);";

        internal static void Apply(SqliteDatabase database)
        {
            database.Execute("PRAGMA journal_mode=WAL;");
            database.Execute("PRAGMA synchronous=NORMAL;");
            database.Execute("PRAGMA wal_autocheckpoint=" + WalCheckpointPages + ";");
            database.Execute("PRAGMA journal_size_limit=" + WalSizeLimitBytes + ";");
            RejectOlderSchema(database);
            database.Execute(CreateSql);
        }

        private static void RejectOlderSchema(SqliteDatabase database)
        {
            if (database.ScalarInt64("SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'schema_info';") == 0) return;
            long version = database.ScalarInt64("SELECT COALESCE(MAX(version), 0) FROM schema_info;");
            if (version != Version)
                throw new SqliteException("database schema version " + version + " is not supported (expected " + Version +
                    "); move the old database file away so a new one is created.");
        }
    }
}
