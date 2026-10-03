using System;
using System.IO;
using ActivityViewer.Core;
using ActivityViewer.Core.Data;
using ActivityViewer.Core.Export;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunExport()
        {
            Export_CsvEscapesAndUsesPtBr();
            Export_TextIsAligned();
            Export_FileHasUtf8Bom();
            ErrorLog_WritesEntry();
        }

        private static ResultTable ExportSample()
        {
            var table = new ResultTable(
                new ResultColumn("Time", typeof(DateTime)),
                new ResultColumn("Player", typeof(string)),
                new ResultColumn("Total", typeof(double)),
                new ResultColumn("Quantity", typeof(long)),
                new ResultColumn("HealthAfter", typeof(double)));
            var when = new DateTime(2026, 10, 2, 13, 5, 9, DateTimeKind.Local);
            table.Rows.Add(new object?[] { when, "Þór; \"o\" 🙂\nlinha2", 35.5, 3L, null });
            table.Rows.Add(new object?[] { when, "Bjorn", 2.0, 10L, 1.25 });
            return table;
        }

        private static void Export_CsvEscapesAndUsesPtBr()
        {
            var writer = new StringWriter { NewLine = "\r\n" };
            ResultExporter.WriteCsv(ExportSample(), writer);
            string expected =
                "Data;Jogador;Total;Quantidade;Vida após\r\n" +
                "02/10/2026 13:05:09;\"Þór; \"\"o\"\" 🙂\nlinha2\";35,5;3;\r\n" +
                "02/10/2026 13:05:09;Bjorn;2;10;1,25\r\n";
            Check(writer.ToString() == expected, "Export: CSV quotes special text, uses ';' and decimal comma");
        }

        private static void Export_TextIsAligned()
        {
            var writer = new StringWriter { NewLine = "\n" };
            ResultExporter.WriteText(ExportSample(), writer);
            string[] lines = writer.ToString().TrimEnd('\n').Split('\n');
            Check(lines.Length == 4, "Export: TXT has header, separator and one line per row");
            Check(lines[2].Contains("linha2"), "Export: TXT keeps multi-line text on one line");
            int column = lines[0].IndexOf(" | ", StringComparison.Ordinal);
            Check(column > 0 && lines[2].IndexOf(" | ", StringComparison.Ordinal) == column && lines[3].IndexOf(" | ", StringComparison.Ordinal) == column, "Export: TXT columns aligned");
        }

        private static void Export_FileHasUtf8Bom()
        {
            string path = Path.Combine(TempDir("export"), "r.csv");
            ResultExporter.Export(ExportSample(), path);
            byte[] bytes = File.ReadAllBytes(path);
            Check(bytes.Length > 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "Export: file starts with UTF-8 BOM for Excel");
        }

        private static void ErrorLog_WritesEntry()
        {
            string path = ErrorLog.Write(new InvalidOperationException("falha de teste"), "Contexto do teste");
            Check(path.StartsWith(Path.Combine(AppPaths.LocalDataRoot, "logs"), StringComparison.OrdinalIgnoreCase) && File.Exists(path), "ErrorLog: file created under logs");
            string text = File.ReadAllText(path);
            Check(text.Contains("falha de teste") && text.Contains("Contexto do teste"), "ErrorLog: entry has message and context");
        }
    }
}
