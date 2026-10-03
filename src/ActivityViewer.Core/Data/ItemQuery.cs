using System;
using System.Collections.Generic;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public sealed class ItemCriteria
    {
        public string Prefab { get; init; } = "";

        public int MinCount { get; init; }

        public IReadOnlyCollection<string> Events { get; init; } = ItemQuery.AllEvents;
    }

    public static class ItemQuery
    {
        public static readonly IReadOnlyList<string> AllEvents = new[]
        {
            "Pickup", "Drop", "Move", "MoveAll", "StackAll", "Craft", "Equip", "Unequip", "Consume"
        };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, ItemCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Id", typeof(string)),
                new ResultColumn("Event", typeof(string)),
                new ResultColumn("Item", typeof(string)),
                new ResultColumn("Quantity", typeof(long)),
                new ResultColumn("Quality", typeof(long)),
                new ResultColumn("Crafter", typeof(string)),
                new ResultColumn("Origin", typeof(string)),
                new ResultColumn("Destination", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));
            if (criteria.Events.Count == 0) return table;

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, i.prefab, i.count, i.quality, i.crafter_name, " +
                "e.target, e.details, e.x, e.y, e.z FROM event_items i JOIN events e ON e.id = i.event_id");
            sql.WhereIn("e.event", criteria.Events);
            if (criteria.MinCount > 0) sql.Where("i.count >= ?", (long)criteria.MinCount);
            if (criteria.Prefab.Length > 0) sql.Where("instr(lower(i.prefab), lower(?)) > 0", criteria.Prefab);
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id, i.rowid");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        private static object?[] Map(SqliteStatement s)
        {
            string eventName = s.ColumnText(3);
            (string origin, string destination) = RouteParser.Parse(eventName, s.ColumnText(8), s.ColumnText(9));
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                eventName,
                s.ColumnText(4),
                s.ColumnInt64(5),
                s.ColumnInt64(6),
                s.ColumnText(7),
                origin,
                destination,
                s.ColumnDouble(10),
                s.ColumnDouble(11),
                s.ColumnDouble(12)
            };
        }
    }
}
