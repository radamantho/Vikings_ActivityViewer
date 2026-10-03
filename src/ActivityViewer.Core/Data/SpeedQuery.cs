using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using ActivityViewer.Core.Analysis;

namespace ActivityViewer.Core.Data
{
    public static class SpeedQuery
    {
        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, double maxSpeed, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Id", typeof(string)),
                new ResultColumn("Speed", typeof(double)),
                new ResultColumn("Distance", typeof(double)),
                new ResultColumn("Seconds", typeof(double)),
                new ResultColumn("From", typeof(string)),
                new ResultColumn("To", typeof(string)));

            IEnumerable<string> events = SpeedAnalyzer.PlayerPositionEvents.Concat(SpeedAnalyzer.BarrierEvents);
            List<ActionPoint> points = PointReader.Read(database, filter, events, token);
            token.ThrowIfCancellationRequested();
            foreach (SpeedHit hit in SpeedAnalyzer.Analyze(points, maxSpeed))
            {
                if (table.Rows.Count >= filter.RowLimit)
                {
                    table.Truncated = true;
                    break;
                }

                table.Rows.Add(new object?[]
                {
                    TimeFormat.ToLocal(hit.ToUtcMs), hit.PlayerName, hit.PlatformId, hit.Speed, hit.Distance, hit.Seconds,
                    Position(hit.FromX, hit.FromZ), Position(hit.ToX, hit.ToZ)
                });
            }

            table.Elapsed = watch.Elapsed;
            return table;
        }

        private static string Position(double x, double z) => string.Format(CultureInfo.InvariantCulture, "{0:0.#}, {1:0.#}", x, z);
    }
}
