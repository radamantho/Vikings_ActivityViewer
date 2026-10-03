using System.Collections.Generic;
using System.Threading;
using ActivityViewer.Core.Analysis;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal static class PointReader
    {
        internal static List<ActionPoint> Read(ActivityDatabase database, QueryFilter filter, IEnumerable<string> events, CancellationToken token)
        {
            SqlBuilder sql = new SqlBuilder("SELECT e.time_utc, e.platform_id, e.player_name, e.event, e.target, e.x, e.y, e.z FROM events e")
                .WhereIn("e.event", events)
                .ApplyFilter(filter, "e")
                .Append("ORDER BY e.platform_id, e.time_utc, e.id");

            return database.Run(db =>
            {
                var points = new List<ActionPoint>();
                using SqliteStatement s = sql.Prepare(db);
                while (s.Step())
                {
                    if ((points.Count & 4095) == 0) token.ThrowIfCancellationRequested();
                    points.Add(new ActionPoint(s.ColumnInt64(0), s.ColumnText(1), s.ColumnText(2), s.ColumnText(3), s.ColumnText(4),
                        s.ColumnDouble(5), s.ColumnDouble(6), s.ColumnDouble(7)));
                }
                return points;
            });
        }
    }
}
