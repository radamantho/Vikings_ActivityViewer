using System.Collections.Generic;
using System.Text;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal sealed class SqlBuilder
    {
        private readonly StringBuilder _sql;
        private readonly List<object> _values = new List<object>();
        private bool _hasWhere;

        internal SqlBuilder(string select)
        {
            _sql = new StringBuilder(select);
        }

        internal string Sql => _sql.ToString();

        internal SqlBuilder Where(string condition, params object[] values)
        {
            _sql.Append(_hasWhere ? " AND (" : " WHERE (").Append(condition).Append(')');
            _hasWhere = true;
            _values.AddRange(values);
            return this;
        }

        internal SqlBuilder WhereIn(string column, IEnumerable<string> values)
        {
            var list = new List<object>(values);
            var marks = new StringBuilder();
            for (int i = 0; i < list.Count; i++) marks.Append(i == 0 ? "?" : ", ?");
            return Where(column + " IN (" + marks + ")", list.ToArray());
        }

        internal SqlBuilder ApplyFilter(QueryFilter filter, string alias)
        {
            if (!string.IsNullOrEmpty(filter.PlatformId)) Where(alias + ".platform_id = ?", filter.PlatformId);
            if (filter.From.HasValue) Where(alias + ".time_utc >= ?", TimeFormat.ToUtcMs(filter.From.Value));
            if (filter.To.HasValue) Where(alias + ".time_utc <= ?", TimeFormat.ToUtcMs(filter.To.Value));
            return this;
        }

        internal SqlBuilder Append(string tail)
        {
            _sql.Append(' ').Append(tail);
            return this;
        }

        internal SqliteStatement Prepare(SqliteDatabase database)
        {
            SqliteStatement statement = database.Prepare(_sql.ToString());
            for (int i = 0; i < _values.Count; i++) statement.Bind(i + 1, _values[i]);
            return statement;
        }
    }
}
