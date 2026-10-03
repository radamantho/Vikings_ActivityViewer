using System;
using System.Globalization;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core
{
    public static class InputParser
    {
        private static readonly string[] PortugueseDateTimeFormats = { "d/M/yyyy H:mm:ss", "d/M/yyyy H:mm" };
        private static readonly string[] PortugueseDateFormats = { "d/M/yyyy" };
        private static readonly string[] EnglishDateTimeFormats = { "yyyy-M-d H:mm:ss", "yyyy-M-d H:mm" };
        private static readonly string[] EnglishDateFormats = { "yyyy-M-d" };

        private static bool English => Lang.Current == Language.English;

        public static string DateHint => English ? "yyyy-MM-dd [HH:mm]" : "dd/MM/aaaa [HH:mm]";

        public static bool TryParseNumber(string text, out double value)
        {
            value = 0;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;
            if (English && trimmed.Contains(',')) return false;
            string normalized = English ? trimmed : trimmed.Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseInt(string text, out int value)
        {
            value = 0;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;
            return int.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryParseStart(string text, out DateTime? value) => TryParseDate(text, false, out value);

        public static bool TryParseEnd(string text, out DateTime? value) => TryParseDate(text, true, out value);

        private static bool TryParseDate(string text, bool endOfDay, out DateTime? value)
        {
            value = null;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return true;

            string[] dateTimeFormats = English ? EnglishDateTimeFormats : PortugueseDateTimeFormats;
            string[] dateFormats = English ? EnglishDateFormats : PortugueseDateFormats;

            if (DateTime.TryParseExact(trimmed, dateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime full))
            {
                value = DateTime.SpecifyKind(full, DateTimeKind.Local);
                return true;
            }

            if (DateTime.TryParseExact(trimmed, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime day))
            {
                DateTime local = DateTime.SpecifyKind(day, DateTimeKind.Local);
                value = endOfDay ? local.AddDays(1).AddMilliseconds(-1) : local;
                return true;
            }

            return false;
        }
    }
}
