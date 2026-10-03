using System;
using ActivityViewer.Core;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunInput()
        {
            Input_ParsesBrazilianFormats();
        }

        private static void Input_ParsesBrazilianFormats()
        {
            Check(InputParser.TryParseNumber("2,5", out double comma) && comma == 2.5, "Input: decimal comma");
            Check(InputParser.TryParseNumber("2.5", out double dot) && dot == 2.5, "Input: decimal point");
            Check(InputParser.TryParseNumber("  ", out double empty) && empty == 0, "Input: empty number is zero");
            Check(!InputParser.TryParseNumber("abc", out _), "Input: text is not a number");
            Check(!InputParser.TryParseNumber("-3", out _), "Input: negative numbers refused");
            Check(InputParser.TryParseInt("7", out int seven) && seven == 7, "Input: integer");
            Check(!InputParser.TryParseInt("7,5", out _), "Input: decimal refused where an integer is expected");

            Check(InputParser.TryParseStart("01/10/2026 14:30", out DateTime? start) && start == new DateTime(2026, 10, 1, 14, 30, 0) && start.Value.Kind == DateTimeKind.Local, "Input: date and time");
            Check(InputParser.TryParseStart("1/10/2026", out DateTime? day) && day == new DateTime(2026, 10, 1), "Input: date without time starts at midnight");
            Check(InputParser.TryParseStart("", out DateTime? none) && none == null, "Input: empty date means no limit");
            Check(!InputParser.TryParseStart("31/02/2026", out _), "Input: impossible date refused");
            Check(InputParser.TryParseEnd("01/10/2026", out DateTime? end) && end == new DateTime(2026, 10, 1, 23, 59, 59, 999), "Input: 'Até' with a date only covers the whole day");
            Check(InputParser.TryParseEnd("01/10/2026 10:00", out DateTime? endTime) && endTime == new DateTime(2026, 10, 1, 10, 0, 0), "Input: 'Até' with a time is exact");
        }
    }
}
