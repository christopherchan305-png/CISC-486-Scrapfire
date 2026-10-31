using System;

namespace Scrapfire.Characters.Inventory
{
    public enum InventorySlotKind { Weapon, Armor, Backpack }

    public sealed partial class InventoryContainer
    {
        public sealed class Slot
        {
            public InventorySlotKind Kind { get; }
            public bool Locked { get; private set; }
            public ItemStack Content { get; internal set; } = ItemStack.EMPTY;

            public bool IsFull => Locked || Content.IsFull;

            internal Slot(InventorySlotKind kind, bool locked)
            {
                if (!Enum.IsDefined(typeof(InventorySlotKind), kind))
                {
                    throw new ArgumentOutOfRangeException(nameof(kind));
                }

                Kind = kind;
                Locked = locked;
            }

            public bool Accepts(Item item)
            {
                if (Locked || item == null || !item.HasValidCategory) return false;
                switch (Kind)
                {
                    case InventorySlotKind.Weapon: return item.ItemCategory == Item.Category.Weapon;
                    case InventorySlotKind.Armor: return item.ItemCategory == Item.Category.Armor;
                    default: return item.ItemCategory != Item.Category.Weapon && item.ItemCategory != Item.Category.Armor;
                }
            }

            internal bool Unlock()
            {
                if (!Locked)
                {
                    return false;
                }

                Locked = false;
                return true;
            }
        }
    }
}
