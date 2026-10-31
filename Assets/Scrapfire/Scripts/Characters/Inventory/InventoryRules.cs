namespace Scrapfire.Characters.Inventory
{
    /// <summary>Game-specific category layout and upgrade limits.</summary>
    public static class InventoryRules
    {
        public const int MaximumWeaponSlots = 2;
        public const int MaximumArmorSlots = 1;
        public const int DefaultBackpackSlots = 12;
        public const int MaximumBackpackSlots = 24;

        public static InventoryContainer CreateInventory(int backpackSlots = DefaultBackpackSlots)
        {
            return new InventoryContainer.Builder()
                .AddSlots(InventorySlotKind.Weapon, MaximumWeaponSlots)
                .AddSlots(InventorySlotKind.Armor, MaximumArmorSlots)
                .AddSlots(InventorySlotKind.Backpack, backpackSlots)
                .Build();
        }

        public static int MaximumSlots(InventorySlotKind kind)
        {
            if (kind == InventorySlotKind.Weapon)
            {
                return MaximumWeaponSlots;
            }

            return kind == InventorySlotKind.Armor ? MaximumArmorSlots : MaximumBackpackSlots;
        }
    }
}
