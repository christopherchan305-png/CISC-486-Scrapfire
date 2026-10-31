using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Inventory.Use
{
    // Shared action definition. Per-player and per-stack state must stay outside this asset.
    public abstract class ItemUseBehaviour : ScriptableObject
    {
        public virtual int ConsumedAmount => 0;

        public abstract bool CanUse(CharacterPlayer user, ItemStack stack);

        public bool TryUse(CharacterPlayer user, ItemStack stack)
        {
            if (!CanUse(user, stack))
            {
                return false;
            }

            return Use(user, stack);
        }

        // Apply the effect only. ItemUser handles inventory validation and consumption.
        protected abstract bool Use(CharacterPlayer user, ItemStack stack);
    }
}
