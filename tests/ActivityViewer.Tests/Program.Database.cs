using System;
using System.Collections.Generic;
using System.IO;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Sqlite;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunDatabase()
        {
            Database_OpensModDatabaseAndListsPlayers();
            Database_RefusesNewerSchema();
            Database_RefusesOtherFiles();
            Database_ReadsWalCopyWithoutShm();
            Database_Suggestions();
            Database_TimeFormatRoundTrip();
            Database_SqlBuilderAppliesFilter();
        }

        private static void Database_OpensModDatabaseAndListsPlayers()
        {
            string path = BuildDb("players",
                Rec(Mod.ActivityEventType.Ping, 0, "Steam_2", "Bjorn", amount: 40),
                Rec(Mod.ActivityEventType.Ping, 1000, "Steam_1", "Ragnar", amount: 50));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            IReadOnlyList<PlayerInfo> players = db.Players();
            Check(players.Count == 2, "Two players listed");
            Check(players[0].Name == "Bjorn" && players[1].Name == "Ragnar", "Players sorted by name");
            Check(players[1].Display == "Ragnar (Steam_1)", "Player display shows name and platform id");
            Check(db.QuickCheck() == "ok", "Quick check is ok");
        }

        private static void Database_RefusesNewerSchema()
        {
            string path = BuildDb("newer");
            using (var writer = new SqliteDatabase(path, readOnly: false)) writer.Execute("UPDATE schema_info SET version = 2;");
            string message = "";
            try { using ActivityDatabase db = ActivityDatabase.Open(path); }
            catch (InvalidActivityDatabaseException error) { message = error.Message; }
            Check(message == "Banco de uma versão mais nova do mod. Atualize o Viewer.", "Newer schema refused with the update message");
        }

        private static void Database_RefusesOtherFiles()
        {
            string dir = TempDir("otherfiles");
            string sqlite = Path.Combine(dir, "other.db");
            using (var writer = new SqliteDatabase(sqlite, readOnly: false)) writer.Execute("CREATE TABLE x (a INTEGER);");
            string text = Path.Combine(dir, "text.db");
            File.WriteAllText(text, "isto não é um banco de dados, apenas texto comum para o teste");

            string first = "";
            try { using ActivityDatabase db = ActivityDatabase.Open(sqlite); }
            catch (InvalidActivityDatabaseException error) { first = error.Message; }
            Check(first == "O arquivo não é um banco do Vikings_ActivityLog.", "SQLite file without schema_info refused");

            bool second = false;
            try { using ActivityDatabase db = ActivityDatabase.Open(text); }
            catch (InvalidActivityDatabaseException) { second = true; }
            Check(second, "Non-SQLite file refused with InvalidActivityDatabaseException");

            bool missing = false;
            try { using ActivityDatabase db = ActivityDatabase.Open(Path.Combine(dir, "missing.db")); }
            catch (FileNotFoundException) { missing = true; }
            Check(missing, "Missing file reported as FileNotFoundException");
        }

        private static void Database_ReadsWalCopyWithoutShm()
        {
            string dir = TempDir("walcopy");
            string source = Path.Combine(dir, "live.db");
            string copyDir = Path.Combine(dir, "copy");
            Directory.CreateDirectory(copyDir);
            string copy = Path.Combine(copyDir, "live.db");

            using (var store = new Mod.ActivityStore(source))
            {
                store.WriteBatch(new List<Mod.ActivityRecord> { Rec(Mod.ActivityEventType.Ping, 0, amount: 33) });
                CopyShared(source, copy);
                CopyShared(source + "-wal", copy + "-wal");
            }

            Check(!File.Exists(copy + "-shm"), "Copy starts without -shm");
            using ActivityDatabase db = ActivityDatabase.Open(copy);
            Check(db.Players().Count == 1, "Rows that exist only in the WAL are visible in the copy");
            Check(db.QuickCheck() == "ok", "WAL copy passes quick check");
        }

        private static void CopyShared(string from, string to)
        {
            using var input = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using FileStream output = File.Create(to);
            input.CopyTo(output);
        }

        private static void Database_Suggestions()
        {
            Mod.ActivityRecord damage = Rec(Mod.ActivityEventType.Damage, 0, target: "Boar");
            damage.Damage = new Mod.ActivityDamage { Slash = 5f, Total = 5f };
            Mod.ActivityRecord pickup = Rec(Mod.ActivityEventType.Pickup, 10);
            pickup.Items.Add(Item("Wood", 10));
            pickup.Items.Add(Item("Stone", 2));
            string path = BuildDb("suggest", damage, pickup, Rec(Mod.ActivityEventType.Interact, 20, target: "piece_chest", details: "result:True"));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            IReadOnlyList<string> prefabs = db.DistinctPrefabs();
            Check(prefabs.Count == 2 && prefabs[0] == "Stone" && prefabs[1] == "Wood", "Distinct prefabs sorted");
            IReadOnlyList<string> targets = db.DistinctTargets("Damage");
            Check(targets.Count == 1 && targets[0] == "Boar", "Distinct damage targets");
            Check(db.DistinctTargets("Interact", "Use", "Text").Count == 1, "Distinct interaction targets");
        }

        private static void Database_TimeFormatRoundTrip()
        {
            DateTime local = TimeFormat.ToLocal(T0);
            Check(local.Kind == DateTimeKind.Local, "ToLocal returns local time");
            Check(TimeFormat.ToUtcMs(local) == T0, "Local time converts back to the same UTC ms");
        }

        private static void Database_SqlBuilderAppliesFilter()
        {
            var filter = new QueryFilter { PlatformId = "Steam_1", From = TimeFormat.ToLocal(T0), To = TimeFormat.ToLocal(T0 + 5000) };
            SqlBuilder sql = new SqlBuilder("SELECT e.id FROM events e").Where("e.amount >= ?", 3L).ApplyFilter(filter, "e").Append("ORDER BY e.id");
            Check(sql.Sql == "SELECT e.id FROM events e WHERE (e.amount >= ?) AND (e.platform_id = ?) AND (e.time_utc >= ?) AND (e.time_utc <= ?) ORDER BY e.id", "SQL assembled with filter conditions");

            string path = BuildDb("builder",
                Rec(Mod.ActivityEventType.Ping, 0, amount: 5),
                Rec(Mod.ActivityEventType.Ping, 1000, amount: 1),
                Rec(Mod.ActivityEventType.Ping, 9000, amount: 5),
                Rec(Mod.ActivityEventType.Ping, 2000, "Steam_2", "Bjorn", amount: 5));
            using ActivityDatabase db = ActivityDatabase.Open(path);
            var table = new ResultTable(new ResultColumn("Id", typeof(long)));
            QueryReader.Read(db, sql, table, s => new object?[] { s.ColumnInt64(0) }, ResultTable.MaxRows, default);
            Check(table.Rows.Count == 1, "Filter keeps only matching player, period and amount");
        }
    }
}
