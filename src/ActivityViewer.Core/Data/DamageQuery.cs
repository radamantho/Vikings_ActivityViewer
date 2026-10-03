using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using ActivityViewer.Core.Localization;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    public sealed class DamageCriteria
    {
        public double MinTotal { get; init; }

        public string Target { get; init; } = "";

        public string Attacker { get; init; } = "";
    }

    public static class DamageQuery
    {
        private const int FirstTypeColumn = 6;
        private const int HealthColumn = 17;

        private static readonly string[] TypeKeys =
        {
            "damage.damage", "damage.blunt", "damage.slash", "damage.pierce", "damage.fire", "damage.frost",
            "damage.lightning", "damage.poison", "damage.spirit", "damage.chop", "damage.pickaxe"
        };

        public static ResultTable Run(ActivityDatabase database, QueryFilter filter, DamageCriteria criteria, CancellationToken token)
        {
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Id", typeof(string)),
                new ResultColumn("Event", typeof(string)),
                new ResultColumn("Target", typeof(string)),
                new ResultColumn("Total", typeof(double)),
                new ResultColumn("Types", typeof(string)),
                new ResultColumn("HealthAfter", typeof(double)),
                new ResultColumn("X", typeof(double)),
                new ResultColumn("Y", typeof(double)),
                new ResultColumn("Z", typeof(double)));

            var sql = new SqlBuilder(
                "SELECT e.time_utc, e.player_name, e.platform_id, e.event, e.target, d.total, " +
                "d.damage, d.blunt, d.slash, d.pierce, d.fire, d.frost, d.lightning, d.poison, d.spirit, d.chop, d.pickaxe, " +
                "d.health_after, e.x, e.y, e.z FROM events e JOIN event_damage d ON d.event_id = e.id");
            AddEventCondition(sql, criteria);
            if (criteria.MinTotal > 0) sql.Where("d.total >= ?", criteria.MinTotal);
            sql.ApplyFilter(filter, "e").Append("ORDER BY e.time_utc, e.id");

            return QueryReader.Read(database, sql, table, Map, filter.RowLimit, token);
        }

        private static void AddEventCondition(SqlBuilder sql, DamageCriteria criteria)
        {
            bool hasTarget = criteria.Target.Length > 0;
            bool hasAttacker = criteria.Attacker.Length > 0;
            var parts = new List<string>();
            var values = new List<object>();

            if (hasTarget || !hasAttacker)
            {
                if (hasTarget)
                {
                    parts.Add("(e.event = 'Damage' AND instr(lower(e.target), lower(?)) > 0)");
                    values.Add(criteria.Target);
                }
                else parts.Add("e.event = 'Damage'");
            }

            if (hasAttacker || !hasTarget)
            {
                if (hasAttacker)
                {
                    parts.Add("(e.event = 'Damaged' AND instr(lower(e.target), lower(?)) > 0)");
                    values.Add(criteria.Attacker);
                }
                else parts.Add("e.event = 'Damaged'");
            }

            sql.Where(string.Join(" OR ", parts), values.ToArray());
        }

        private static object?[] Map(SqliteStatement s)
        {
            return new object?[]
            {
                TimeFormat.ToLocal(s.ColumnInt64(0)),
                s.ColumnText(1),
                s.ColumnText(2),
                s.ColumnText(3),
                s.ColumnText(4),
                s.ColumnDouble(5),
                Types(s),
                s.ColumnIsNull(HealthColumn) ? null : (object)s.ColumnDouble(HealthColumn),
                s.ColumnDouble(18),
                s.ColumnDouble(19),
                s.ColumnDouble(20)
            };
        }

        private static string Types(SqliteStatement s)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < TypeKeys.Length; i++)
            {
                double value = s.ColumnDouble(FirstTypeColumn + i);
                if (value == 0) continue;
                if (builder.Length > 0) builder.Append(" · ");
                builder.Append(Lang.T(TypeKeys[i])).Append(' ').Append(value.ToString("0.##", Lang.Culture));
            }
            return builder.ToString();
        }
    }
}
