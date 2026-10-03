using System;
using System.Collections.Generic;
using System.Linq;

namespace ActivityViewer.Core.Analysis
{
    public sealed class SpeedHit
    {
        public long FromUtcMs { get; set; }

        public long ToUtcMs { get; set; }

        public string PlatformId { get; set; } = "";

        public string PlayerName { get; set; } = "";

        public double Speed { get; set; }

        public double Distance { get; set; }

        public double Seconds { get; set; }

        public double FromX { get; set; }

        public double FromZ { get; set; }

        public double ToX { get; set; }

        public double ToZ { get; set; }
    }

    public static class SpeedAnalyzer
    {
        public const long MinElapsedMs = 1000;

        public static readonly IReadOnlyCollection<string> PlayerPositionEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dodge", "Damaged", "Drop", "Consume", "Equip", "Unequip", "Craft", "Repair item", "Ping", "Inventory",
            "TrinketActivated", "Command", "Command remote", "Move", "MoveAll", "StackAll"
        };

        public static readonly IReadOnlyCollection<string> BarrierEvents = new HashSet<string>(StringComparer.Ordinal)
        {
            "Teleport", "Spawned", "Dead", "Connected", "Disconnected"
        };

        public static List<SpeedHit> Analyze(IEnumerable<ActionPoint> points, double maxSpeed)
        {
            var hits = new List<SpeedHit>();
            string? player = null;
            ActionPoint? previous = null;

            foreach (ActionPoint point in points.OrderBy(p => p.PlatformId, StringComparer.Ordinal).ThenBy(p => p.TimeUtcMs))
            {
                if (point.PlatformId != player)
                {
                    player = point.PlatformId;
                    previous = null;
                }

                if (BarrierEvents.Contains(point.Event))
                {
                    previous = null;
                    continue;
                }

                if (!PlayerPositionEvents.Contains(point.Event)) continue;
                if (point.X == 0 && point.Y == 0 && point.Z == 0) continue;

                if (previous == null)
                {
                    previous = point;
                    continue;
                }

                long elapsed = point.TimeUtcMs - previous.TimeUtcMs;
                if (elapsed < MinElapsedMs) continue;

                double dx = point.X - previous.X;
                double dz = point.Z - previous.Z;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                double seconds = elapsed / 1000.0;
                double speed = distance / seconds;
                if (speed > maxSpeed)
                {
                    hits.Add(new SpeedHit
                    {
                        FromUtcMs = previous.TimeUtcMs,
                        ToUtcMs = point.TimeUtcMs,
                        PlatformId = point.PlatformId,
                        PlayerName = point.PlayerName,
                        Speed = Math.Round(speed, 1),
                        Distance = Math.Round(distance, 1),
                        Seconds = Math.Round(seconds, 1),
                        FromX = previous.X,
                        FromZ = previous.Z,
                        ToX = point.X,
                        ToZ = point.Z
                    });
                }

                previous = point;
            }

            hits.Sort((a, b) => a.ToUtcMs.CompareTo(b.ToUtcMs));
            return hits;
        }
    }
}
