using System.Collections.Generic;
using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Inventory.Use;
using PlayerInventory = Scrapfire.Characters.Inventory.InventoryContainer;

namespace Scrapfire.Characters.Player
{
    [RequireComponent(typeof(CharacterPlayer), typeof(ItemUser))]
    public sealed class MedicalItemUser : MonoBehaviour
    {
        private CharacterPlayer _player;
        private ItemUser _itemUser;

        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
            _itemUser = GetComponent<ItemUser>();
        }

        public IEnumerable<PlayerInventory.Slot> AvailableItems
        {
            get
            {
                foreach (PlayerInventory.Slot slot in _player.CharacterInventory.GetSlots(InventorySlotKind.Backpack))
                {
                    if (!slot.Locked && !slot.Content.IsEmpty && slot.Content.Data.UseBehaviour != null)
                    {
                        yield return slot;
                    }
                }
            }
        }

        // Smallest sufficient heal, otherwise the largest usable heal.
        public PlayerInventory.Slot Recommend()
        {
            if (!_player.CanAct)
            {
                return null;
            }

            float missingHealth = _player.CharacterHealth.MaximumHealthValue
                - _player.CharacterHealth.CurrentHealthValue;

            PlayerInventory.Slot sufficient = null;
            PlayerInventory.Slot largest = null;
            float sufficientHeal = float.PositiveInfinity;
            float largestHeal = 0f;

            foreach (PlayerInventory.Slot slot in AvailableItems)
            {
                if (!(slot.Content.Data.UseBehaviour is HealingItemUseBehaviour healing)
                    || !_itemUser.CanUse(slot))
                {
                    continue;
                }

                if (healing.HealAmount >= missingHealth && healing.HealAmount < sufficientHeal)
                {
                    sufficient = slot;
                    sufficientHeal = healing.HealAmount;
                }

                if (healing.HealAmount > largestHeal)
                {
                    largest = slot;
                    largestHeal = healing.HealAmount;
                }
            }

            return sufficient ?? largest;
        }

        public bool UseRecommended()
        {
            return TryUse(Recommend());
        }

        public bool TryUse(PlayerInventory.Slot slot)
        {
            if (!isActiveAndEnabled || slot == null || slot.Kind != InventorySlotKind.Backpack)
            {
                return false;
            }

            return _itemUser.TryUse(slot);
        }
    }
}
