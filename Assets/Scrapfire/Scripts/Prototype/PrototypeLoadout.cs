using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;

namespace Scrapfire.Prototype
{
    /// <summary>Finite one-time starter supplies, separate from inventory capacity rules.</summary>
    [RequireComponent(typeof(CharacterPlayer))]
    public sealed class PrototypeLoadout : MonoBehaviour
    {
        public Item Rifle;
        public Item Ammo;
        public Item Medkit;
        [Min(0)] public int ReserveAmmo = 60;
        [Min(0)] public int MedicalCount = 2;
        private void Start()
        {
            var inventory = GetComponent<CharacterPlayer>().CharacterInventory;
            if (Rifle != null) Grant(inventory, new ItemStack(Rifle));
            if (Ammo != null && ReserveAmmo > 0) Grant(inventory, new ItemStack(Ammo, ReserveAmmo));
            if (Medkit != null && MedicalCount > 0) Grant(inventory, new ItemStack(Medkit, MedicalCount));
        }
        private void Grant(InventoryContainer inventory, ItemStack stack)
        {
            if (!inventory.TryAddAll(stack)) Debug.LogWarning("Starter supply does not fit: " + stack.Data.DisplayName, this);
        }
    }
}
