using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Export
{
    public static class ResultExporter
    {
        private const int MaxTextWidth = 60;
        private const string TextSeparator = " | ";
        private static readonly char[] PortugueseCsvSpecial = { ';', '"', '\n', '\r' };
        private static readonly char[] EnglishCsvSpecial = { ',', '"', '\n', '\r' };

        private static string CsvSeparator => Lang.Current == Language.English ? "," : ";";

        public static void Export(ResultTable table, string path)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            if (string.Equals(Path.GetExtension(path), ".csv", StringComparison.OrdinalIgnoreCase)) WriteCsv(table, writer);
            else WriteText(table, writer);
        }

        public static string FormatValue(object? value)
        {
            switch (value)
            {
                case null: return "";
                case DateTime date: return date.ToString(TimeFormat.Pattern, CultureInfo.InvariantCulture);
                case double number: return number.ToString("0.##", Lang.Culture);
                case long integer: return integer.ToString(CultureInfo.InvariantCulture);
                default: return value.ToString() ?? "";
            }
        }

        public static void WriteCsv(ResultTable table, TextWriter writer)
        {
            string separator = CsvSeparator;
            writer.WriteLine(string.Join(separator, table.Columns.Select(c => Escape(c.Header))));
            foreach (object?[] row in table.Rows)
                writer.WriteLine(string.Join(separator, row.Select(v => Escape(FormatValue(v)))));
        }

        public static void WriteText(ResultTable table, TextWriter writer)
        {
            int count = table.Columns.Count;
            var widths = new int[count];
            for (int i = 0; i < count; i++) widths[i] = Math.Min(MaxTextWidth, table.Columns[i].Header.Length);
            foreach (object?[] row in table.Rows)
                for (int i = 0; i < count; i++) widths[i] = Math.Max(widths[i], Cell(row[i]).Length);

            writer.WriteLine(Line(table.Columns.Select(c => Clip(c.Header)).ToArray(), widths));
            writer.WriteLine(new string('-', widths.Sum() + TextSeparator.Length * (count - 1)));
            foreach (object?[] row in table.Rows)
                writer.WriteLine(Line(row.Select(Cell).ToArray(), widths));
        }

        private static string Escape(string value)
        {
            if (value.IndexOfAny(Lang.Current == Language.English ? EnglishCsvSpecial : PortugueseCsvSpecial) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static string Cell(object? value) => Clip(FormatValue(value).Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' '));

        private static string Clip(string value) => value.Length <= MaxTextWidth ? value : value.Substring(0, MaxTextWidth - 3) + "...";

        private static string Line(string[] cells, int[] widths)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) builder.Append(TextSeparator);
                builder.Append(i == cells.Length - 1 ? cells[i] : cells[i].PadRight(widths[i]));
            }
            return builder.ToString();
        }
    }
}
