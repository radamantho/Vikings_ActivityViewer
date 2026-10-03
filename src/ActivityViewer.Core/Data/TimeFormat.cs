using System;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Data
{
    public static class TimeFormat
    {
        public static string Pattern => Lang.Current == Language.English ? "yyyy-MM-dd HH:mm:ss" : "dd/MM/yyyy HH:mm:ss";

        public static DateTime ToLocal(long utcMs) => DateTimeOffset.FromUnixTimeMilliseconds(utcMs).LocalDateTime;

        public static long ToUtcMs(DateTime local) =>
            new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeMilliseconds();
    }
}
