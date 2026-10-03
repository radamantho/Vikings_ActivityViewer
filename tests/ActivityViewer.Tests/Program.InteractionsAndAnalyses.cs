using System.Collections.Generic;
using ActivityViewer.Core.Analysis;
using ActivityViewer.Core.Data;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunInteractionsAndAnalyses()
        {
            Interactions_FiltersAndParsing();
            Frequency_Limits();
            Frequency_QueryReadsDatabase();
            Speed_Rules();
            Speed_QueryReadsDatabase();
        }

        private static void Interactions_FiltersAndParsing()
        {
            Mod.ActivityRecord use = Rec(Mod.ActivityEventType.Use, 2000, target: "fire_pit");
            use.Items.Add(Item("Wood", 1));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("interactions",
                Rec(Mod.ActivityEventType.Interact, 0, target: "piece_chest_wood", details: "result:True"),
                Rec(Mod.ActivityEventType.Interact, 1000, target: "door", details: "result:False info:owner:Bjorn active:True"),
                use,
                Rec(Mod.ActivityEventType.Text, 3000, target: "sign", details: "Olá; \"mundo\""),
                Rec(Mod.ActivityEventType.Ping, 4000, amount: 20)));

            ResultTable all = InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria(), default);
            Check(all.Rows.Count == 4, "Interactions: Interact, Use and Text only");
            Check((string?)all.Value(0, "Result") == "Sucesso", "Interactions: success result parsed");
            Check((string?)all.Value(1, "Result") == "Falha" && (string?)all.Value(1, "Info") == "owner:Bjorn active:True", "Interactions: failure and info parsed");
            Check((string?)all.Value(2, "UsedItem") == "Wood", "Interactions: used item shown for Use");
            Check((string?)all.Value(3, "Info") == "Olá; \"mundo\"", "Interactions: text shown for Text");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Object = "CHEST" }, default).Rows.Count == 1, "Interactions: object filter");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Result = InteractionResult.Success }, default).Rows.Count == 1, "Interactions: success filter");
            Check(InteractionQuery.Run(db, QueryFilter.All, new InteractionCriteria { Result = InteractionResult.Failure }, default).Rows.Count == 1, "Interactions: failure filter");
            Check(InteractionQuery.ParseDetails("Interact", "algo inesperado") == ("", ""), "Interactions: unexpected details give empty result");
        }

        private static ActionPoint P(long ms, string player = "A", string eventName = "Pickup", string target = "Wood", double x = 10, double z = 10)
        {
            return new ActionPoint(T0 + ms, player, player == "A" ? "Ragnar" : "Bjorn", eventName, target, x, 30, z);
        }

        private static List<ActionPoint> Burst(long start, int count, long step, string player = "A")
        {
            var points = new List<ActionPoint>();
            for (int i = 0; i < count; i++) points.Add(P(start + i * step, player));
            return points;
        }

        private static void Frequency_Limits()
        {
            Check(FrequencyAnalyzer.Analyze(Burst(0, 7, 100), 7).Count == 0, "Frequency: exactly the limit is not reported");

            List<FrequencyHit> eight = FrequencyAnalyzer.Analyze(Burst(0, 8, 100), 7);
            Check(eight.Count == 1 && eight[0].MaxCount == 8 && eight[0].StartUtcMs == T0, "Frequency: one above the limit is reported once");

            List<ActionPoint> edge = Burst(0, 7, 100);
            edge.Add(P(1000));
            Check(FrequencyAnalyzer.Analyze(edge, 7).Count == 0, "Frequency: events 1000 ms apart are not in the same window");

            List<FrequencyHit> long20 = FrequencyAnalyzer.Analyze(Burst(0, 20, 100), 7);
            Check(long20.Count == 1 && long20[0].MaxCount == 10, "Frequency: a continuous burst is merged into one report");

            List<ActionPoint> two = Burst(0, 8, 100);
            two.AddRange(Burst(10000, 8, 100));
            Check(FrequencyAnalyzer.Analyze(two, 7).Count == 2, "Frequency: separate bursts are separate reports");

            List<ActionPoint> mixed = Burst(0, 5, 100, "A");
            mixed.AddRange(Burst(50, 5, 100, "B"));
            Check(FrequencyAnalyzer.Analyze(mixed, 7).Count == 0, "Frequency: players are counted separately");

            List<ActionPoint> kinds = Burst(0, 7, 100);
            kinds.Add(P(700, eventName: "Place", target: "piece_wall"));
            List<FrequencyHit> kindHits = FrequencyAnalyzer.Analyze(kinds, 7);
            Check(kindHits.Count == 1 && kindHits[0].Events.Contains("Place") && kindHits[0].Objects.Contains("piece_wall"), "Frequency: report lists events and objects");
        }

        private static void Frequency_QueryReadsDatabase()
        {
            var records = new List<Mod.ActivityRecord>();
            for (int i = 0; i < 8; i++) records.Add(Rec(Mod.ActivityEventType.Pickup, i * 100, target: ""));
            records.Add(Rec(Mod.ActivityEventType.Ping, 50, amount: 30));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("frequency", records.ToArray()));
            ResultTable table = FrequencyQuery.Run(db, QueryFilter.All, 7, default);
            Check(table.Rows.Count == 1 && (long?)table.Value(0, "Actions") == 8, "Frequency query: one report with eight actions");
        }

        private static void Speed_Rules()
        {
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(60000, eventName: "Ping", x: 110) }, 150).Count == 0, "Speed: normal movement is not reported");

            List<SpeedHit> fast = SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(2000, eventName: "Dodge", x: 1010) }, 150);
            Check(fast.Count == 1 && fast[0].Speed == 500 && fast[0].Distance == 1000 && fast[0].Seconds == 2, "Speed: impossible movement is reported");

            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(1000, eventName: "Teleport"), P(2000, eventName: "Dodge", x: 1010) }, 150).Count == 0, "Speed: teleport breaks the pair");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(500, eventName: "Dead"), P(2000, eventName: "Ping", x: 1010) }, 150).Count == 0, "Speed: death breaks the pair");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(300, eventName: "Dodge", x: 5000), P(3000, eventName: "Ping", x: 40) }, 150).Count == 0, "Speed: pairs under one second are not measured");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, eventName: "Ping"), P(1500, eventName: "Place", x: 5000), P(3000, eventName: "Ping", x: 40) }, 150).Count == 0, "Speed: target-position events are ignored");
            var zero = new ActionPoint(T0, "A", "Ragnar", "Ping", "", 0, 0, 0);
            Check(SpeedAnalyzer.Analyze(new[] { zero, P(2000, eventName: "Ping", x: 1000) }, 150).Count == 0, "Speed: zero positions are ignored");
            Check(SpeedAnalyzer.Analyze(new[] { P(0, "A", "Ping"), P(2000, "B", "Ping", x: 5000) }, 150).Count == 0, "Speed: different players are never paired");
        }

        private static void Speed_QueryReadsDatabase()
        {
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("speed",
                Rec(Mod.ActivityEventType.Ping, 0, amount: 30),
                Rec(Mod.ActivityEventType.Dodge, 2000, x: 1010f)));
            ResultTable table = SpeedQuery.Run(db, QueryFilter.All, 150, default);
            Check(table.Rows.Count == 1 && (double?)table.Value(0, "Speed") == 500.0, "Speed query: one report at 500 u/s");
            Check((string?)table.Value(0, "From") == "10, 10" && (string?)table.Value(0, "To") == "1010, 10", "Speed query: from and to positions (X, Z)");
        }
    }
}
