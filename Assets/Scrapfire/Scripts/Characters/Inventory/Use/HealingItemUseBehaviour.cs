using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Inventory.Use
{
    [CreateAssetMenu(menuName = "Scrapfire/Items/Use/Healing")]
    public sealed class HealingItemUseBehaviour : ItemUseBehaviour
    {
        [Min(0f)] public float HealAmount = 25f;

        public override int ConsumedAmount => 1;

        public override bool CanUse(CharacterPlayer user, ItemStack stack)
        {
            if (user == null || !user.CanAct || stack == null || stack.IsEmpty)
            {
                return false;
            }

            Health health = user.CharacterHealth;

            return health.isActiveAndEnabled
                && HealAmount > 0f
                && !float.IsInfinity(HealAmount)
                && !float.IsNaN(HealAmount)
                && health.CurrentHealthValue < health.MaximumHealthValue;
        }

        protected override bool Use(CharacterPlayer user, ItemStack stack)
        {
            return user.CharacterHealth.Heal(HealAmount) > 0f;
        }
    }
}
