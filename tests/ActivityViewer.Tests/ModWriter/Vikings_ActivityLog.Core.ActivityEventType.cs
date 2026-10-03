namespace Vikings_ActivityLog
{
    internal enum ActivityEventType : byte
    {
        Spawned = 1,
        Inventory,
        Equip,
        Unequip,
        Consume,
        Craft,
        RepairItem,
        Dodge,
        Teleport,
        Damage,
        Damaged,
        Dead,
        Pickup,
        Drop,
        Place,
        Remove,
        RepairBuilding,
        Interact,
        Use,
        Text,
        StackAll,
        MoveAll,
        Move,
        Grave,
        Ping,
        TrinketActivated,
        Connected,
        Disconnected,
        Command,
        CommandRemote
    }

    internal static class ActivityEventNames
    {
        internal static readonly ActivityEventType[] All =
        {
            ActivityEventType.Spawned, ActivityEventType.Inventory, ActivityEventType.Equip, ActivityEventType.Unequip,
            ActivityEventType.Consume, ActivityEventType.Craft, ActivityEventType.RepairItem, ActivityEventType.Dodge,
            ActivityEventType.Teleport, ActivityEventType.Damage, ActivityEventType.Damaged, ActivityEventType.Dead,
            ActivityEventType.Pickup, ActivityEventType.Drop, ActivityEventType.Place, ActivityEventType.Remove,
            ActivityEventType.RepairBuilding, ActivityEventType.Interact, ActivityEventType.Use, ActivityEventType.Text,
            ActivityEventType.StackAll, ActivityEventType.MoveAll, ActivityEventType.Move, ActivityEventType.Grave,
            ActivityEventType.Ping, ActivityEventType.TrinketActivated, ActivityEventType.Connected,
            ActivityEventType.Disconnected, ActivityEventType.Command, ActivityEventType.CommandRemote
        };

        internal static string Name(ActivityEventType type)
        {
            switch (type)
            {
                case ActivityEventType.RepairItem: return "Repair item";
                case ActivityEventType.RepairBuilding: return "Repair building";
                case ActivityEventType.CommandRemote: return "Command remote";
                default: return type.ToString();
            }
        }
    }
}
