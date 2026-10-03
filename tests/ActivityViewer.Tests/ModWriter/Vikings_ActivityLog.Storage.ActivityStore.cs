using System;
using System.Collections.Generic;
using System.IO;

namespace Vikings_ActivityLog
{
    internal sealed class ActivityStore : IDisposable
    {
        private const string InsertEventSql =
            "INSERT INTO events (time_utc, platform_id, player_name, player_id, event, x, y, z, target, amount, details) " +
            "VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11);";

        private const string InsertItemSql =
            "INSERT INTO event_items (event_id, source, prefab, count, quality, crafter_id, crafter_name, custom_data) " +
            "VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8);";

        private const string InsertDamageSql =
            "INSERT INTO event_damage (event_id, damage, blunt, slash, pierce, fire, frost, lightning, poison, spirit, chop, pickaxe, total, health_after) " +
            "VALUES (?1, ?2, ?3, ?4, ?5, ?6, ?7, ?8, ?9, ?10, ?11, ?12, ?13, ?14);";

        private const string UpsertPlayerSql =
            "INSERT INTO players (platform_id, player_id, last_name, first_seen_utc, last_seen_utc) VALUES (?1, ?2, ?3, ?4, ?4) " +
            "ON CONFLICT(platform_id) DO UPDATE SET " +
            "player_id = CASE WHEN excluded.player_id <> 0 THEN excluded.player_id ELSE players.player_id END, " +
            "last_name = CASE WHEN excluded.last_name <> '' THEN excluded.last_name ELSE players.last_name END, " +
            "last_seen_utc = MAX(players.last_seen_utc, excluded.last_seen_utc);";

        private readonly SqliteStatement _insertEvent;
        private readonly SqliteStatement _insertItem;
        private readonly SqliteStatement _insertDamage;
        private readonly SqliteStatement _upsertPlayer;

        internal ActivityStore(string path)
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            Database = new SqliteDatabase(path);
            ActivitySchema.Apply(Database);
            _insertEvent = Database.Prepare(InsertEventSql);
            _insertItem = Database.Prepare(InsertItemSql);
            _insertDamage = Database.Prepare(InsertDamageSql);
            _upsertPlayer = Database.Prepare(UpsertPlayerSql);
        }

        internal SqliteDatabase Database { get; }

        internal void WriteBatch(IReadOnlyList<ActivityRecord> records)
        {
            if (records.Count == 0) return;

            Database.Execute("BEGIN IMMEDIATE;");
            try
            {
                foreach (ActivityRecord record in records)
                {
                    long eventId = InsertEvent(record);
                    foreach (ActivityItem item in record.Items) InsertItem(eventId, item);
                    if (record.Damage != null) InsertDamage(eventId, record.Damage);
                    UpsertPlayer(record);
                }

                Database.Execute("COMMIT;");
            }
            catch
            {
                ResetStatements();
                Database.Execute("ROLLBACK;");
                throw;
            }
        }

        internal int DeleteOlderThan(long cutoffUtcMs)
        {
            Database.Execute("BEGIN IMMEDIATE;");
            try
            {
                RunDelete("DELETE FROM event_items WHERE event_id IN (SELECT id FROM events WHERE time_utc < ?1);", cutoffUtcMs);
                RunDelete("DELETE FROM event_damage WHERE event_id IN (SELECT id FROM events WHERE time_utc < ?1);", cutoffUtcMs);
                int deleted = RunDelete("DELETE FROM events WHERE time_utc < ?1;", cutoffUtcMs);
                Database.Execute("COMMIT;");
                return deleted;
            }
            catch
            {
                Database.Execute("ROLLBACK;");
                throw;
            }
        }

        public void Dispose()
        {
            _insertEvent.Dispose();
            _insertItem.Dispose();
            _insertDamage.Dispose();
            _upsertPlayer.Dispose();
            Database.Dispose();
        }

        private long InsertEvent(ActivityRecord record)
        {
            _insertEvent.Bind(1, record.TimeUtcMs);
            _insertEvent.Bind(2, record.PlatformId);
            _insertEvent.Bind(3, record.PlayerName);
            _insertEvent.Bind(4, record.PlayerId);
            _insertEvent.Bind(5, ActivityEventNames.Name(record.Type));
            _insertEvent.Bind(6, (double)record.X);
            _insertEvent.Bind(7, (double)record.Y);
            _insertEvent.Bind(8, (double)record.Z);
            _insertEvent.Bind(9, record.Target);
            _insertEvent.Bind(10, (long)record.Amount);
            _insertEvent.Bind(11, record.Details);
            _insertEvent.Step();
            _insertEvent.Reset();
            return Database.LastInsertRowId;
        }

        private void InsertItem(long eventId, ActivityItem item)
        {
            _insertItem.Bind(1, eventId);
            _insertItem.Bind(2, item.Source);
            _insertItem.Bind(3, item.Prefab);
            _insertItem.Bind(4, (long)item.Count);
            _insertItem.Bind(5, (long)item.Quality);
            _insertItem.Bind(6, item.CrafterId);
            _insertItem.Bind(7, item.CrafterName);
            _insertItem.Bind(8, item.CustomData);
            _insertItem.Step();
            _insertItem.Reset();
        }

        private void InsertDamage(long eventId, ActivityDamage damage)
        {
            _insertDamage.Bind(1, eventId);
            _insertDamage.Bind(2, (double)damage.Damage);
            _insertDamage.Bind(3, (double)damage.Blunt);
            _insertDamage.Bind(4, (double)damage.Slash);
            _insertDamage.Bind(5, (double)damage.Pierce);
            _insertDamage.Bind(6, (double)damage.Fire);
            _insertDamage.Bind(7, (double)damage.Frost);
            _insertDamage.Bind(8, (double)damage.Lightning);
            _insertDamage.Bind(9, (double)damage.Poison);
            _insertDamage.Bind(10, (double)damage.Spirit);
            _insertDamage.Bind(11, (double)damage.Chop);
            _insertDamage.Bind(12, (double)damage.Pickaxe);
            _insertDamage.Bind(13, (double)damage.Total);
            if (float.IsNaN(damage.HealthAfter)) _insertDamage.Bind(14, (string?)null);
            else _insertDamage.Bind(14, (double)damage.HealthAfter);
            _insertDamage.Step();
            _insertDamage.Reset();
        }

        private void UpsertPlayer(ActivityRecord record)
        {
            _upsertPlayer.Bind(1, record.PlatformId);
            _upsertPlayer.Bind(2, record.PlayerId);
            _upsertPlayer.Bind(3, record.PlayerName);
            _upsertPlayer.Bind(4, record.TimeUtcMs);
            _upsertPlayer.Step();
            _upsertPlayer.Reset();
        }

        private int RunDelete(string sql, long cutoffUtcMs)
        {
            using SqliteStatement statement = Database.Prepare(sql);
            statement.Bind(1, cutoffUtcMs);
            statement.Step();
            return Database.Changes;
        }

        private void ResetStatements()
        {
            _insertEvent.Reset();
            _insertItem.Reset();
            _insertDamage.Reset();
            _upsertPlayer.Reset();
        }
    }
}
