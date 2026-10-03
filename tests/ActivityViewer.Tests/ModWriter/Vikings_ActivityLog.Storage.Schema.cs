namespace Vikings_ActivityLog
{
    internal static class ActivitySchema
    {
        internal const int Version = 1;
        internal const int WalCheckpointPages = 256;
        internal const long WalSizeLimitBytes = 1048576;

        private const string CreateSql =
            "CREATE TABLE IF NOT EXISTS schema_info (version INTEGER NOT NULL);" +
            "INSERT INTO schema_info (version) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM schema_info);" +
            "CREATE TABLE IF NOT EXISTS players (" +
            "platform_id TEXT PRIMARY KEY, player_id INTEGER NOT NULL DEFAULT 0, last_name TEXT NOT NULL DEFAULT ''," +
            "first_seen_utc INTEGER NOT NULL, last_seen_utc INTEGER NOT NULL);" +
            "CREATE TABLE IF NOT EXISTS events (" +
            "id INTEGER PRIMARY KEY, time_utc INTEGER NOT NULL, platform_id TEXT NOT NULL," +
            "player_name TEXT NOT NULL DEFAULT '', player_id INTEGER NOT NULL DEFAULT 0, event TEXT NOT NULL," +
            "x REAL NOT NULL DEFAULT 0, y REAL NOT NULL DEFAULT 0, z REAL NOT NULL DEFAULT 0," +
            "target TEXT NOT NULL DEFAULT '', amount INTEGER NOT NULL DEFAULT 0, details TEXT NOT NULL DEFAULT '');" +
            "CREATE INDEX IF NOT EXISTS ix_events_time ON events (time_utc);" +
            "CREATE INDEX IF NOT EXISTS ix_events_player_time ON events (platform_id, time_utc);" +
            "CREATE INDEX IF NOT EXISTS ix_events_event_time ON events (event, time_utc);" +
            "CREATE INDEX IF NOT EXISTS ix_events_position ON events (x, z);" +
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
            database.Execute(CreateSql);
        }
    }
}
