using System;

namespace Scrapfire.Characters.Inventory
{
    public sealed partial class InventoryContainer
    {
        private int _batchDepth;
        private bool _changed;
        private IDisposable Batch() { _batchDepth++; return new MutationScope(this); }
        private void NotifyChanged()
        {
            if (_batchDepth > 0) _changed = true;
            else Changed?.Invoke();
        }
        private sealed class MutationScope : IDisposable
        {
            private InventoryContainer _owner;
            public MutationScope(InventoryContainer owner) { _owner = owner; }
            public void Dispose()
            {
                if (_owner == null) return;
                var owner = _owner; _owner = null;
                owner._batchDepth--;
                if (owner._batchDepth == 0 && owner._changed)
                { owner._changed = false; owner.Changed?.Invoke(); }
            }
        }

        public int RemainingCapacity(ItemStack stack)
        {
            if (stack == null) throw new ArgumentNullException(nameof(stack));
            if (stack.IsEmpty) return 0;
            long capacity = 0;
            foreach (var slot in _slots)
            {
                if (!slot.Accepts(stack.Data)) continue;
                if (slot.Content.IsEmpty) capacity += stack.Data.StackLimit;
                else if (slot.Content.LikelyEquals(stack)) capacity += Math.Max(0, stack.Data.StackLimit - slot.Content.Amount);
            }
            return (int)Math.Min(int.MaxValue, capacity);
        }

        public bool TryAddAll(ItemStack stack)
        {
            ValidateIncoming(stack);
            if (stack.IsEmpty) return true;
            if (RemainingCapacity(stack) < stack.Amount) return false;
            Add(stack);
            return true;
        }

        public bool TryExpandBackpack(int capacity)
        {
            if (capacity <= BackpackCapacity || capacity > InventoryRules.MaximumBackpackSlots) return false;
            while (BackpackCapacity < capacity) _slots.Add(new Slot(InventorySlotKind.Backpack, false));
            NotifyChanged();
            return true;
        }

        // Returns a detached instance for a future world-drop/pickup system; does not create a GameObject.
        public ItemStack Drop(int index, int amount = int.MaxValue) => Remove(index, amount);

        private ItemStack Detach(int index, int amount)
        {
            var slot = _slots[index];
            if (amount >= slot.Content.Amount)
            {
                var result = slot.Content;
                slot.Content = ItemStack.EMPTY;
                result.Owner = null;
                return result;
            }
            return slot.Content.SplitOwned(amount);
        }

        public bool TryMove(int from, int to, int amount = int.MaxValue)
        {
            ValidateIndex(from); ValidateIndex(to);
            if (from == to || amount <= 0) return false;
            var source = _slots[from]; var target = _slots[to];
            if (source.Locked || source.Content.IsEmpty || !target.Accepts(source.Content.Data)) return false;
            if (!target.Content.IsEmpty && !target.Content.LikelyEquals(source.Content)) return false;
            int space = source.Content.Data.StackLimit - (target.Content.IsEmpty ? 0 : target.Content.Amount);
            int moved = Math.Min(space, Math.Min(amount, source.Content.Amount));
            if (moved <= 0) return false;
            using var batch = Batch();
            var taken = Detach(from, moved);
            Add(taken, to);
            return true;
        }

        public bool TrySwap(int first, int second)
        {
            ValidateIndex(first); ValidateIndex(second);
            if (first == second) return false;
            var a = _slots[first]; var b = _slots[second];
            if (a.Locked || b.Locked || (a.Content.IsEmpty && b.Content.IsEmpty)) return false;
            if ((!a.Content.IsEmpty && !b.Accepts(a.Content.Data)) || (!b.Content.IsEmpty && !a.Accepts(b.Content.Data))) return false;
            var temporary = a.Content; a.Content = b.Content; b.Content = temporary;
            NotifyChanged();
            return true;
        }

        public bool TryEquip(int index, ItemStack equipment, out ItemStack displaced)
        {
            ValidateIndex(index); ValidateIncoming(equipment);
            displaced = ItemStack.EMPTY;
            var slot = _slots[index];
            if (equipment.IsEmpty || equipment.Amount != 1 || slot.Kind == InventorySlotKind.Backpack || !slot.Accepts(equipment.Data)) return false;
            var replacement = equipment.Split(1);
            displaced = slot.Content;
            if (!displaced.IsEmpty) displaced.Owner = null;
            replacement.Owner = this;
            slot.Content = replacement;
            NotifyChanged();
            return true;
        }

        /// <summary>All-or-nothing handoff. Failed transfers change neither inventory.</summary>
        public bool TryTransferTo(InventoryContainer target, int index, int amount = int.MaxValue)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            ValidateIndex(index);
            var slot = _slots[index];
            if (ReferenceEquals(this, target) || slot.Locked || slot.Content.IsEmpty || amount <= 0) return false;
            if (amount != int.MaxValue && amount > slot.Content.Amount) return false;
            int transferred = Math.Min(amount, slot.Content.Amount);
            if (target.RemainingCapacity(slot.Content) < transferred) return false;
            // Both data sets are committed before events. Notify the old owner first to unequip it.
            using var targetBatch = target.Batch();
            using var sourceBatch = Batch();
            var taken = Detach(index, transferred);
            target.Add(taken);
            NotifyChanged();
            return true;
        }
    }
}
