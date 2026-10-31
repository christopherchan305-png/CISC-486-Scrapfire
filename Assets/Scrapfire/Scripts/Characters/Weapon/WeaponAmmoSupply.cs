using System;
using Scrapfire.Characters.Inventory;
using PlayerInventory = Scrapfire.Characters.Inventory.InventoryContainer;

namespace Scrapfire.Characters.Weapon
{
    // Runtime resource access, independent of input, hit resolution and presentation.
    public sealed class WeaponAmmoSupply
    {
        private readonly WeaponData _data;
        private readonly ItemStack.WeaponState _magazine;
        private readonly PlayerInventory _inventory;
        private int _standaloneReserve;

        public int MagazineAmmo => _magazine.MagazineAmmo;
        public int ReserveAmmo => _inventory != null
            ? _inventory.Count(_data.Ammo.Item)
            : _standaloneReserve;

        public WeaponAmmoSupply(WeaponData data, ItemStack.WeaponState magazine,
            PlayerInventory inventory = null, int standaloneReserve = 0)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));
            _magazine = magazine ?? throw new ArgumentNullException(nameof(magazine));
            _inventory = inventory;
            _standaloneReserve = Math.Max(0, standaloneReserve);
        }

        public bool CanShoot => _data.Ammo != null && _data.Ammo.Item != null
            && _data.Ammo.Item.ItemCategory == Item.Category.Ammo
            && _data.Ammo.AmmoConsumptionPerShoot > 0
            && _data.Ammo.BulletPerShoot > 0
            && (_data.MagazineUsed ? MagazineAmmo : ReserveAmmo) >= _data.Ammo.AmmoConsumptionPerShoot;

        public bool SpendShot()
        {
            if (!CanShoot)
            {
                return false;
            }

            int amount = _data.Ammo.AmmoConsumptionPerShoot;

            if (_data.MagazineUsed)
            {
                return _magazine.Spend(amount);
            }

            return TakeReserve(amount) == amount;
        }

        public bool CanReload => _data.MagazineUsed && _data.Ammo != null
            && _data.Ammo.Item != null && ReserveAmmo > 0
            && MagazineAmmo < Math.Max(1, _data.MagazineCapacity);

        public int ReloadStep()
        {
            if (!CanReload)
            {
                return 0;
            }

            int needed = Math.Max(1, _data.MagazineCapacity) - MagazineAmmo;

            if (_data.MagazineReloadMode == WeaponData.ReloadMode.SingleRound)
            {
                needed = Math.Min(needed, 1);
            }

            int transferred = TakeReserve(needed);
            return _magazine.Load(transferred, _data.MagazineCapacity);
        }

        private int TakeReserve(int amount)
        {
            if (_inventory != null)
            {
                return _inventory.Remove(_data.Ammo.Item, amount);
            }

            int taken = Math.Min(amount, _standaloneReserve);
            _standaloneReserve -= taken;
            return taken;
        }

        public int AddReserve(int amount)
        {
            if (amount <= 0 || _data.Ammo == null || _data.Ammo.Item == null)
            {
                return 0;
            }

            if (_inventory != null)
            {
                ItemStack remainder = _inventory.Add(new ItemStack(_data.Ammo.Item, amount));
                return amount - remainder.Amount;
            }

            int added = Math.Min(amount, int.MaxValue - _standaloneReserve);
            _standaloneReserve += added;
            return added;
        }
    }
}
