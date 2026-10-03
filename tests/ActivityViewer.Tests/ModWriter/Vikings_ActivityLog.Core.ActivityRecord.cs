using System.Collections.Generic;

namespace Vikings_ActivityLog
{
    internal sealed class ActivityItem
    {
        public string Source = "";
        public string Prefab = "";
        public int Count;
        public int Quality;
        public long CrafterId;
        public string CrafterName = "";
        public string CustomData = "";
    }

    internal sealed class ActivityDamage
    {
        public float Damage;
        public float Blunt;
        public float Slash;
        public float Pierce;
        public float Fire;
        public float Frost;
        public float Lightning;
        public float Poison;
        public float Spirit;
        public float Chop;
        public float Pickaxe;
        public float Total;
        public float HealthAfter = float.NaN;
    }

    internal sealed class ActivityRecord
    {
        public ActivityEventType Type;
        public double WorldTime;
        public long TimeUtcMs;
        public string PlatformId = "";
        public string PlayerName = "";
        public long PlayerId;
        public float X;
        public float Y;
        public float Z;
        public string Target = "";
        public int Amount;
        public string Details = "";
        public List<ActivityItem> Items = new List<ActivityItem>();
        public ActivityDamage? Damage;
    }
}
