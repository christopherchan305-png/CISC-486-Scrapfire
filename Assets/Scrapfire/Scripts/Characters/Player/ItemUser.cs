using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Inventory.Use;
using PlayerInventory = Scrapfire.Characters.Inventory.InventoryContainer;

namespace Scrapfire.Characters.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterPlayer))]
    public sealed class ItemUser : MonoBehaviour
    {
        private CharacterPlayer _player;

        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
        }

        public bool CanUse(PlayerInventory.Slot slot)
        {
            if (!isActiveAndEnabled || !_player.CanAct || slot == null
                || slot.Locked || slot.Content.IsEmpty
                || _player.CharacterInventory.IndexOf(slot) < 0)
            {
                return false;
            }

            ItemStack stack = slot.Content;
            ItemUseBehaviour behaviour = stack.Data.UseBehaviour;

            return behaviour != null
                && behaviour.ConsumedAmount >= 0
                && stack.Amount >= behaviour.ConsumedAmount
                && behaviour.CanUse(_player, stack);
        }

        public bool TryUse(PlayerInventory.Slot slot)
        {
            if (!CanUse(slot))
            {
                return false;
            }

            ItemStack stack = slot.Content;
            ItemUseBehaviour behaviour = stack.Data.UseBehaviour;
            int consumedAmount = behaviour.ConsumedAmount;

            if (!behaviour.TryUse(_player, stack))
            {
                return false;
            }

            if (consumedAmount > 0)
            {
                int index = _player.CharacterInventory.IndexOf(slot);
                _player.CharacterInventory.Remove(index, consumedAmount);
            }

            return true;
        }
    }
}
