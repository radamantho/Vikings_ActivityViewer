using System;

namespace ActivityViewer.Core.Data
{
    public sealed class QueryFilter
    {
        public static QueryFilter All { get; } = new QueryFilter();

        public string? PlatformId { get; init; }

        public DateTime? From { get; init; }

        public DateTime? To { get; init; }

        public int RowLimit { get; init; } = ResultTable.MaxRows;
    }
}
