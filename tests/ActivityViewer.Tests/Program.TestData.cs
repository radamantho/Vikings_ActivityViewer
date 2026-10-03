using System.Collections.Generic;
using System.IO;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        private const long T0 = 1767225600000;

        private static Mod.ActivityRecord Rec(Mod.ActivityEventType type, long offsetMs, string platform = "Steam_1", string name = "Ragnar",
            float x = 10f, float y = 30f, float z = 10f, string target = "", int amount = 0, string details = "")
        {
            return new Mod.ActivityRecord
            {
                Type = type,
                TimeUtcMs = T0 + offsetMs,
                PlatformId = platform,
                PlayerName = name,
                PlayerId = platform == "Steam_1" ? 101 : 202,
                X = x,
                Y = y,
                Z = z,
                Target = target,
                Amount = amount,
                Details = details
            };
        }

        private static Mod.ActivityItem Item(string prefab, int count, int quality = 1, string source = "", long crafterId = 0, string crafterName = "")
        {
            return new Mod.ActivityItem { Prefab = prefab, Count = count, Quality = quality, Source = source, CrafterId = crafterId, CrafterName = crafterName };
        }

        private static string BuildDb(string name, params Mod.ActivityRecord[] records)
        {
            string path = Path.Combine(TempDir("db_" + name), name + ".db");
            using (var store = new Mod.ActivityStore(path))
            {
                if (records.Length > 0) store.WriteBatch(new List<Mod.ActivityRecord>(records));
            }
            return path;
        }
    }
}
