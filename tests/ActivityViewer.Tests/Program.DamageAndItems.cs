using System;
using ActivityViewer.Core.Data;
using Mod = Vikings_ActivityLog;

namespace ActivityViewer.Tests
{
    internal static partial class Program
    {
        static partial void RunDamageAndItems()
        {
            Damage_FiltersAndColumns();
            Items_FiltersAndRoutes();
            Route_ParsesMoveDetails();
        }

        private static void Damage_FiltersAndColumns()
        {
            Mod.ActivityRecord boar = Rec(Mod.ActivityEventType.Damage, 0, target: "Boar");
            boar.Damage = new Mod.ActivityDamage { Slash = 30f, Fire = 2f, Total = 32f };
            Mod.ActivityRecord troll = Rec(Mod.ActivityEventType.Damage, 1000, target: "Troll");
            troll.Damage = new Mod.ActivityDamage { Blunt = 5f, Total = 5f };
            Mod.ActivityRecord hurt = Rec(Mod.ActivityEventType.Damaged, 2000, target: "Greydwarf");
            hurt.Damage = new Mod.ActivityDamage { Pierce = 12f, Total = 12f, HealthAfter = 40f };
            Mod.ActivityRecord other = Rec(Mod.ActivityEventType.Damage, 3000, "Steam_2", "Bjorn", target: "Boar");
            other.Damage = new Mod.ActivityDamage { Slash = 8f, Total = 8f };
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("damage", boar, troll, hurt, other));

            ResultTable all = DamageQuery.Run(db, QueryFilter.All, new DamageCriteria(), default);
            Check(all.Rows.Count == 4, "Damage: all four damage rows");
            Check((string?)all.Value(0, "Target") == "Boar" && (string?)all.Value(0, "Event") == "Damage", "Damage: rows ordered by time");
            Check((string?)all.Value(0, "Types") == "Corte 30 · Fogo 2", "Damage: non-zero damage types listed");
            Check(all.Value(0, "HealthAfter") == null && (double?)all.Value(2, "HealthAfter") == 40.0, "Damage: health after only for Damaged");

            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { MinTotal = 10 }, default).Rows.Count == 2, "Damage: minimum total");
            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Target = "boar" }, default).Rows.Count == 2, "Damage: target filter is case-insensitive and keeps only Damage");
            ResultTable attacker = DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Attacker = "GREY" }, default);
            Check(attacker.Rows.Count == 1 && (string?)attacker.Value(0, "Event") == "Damaged", "Damage: attacker filter keeps only Damaged");
            Check(DamageQuery.Run(db, QueryFilter.All, new DamageCriteria { Target = "boar", Attacker = "grey" }, default).Rows.Count == 3, "Damage: target and attacker together give the union");
            Check(DamageQuery.Run(db, new QueryFilter { PlatformId = "Steam_2" }, new DamageCriteria(), default).Rows.Count == 1, "Damage: global player filter");
            var period = new QueryFilter { From = TimeFormat.ToLocal(T0 + 500), To = TimeFormat.ToLocal(T0 + 2500) };
            Check(DamageQuery.Run(db, period, new DamageCriteria(), default).Rows.Count == 2, "Damage: global period filter");
            ResultTable capped = DamageQuery.Run(db, new QueryFilter { RowLimit = 2 }, new DamageCriteria(), default);
            Check(capped.Rows.Count == 2 && capped.Truncated, "Damage: row limit truncates and flags the result");
        }

        private static void Items_FiltersAndRoutes()
        {
            Mod.ActivityRecord pickup = Rec(Mod.ActivityEventType.Pickup, 0);
            pickup.Items.Add(Item("Wood", 10));
            Mod.ActivityRecord move = Rec(Mod.ActivityEventType.Move, 1000, amount: 1, details: "from:Inventory to:Baú grande");
            move.Items.Add(Item("SwordIron", 1, 3, crafterId: 9, crafterName: "Bjorn"));
            Mod.ActivityRecord drop = Rec(Mod.ActivityEventType.Drop, 2000, target: "Inventory", amount: 5);
            drop.Items.Add(Item("Stone", 5));
            Mod.ActivityRecord craft = Rec(Mod.ActivityEventType.Craft, 3000, "Steam_2", "Bjorn");
            craft.Items.Add(Item("ArrowWood", 20));
            Mod.ActivityRecord consume = Rec(Mod.ActivityEventType.Consume, 4000, target: "Inventory");
            consume.Items.Add(Item("CookedMeat", 1));
            Mod.ActivityRecord snapshot = Rec(Mod.ActivityEventType.Inventory, 5000);
            snapshot.Items.Add(Item("Wood", 50));
            using ActivityDatabase db = ActivityDatabase.Open(BuildDb("items", pickup, move, drop, craft, consume, snapshot));

            ResultTable all = ItemQuery.Run(db, QueryFilter.All, new ItemCriteria(), default);
            Check(all.Rows.Count == 5, "Items: five item rows, inventory snapshots excluded");
            Check((string?)all.Value(1, "Item") == "SwordIron" && (long?)all.Value(1, "Quality") == 3 && (string?)all.Value(1, "Crafter") == "Bjorn", "Items: item, quality and crafter");
            Check((string?)all.Value(1, "Origin") == "Inventory" && (string?)all.Value(1, "Destination") == "Baú grande", "Items: move origin and destination");
            Check((string?)all.Value(2, "Origin") == "Inventory" && (string?)all.Value(2, "Destination") == RouteParser.Ground, "Items: drop goes to the ground");
            Check((string?)all.Value(0, "Origin") == RouteParser.Ground, "Items: pickup comes from the ground");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Prefab = "sword" }, default).Rows.Count == 1, "Items: prefab filter");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { MinCount = 10 }, default).Rows.Count == 2, "Items: minimum quantity");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Events = new[] { "Craft" } }, default).Rows.Count == 1, "Items: event type filter");
            Check(ItemQuery.Run(db, QueryFilter.All, new ItemCriteria { Events = Array.Empty<string>() }, default).Rows.Count == 0, "Items: no event types gives no rows");
            Check(ItemQuery.Run(db, new QueryFilter { PlatformId = "Steam_2" }, new ItemCriteria(), default).Rows.Count == 1, "Items: global player filter");
        }

        private static void Route_ParsesMoveDetails()
        {
            Check(RouteParser.Parse("MoveAll", "", "from:Baú de ferro to:Inventory") == ("Baú de ferro", "Inventory"), "Route: names with spaces");
            Check(RouteParser.Parse("Move", "", "texto inesperado") == ("", ""), "Route: unexpected details give empty route");
            Check(RouteParser.Parse("Consume", "Inventory", "") == ("Inventory", "Consumido"), "Route: consume");
            Check(RouteParser.Parse("Equip", "", "") == ("", ""), "Route: other events have no route");
        }
    }
}
