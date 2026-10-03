namespace ActivityViewer.Core.Analysis
{
    public sealed class ActionPoint
    {
        public ActionPoint(long timeUtcMs, string platformId, string playerName, string eventName, string target, double x, double y, double z)
        {
            TimeUtcMs = timeUtcMs;
            PlatformId = platformId;
            PlayerName = playerName;
            Event = eventName;
            Target = target;
            X = x;
            Y = y;
            Z = z;
        }

        public long TimeUtcMs { get; }

        public string PlatformId { get; }

        public string PlayerName { get; }

        public string Event { get; }

        public string Target { get; }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }
    }
}
