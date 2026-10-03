using System;
using System.Collections.Generic;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Data
{
    public sealed class ResultColumn
    {
        public ResultColumn(string key, Type type)
        {
            Key = key;
            Type = type;
        }

        public string Key { get; }

        public string Header => HeaderOf(Key);

        public Type Type { get; }

        public static string HeaderOf(string key) => Lang.T("col." + key);
    }

    public sealed class ResultTable
    {
        public const int MaxRows = 200000;

        public ResultTable(params ResultColumn[] columns)
        {
            Columns = columns;
        }

        public IReadOnlyList<ResultColumn> Columns { get; }

        public List<object?[]> Rows { get; } = new List<object?[]>();

        public bool Truncated { get; set; }

        public TimeSpan Elapsed { get; set; }

        public int IndexOf(string name)
        {
            for (int i = 0; i < Columns.Count; i++)
                if (Columns[i].Key == name) return i;
            return -1;
        }

        public object? Value(int row, string column) => Rows[row][IndexOf(column)];
    }
}
