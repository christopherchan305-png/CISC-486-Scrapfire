using System;
using System.Collections.Generic;
using System.Linq;

namespace Scrapfire.Characters.Inventory
{
    /// <summary>Per-owner runtime storage. Slot kinds are separate from item categories.</summary>
    public sealed partial class InventoryContainer
    {
        private readonly List<Slot> _slots;

        public IReadOnlyList<Slot> Slots { get; }
        public event Action Changed;

        private InventoryContainer(List<Slot> slots)
        {
            _slots = slots;
            Slots = _slots.AsReadOnly();
        }

        public IEnumerable<Slot> GetSlots(InventorySlotKind kind) => _slots.Where(x => x.Kind == kind);
        public IEnumerable<Slot> GetItems(Item.Category category) => _slots.Where(x => !x.Content.IsEmpty && x.Content.Data.ItemCategory == category);
        public int BackpackCapacity => _slots.Count(x => x.Kind == InventorySlotKind.Backpack);

        public int IndexOf(Slot slot)
        {
            return _slots.IndexOf(slot);
        }

        public bool UnlockSlot(int index)
        {
            ValidateIndex(index);

            if (!_slots[index].Unlock())
            {
                return false;
            }

            NotifyChanged();
            return true;
        }

        public bool CanAddSlot(InventorySlotKind kind)
        {
            if (!Enum.IsDefined(typeof(InventorySlotKind), kind))
            {
                return false;
            }

            int count = 0;

            foreach (Slot slot in GetSlots(kind))
            {
                count++;
            }

            return count < InventoryRules.MaximumSlots(kind);
        }

        public int AddSlot(InventorySlotKind kind, bool locked = false)
        {
            if (!CanAddSlot(kind))
            {
                throw new ArgumentException("Invalid slot kind or slot limit reached.", nameof(kind));
            }

            _slots.Add(new Slot(kind, locked));
            NotifyChanged();

            return _slots.Count - 1;
        }

        /// <summary>Fills compatible stacks, then unlocked empty slots. Returns the remainder.</summary>
        public ItemStack Add(ItemStack stack)
        {
            using var batch = Batch();
            ValidateIncoming(stack);

            if (stack.IsEmpty)
            {
                return ItemStack.EMPTY;
            }

            for (int i = 0; i < _slots.Count && !stack.IsEmpty; i++)
            {
                Slot slot = _slots[i];

                if (!slot.Locked && slot.Content.LikelyEquals(stack) && !slot.IsFull)
                {
                    Add(stack, i);
                }
            }

            for (int i = 0; i < _slots.Count && !stack.IsEmpty; i++)
            {
                Slot slot = _slots[i];

                if (slot.Content.IsEmpty && slot.Accepts(stack.Data))
                {
                    Add(stack, i);
                }
            }

            return stack.IsEmpty ? ItemStack.EMPTY : stack;
        }

        public ItemStack Add(ItemStack stack, int index)
        {
            ValidateIndex(index);
            ValidateIncoming(stack);

            if (stack.IsEmpty)
            {
                return ItemStack.EMPTY;
            }

            Slot slot = _slots[index];

            if (!slot.Accepts(stack.Data))
            {
                return stack;
            }

            if (!slot.Content.IsEmpty && !slot.Content.LikelyEquals(stack))
            {
                return stack;
            }

            int currentAmount = slot.Content.IsEmpty ? 0 : slot.Content.Amount;
            int availableSpace = Math.Max(0, stack.Data.StackLimit - currentAmount);
            int addedAmount = Math.Min(availableSpace, stack.Amount);

            if (addedAmount == 0)
            {
                return stack;
            }

            ItemStack transferred = stack.Split(addedAmount);

            if (slot.Content.IsEmpty)
            {
                slot.Content = transferred;
                transferred.Owner = this;
            }
            else
            {
                slot.Content.Grow(addedAmount);
            }

            NotifyChanged();

            return stack.IsEmpty ? ItemStack.EMPTY : stack;
        }

        public ItemStack Remove(int index)
        {
            ValidateIndex(index);

            Slot slot = _slots[index];

            if (slot.Locked || slot.Content.IsEmpty)
            {
                return ItemStack.EMPTY;
            }

            ItemStack taken = slot.Content;
            slot.Content = ItemStack.EMPTY;
            taken.Owner = null;
            NotifyChanged();

            return taken;
        }

        public ItemStack Remove(int index, int amount)
        {
            ValidateIndex(index);

            if (amount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            Slot slot = _slots[index];

            if (slot.Locked || slot.Content.IsEmpty)
            {
                return ItemStack.EMPTY;
            }

            if (amount >= slot.Content.Amount)
            {
                return Remove(index);
            }

            ItemStack taken = slot.Content.SplitOwned(amount);
            NotifyChanged();

            return taken;
        }

        /// <summary>All-or-nothing removal of matching type and instance properties.</summary>
        public bool Take(ItemStack request)
        {
            using var batch = Batch();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (request.IsEmpty)
            {
                return true;
            }

            ItemStack snapshot = request.Copy();
            long available = 0;

            foreach (Slot slot in _slots)
            {
                if (!slot.Locked && slot.Content.LikelyEquals(snapshot))
                {
                    available += slot.Content.Amount;
                }
            }

            if (available < snapshot.Amount)
            {
                return false;
            }

            int remaining = snapshot.Amount;

            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Slot slot = _slots[i];

                if (!slot.Locked && slot.Content.LikelyEquals(snapshot))
                {
                    remaining -= Remove(i, remaining).Amount;
                }
            }

            return true;
        }

        /// <summary>Consumes up to the requested quantity across slots of one item type.</summary>
        public int Remove(Item item, int amount)
        {
            using var batch = Batch();
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (amount <= 0)
            {
                return 0;
            }

            int remaining = amount;

            for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                Slot slot = _slots[i];

                if (!slot.Locked && !slot.Content.IsEmpty && slot.Content.Data == item)
                {
                    remaining -= Remove(i, remaining).Amount;
                }
            }

            return amount - remaining;
        }

        public int Count(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            int total = 0;

            foreach (Slot slot in _slots)
            {
                if (!slot.Locked && !slot.Content.IsEmpty && slot.Content.Data == item)
                {
                    total = checked(total + slot.Content.Amount);
                }
            }

            return total;
        }

        private void ValidateIndex(int index)
        {
            if (index < 0 || index >= _slots.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private void ValidateIncoming(ItemStack stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            if (stack.Owner != null)
                throw new ArgumentException("Remove or transfer an owned stack before adding it to any inventory.", nameof(stack));
        }
    }
}
