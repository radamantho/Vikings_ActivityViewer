using System;
using System.Collections.Generic;
using System.Linq;

namespace ActivityViewer.Core.Analysis
{
    public sealed class FrequencyHit
    {
        public long StartUtcMs { get; set; }

        public long EndUtcMs { get; set; }

        public string PlatformId { get; set; } = "";

        public string PlayerName { get; set; } = "";

        public int MaxCount { get; set; }

        public SortedSet<string> Events { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public SortedSet<string> Objects { get; } = new SortedSet<string>(StringComparer.Ordinal);

        public double X { get; set; }

        public double Y { get; set; }

        public double Z { get; set; }
    }

    public static class FrequencyAnalyzer
    {
        public const long WindowMs = 1000;

        public static List<FrequencyHit> Analyze(IEnumerable<ActionPoint> points, int maxPerSecond)
        {
            var hits = new List<FrequencyHit>();
            var window = new Queue<ActionPoint>();
            string? player = null;
            FrequencyHit? open = null;

            foreach (ActionPoint point in points.OrderBy(p => p.PlatformId, StringComparer.Ordinal).ThenBy(p => p.TimeUtcMs))
            {
                if (point.PlatformId != player)
                {
                    player = point.PlatformId;
                    window.Clear();
                    open = null;
                }

                window.Enqueue(point);
                while (point.TimeUtcMs - window.Peek().TimeUtcMs >= WindowMs) window.Dequeue();
                if (window.Count <= maxPerSecond) continue;

                long start = window.Peek().TimeUtcMs;
                if (open == null || start > open.EndUtcMs)
                {
                    open = new FrequencyHit
                    {
                        StartUtcMs = start,
                        PlatformId = point.PlatformId,
                        PlayerName = point.PlayerName,
                        X = point.X,
                        Y = point.Y,
                        Z = point.Z
                    };
                    hits.Add(open);
                }

                open.EndUtcMs = point.TimeUtcMs;
                open.MaxCount = Math.Max(open.MaxCount, window.Count);
                foreach (ActionPoint item in window)
                {
                    open.Events.Add(item.Event);
                    if (item.Target.Length > 0) open.Objects.Add(item.Target);
                }
            }

            hits.Sort((a, b) => a.StartUtcMs.CompareTo(b.StartUtcMs));
            return hits;
        }
    }
}
