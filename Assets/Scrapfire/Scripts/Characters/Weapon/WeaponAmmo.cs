using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;

namespace Scrapfire.Characters.Weapon
{
    [CreateAssetMenu(menuName = "Scrapfire/Weapons/Standard Ammo")]
    public class WeaponAmmo : ScriptableObject
    {
        // Inventory identity is separate from the ballistic behavior of this ammunition.
        public Item Item;
        [Min(1)] public int AmmoConsumptionPerShoot = 1;
        [Min(1)] public int BulletPerShoot = 1;
        [Min(0f)] public float BulletDamage = 20f;

        // Called once per pellet hitting a Health, with attribution and modified damage.
        // Derived ammunition can apply additional effects after checking applied damage.
        public virtual float OnBulletHit(DamageSource source)
        {
            if (source.HealthOnVictim == null)
            {
                return 0f;
            }

            return source.HealthOnVictim.Damage(source);
        }
    }
}
