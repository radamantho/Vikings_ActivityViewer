using System;
using System.Diagnostics;
using System.Threading;
using ActivityViewer.Core.Sqlite;

namespace ActivityViewer.Core.Data
{
    internal static class QueryReader
    {
        internal static ResultTable Read(ActivityDatabase database, SqlBuilder sql, ResultTable table,
            Func<SqliteStatement, object?[]> map, int limit, CancellationToken token)
        {
            Stopwatch watch = Stopwatch.StartNew();
            database.Run(db =>
            {
                using SqliteStatement statement = sql.Prepare(db);
                while (statement.Step())
                {
                    if ((table.Rows.Count & 1023) == 0) token.ThrowIfCancellationRequested();
                    if (table.Rows.Count >= limit)
                    {
                        table.Truncated = true;
                        break;
                    }
                    table.Rows.Add(map(statement));
                }
                return 0;
            });
            table.Elapsed = watch.Elapsed;
            return table;
        }
    }
}
