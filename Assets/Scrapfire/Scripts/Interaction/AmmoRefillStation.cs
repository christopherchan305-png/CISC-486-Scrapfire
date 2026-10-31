using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Weapon;

namespace Scrapfire.Interaction
{
    /// <summary>World interaction adapter; resources remain in the purchasing player's inventory.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class AmmoRefillStation : MonoBehaviour, IPlayerInteractable
    {
        [Min(0)] public int Cost = 50;
        [Min(1)] public int TargetReserve = 60;
        private bool _purchasing;

        public bool CanInteract(CharacterPlayer player) => isActiveAndEnabled && !_purchasing
            && player != null && player.CanAct && AmmoFor(player) != null;

        private static Item AmmoFor(CharacterPlayer player)
        {
            var weapon = player.CurrentWeapon as RangedWeapon;
            var ammo = weapon != null && weapon.Data != null ? weapon.Data.Ammo?.Item : null;
            return ammo != null && ammo.ItemCategory == Item.Category.Ammo ? ammo : null;
        }

        public string GetPrompt(CharacterPlayer player)
        {
            var ammo = AmmoFor(player);
            if (ammo == null) return "Equip a ranged weapon";
            int missing = TargetReserve - player.CharacterInventory.Count(ammo);
            if (missing <= 0) return "Reserve ammo full";
            if (player.CharacterInventory.RemainingCapacity(new ItemStack(ammo, missing)) < missing)
                return "Backpack full";
            return $"F  Refill reserve to {TargetReserve} — ${Cost}";
        }

        public bool TryInteract(CharacterPlayer player)
        {
            if (!CanInteract(player) || Cost < 0 || TargetReserve <= 0) return false;
            var interactor = player.GetComponent<PlayerInteractor>();
            var collider = GetComponent<BoxCollider>();
            if (interactor == null || !collider.enabled
                || Vector3.Distance(collider.ClosestPoint(player.transform.position), player.transform.position) > interactor.Reach)
                return false;
            var ammo = AmmoFor(player);
            var inventory = player.CharacterInventory;
            int missing = TargetReserve - inventory.Count(ammo);
            if (missing <= 0 || inventory.RemainingCapacity(new ItemStack(ammo, missing)) < missing) return false;
            _purchasing = true;
            try
            {
                if (!player.Balance.TrySpend(Cost)) return false;
                // Balance events may change the inventory or selected weapon: recheck before committing.
                missing = TargetReserve - inventory.Count(ammo);
                if (AmmoFor(player) == ammo && missing > 0 && inventory.TryAddAll(new ItemStack(ammo, missing)))
                    return true;
                player.Balance.Credit(Cost);
                return false;
            }
            finally { _purchasing = false; }
        }
    }
}
