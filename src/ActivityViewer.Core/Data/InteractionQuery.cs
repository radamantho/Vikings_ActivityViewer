using System;
using System.Threading;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public enum InteractionResult
    {
        Any,
        Success,
        Failure
    }

    public sealed class InteractionCriteria
    {
        public string Object { get; init; } = "";

        public InteractionResult Result { get; init; } = InteractionResult.Any;
    }

    public static class InteractionQuery
    {
        public static readonly string[] Events = { "Interact", "Use", "Text" };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, InteractionCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Id", typeof(string)),
                new ResultColumn("Event", typeof(string)),
                new ResultColumn("Object", typeof(string)),
                new ResultColumn("Result", typeof(string)),
                new ResultColumn("Info", typeof(string)),
                new ResultColumn("UsedItem", typeof(string)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, e.target, e.details, " +
                "(SELECT i.prefab FROM event_items i WHERE i.event_id = e.id LIMIT 1), e.x, e.y, e.z FROM events e");
            sql.WhereIn("e.event", Events);
            if (criteria.Object.Length > 0) sql.Where("instr(lower(e.target), lower(?)) > 0", criteria.Object);
            if (criteria.Result != InteractionResult.Any)
            {
                string prefix = criteria.Result == InteractionResult.Success ? "result:True" : "result:False";
                sql.Where("e.event = 'Interact' AND substr(e.details, 1, length(?)) = ?", prefix, prefix);
            }
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        public static (string Result, string Info) ParseDetails(string eventName, string details)
        {
            if (eventName == "Text") return ("", details);
            if (eventName != "Interact" || !details.StartsWith("result:", StringComparison.Ordinal)) return ("", "");

            int space = details.IndexOf(' ');
            string value = space < 0 ? details.Substring(7) : details.Substring(7, space - 7);
            string result = value == "True" ? Lang.T("result.success") : value == "False" ? Lang.T("result.failure") : value;
            int info = details.IndexOf(" info:", StringComparison.Ordinal);
            return (result, info < 0 ? "" : details.Substring(info + 6));
        }

        private static object?[] Map(SqliteStatement s)
        {
            string eventName = s.ColumnText(3);
            (string result, string info) = ParseDetails(eventName, s.ColumnText(5));
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                eventName,
                s.ColumnText(4),
                result,
                info,
                s.ColumnIsNull(6) ? "" : s.ColumnText(6),
                s.ColumnDouble(7),
                s.ColumnDouble(8),
                s.ColumnDouble(9)
            };
        }
    }
}
