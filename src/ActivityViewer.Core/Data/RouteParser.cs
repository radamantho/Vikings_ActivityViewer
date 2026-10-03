using System;
using ActivityViewer.Core.Localization;

namespace ActivityViewer.Core.Data
{
    public static class RouteParser
    {
        public static string Ground => Lang.T("route.ground");

        public static (string Origin, string Destination) Parse(string eventName, string target, string details)
        {
            switch (eventName)
            {
                case "Move":
                case "MoveAll":
                case "StackAll":
                    return ParseMove(details);
                case "Drop":
                    return (target, Ground);
                case "Pickup":
                    return (Ground, Lang.T("route.inventory"));
                case "Consume":
                    return (target, Lang.T("route.consumed"));
                default:
                    return ("", "");
            }
        }

        private static (string Origin, string Destination) ParseMove(string details)
        {
            if (!details.StartsWith("from:", StringComparison.Ordinal)) return ("", "");
            int to = details.IndexOf(" to:", StringComparison.Ordinal);
            if (to < 0) return (details.Substring(5), "");
            return (details.Substring(5, to - 5), details.Substring(to + 4));
        }
    }
}
