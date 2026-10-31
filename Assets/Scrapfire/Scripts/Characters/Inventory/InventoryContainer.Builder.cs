using System;
using System.Collections.Generic;

namespace Scrapfire.Characters.Inventory
{
    public sealed partial class InventoryContainer
    {
        public sealed class Builder
        {
            private readonly List<Slot> _slots = new List<Slot>();

            public Builder AddSlots(InventorySlotKind kind, int amount, bool locked = false)
            {
                if (!Enum.IsDefined(typeof(InventorySlotKind), kind))
                {
                    throw new ArgumentOutOfRangeException(nameof(kind));
                }

                if (amount <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount));
                }

                int existing = 0;

                foreach (Slot slot in _slots)
                {
                    if (slot.Kind == kind)
                    {
                        existing++;
                    }
                }

                if (amount > InventoryRules.MaximumSlots(kind) - existing)
                {
                    throw new ArgumentException("Category slot limit reached.", nameof(amount));
                }

                for (int i = 0; i < amount; i++)
                {
                    _slots.Add(new Slot(kind, locked));
                }

                return this;
            }

            public InventoryContainer Build()
            {
                // Reusing a builder never shares mutable slots between players.
                var slots = new List<Slot>(_slots.Count);

                foreach (Slot slot in _slots)
                {
                    slots.Add(new Slot(slot.Kind, slot.Locked));
                }

                return new InventoryContainer(slots);
            }
        }
    }
}
