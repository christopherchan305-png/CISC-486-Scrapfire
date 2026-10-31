using System;
using UnityEngine;

namespace Scrapfire.Characters.Inventory
{
    public sealed class ItemStack
    {
        public sealed class WeaponState
        {
            public int MagazineAmmo { get; private set; }
            internal WeaponState(int ammo) { MagazineAmmo = Math.Max(0, ammo); }
            public bool SpendRound()
            {
                return Spend(1);
            }
            public bool Spend(int amount)
            {
                if (amount <= 0 || MagazineAmmo < amount)
                {
                    return false;
                }

                MagazineAmmo -= amount;
                return true;
            }

            public int Load(int amount, int capacity)
            {
                int loaded = Math.Min(Math.Max(0, amount), Math.Max(0, capacity - MagazineAmmo));
                MagazineAmmo += loaded;
                return loaded;
            }
            internal WeaponState Copy() => new WeaponState(MagazineAmmo);
        }

        public static readonly ItemStack EMPTY = new ItemStack();
        public Item Data { get; private set; }
        public int Amount { get; private set; }
        public float Durability { get; private set; }
        public WeaponState Weapon { get; private set; }
        internal InventoryContainer Owner { get; set; }
        public bool IsEmpty => Data == null || Amount <= 0;
        public bool IsFull => !IsEmpty && Amount >= Data.StackLimit;

        private ItemStack() { }

        // Bulk incoming resources may exceed a slot limit; equipment always has quantity one.
        public ItemStack(Item data, int amount = 1, float? durability = null)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.HasValidCategory) throw new ArgumentException("An item must have exactly one category.", nameof(data));
            if (amount <= 0 || (data.StackLimit == 1 && amount != 1)) throw new ArgumentOutOfRangeException(nameof(amount));
            float maximum = Mathf.Max(0f, data.Durability);
            float value = durability ?? maximum;
            if (value < 0f || float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(durability));
            Data = data;
            Amount = amount;
            Durability = maximum > 0f ? Mathf.Min(value, maximum) : 0f;
            if (data.ItemCategory == Item.Category.Weapon)
            {
                int capacity = data.Weapon != null ? Mathf.Max(1, data.Weapon.MagazineCapacity) : 1;
                int initial = data.Weapon != null && data.Weapon.MagazineUsed ? capacity : 0;
                Weapon = new WeaponState(Mathf.Clamp(initial, 0, capacity));
            }
        }

        public void Reset()
        {
            EnsureDetached();
            if (ReferenceEquals(this, EMPTY)) return;
            Data = null;
            Amount = 0;
            Durability = 0f;
            Weapon = null;
        }

        public ItemStack Copy() => Copy(Amount);
        public ItemStack Copy(int amount)
        {
            if (IsEmpty) return EMPTY;
            var copy = new ItemStack(Data, amount, Durability);
            copy.Weapon = Weapon?.Copy();
            return copy;
        }
        public ItemStack CopyAndReset()
        {
            EnsureDetached();
            var copy = Copy();
            Reset();
            return copy;
        }
        public ItemStack Split(int amount)
        {
            EnsureDetached();
            return SplitCore(amount);
        }
        internal ItemStack SplitOwned(int amount) => SplitCore(amount);
        private ItemStack SplitCore(int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (IsEmpty) return EMPTY;
            var taken = Copy(Math.Min(amount, Amount));
            Amount -= taken.Amount;
            if (Amount == 0) { Owner = null; Reset(); }
            return taken;
        }
        private void EnsureDetached()
        {
            if (Owner != null) throw new InvalidOperationException("Change owned stacks through their inventory.");
        }
        internal void Grow(int amount) { if (!IsEmpty) Amount = checked(Amount + amount); }
        internal void Wear(float amount) { if (!IsEmpty) Durability = Mathf.Max(0f, Durability - amount); }

        // Any future per-instance properties must also participate in copying and matching.
        public bool LikelyEquals(ItemStack other) => other != null && !IsEmpty && !other.IsEmpty
            && other.Data == Data && other.Durability == Durability
            && (other.Weapon?.MagazineAmmo ?? 0) == (Weapon?.MagazineAmmo ?? 0);
    }
}
