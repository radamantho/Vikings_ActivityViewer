using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using ActivityViewer.Core.Analysis;

namespace ActivityViewer.Core.Data
{
    public static class FrequencyQuery
    {
        private const int MaxObjectsShown = 10;

        public static readonly IReadOnlyList<string> Events = new[] { "Pickup", "Place", "Remove", "Interact", "Drop", "Move" };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, int maxPerSecond, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Id", typeof(string)),
                new ResultColumn("Actions", typeof(long)),
                new ResultColumn("Events", typeof(string)),
                new ResultColumn("Objects", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            List<ActionPoint> points = PointReader.Read(database, filter, Events, token);
            token.ThrowIfCancellationRequested();
            foreach (FrequencyHit hit in FrequencyAnalyzer.Analyze(points, maxPerSecond))
            {
                if (table.Rows.Count >= filter.RowLimit)
                {
                    table.Truncated = true;
                    break;
                }

                string objects = string.Join(", ", hit.Objects.Take(MaxObjectsShown)) + (hit.Objects.Count > MaxObjectsShown ? ", ..." : "");
                table.Rows.Add(new object?[]
                {
                    TimeFormat.ToLocal(hit.StartUtcMs), hit.PlayerName, hit.PlatformId, (long)hit.MaxCount,
                    string.Join(", ", hit.Events), objects, hit.X, hit.Y, hit.Z
                });
            }

            table.Elapsed = watch.Elapsed;
            return table;
        }
    }
}
