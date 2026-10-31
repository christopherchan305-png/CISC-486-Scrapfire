using System;
using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using PlayerInventory = Scrapfire.Characters.Inventory.InventoryContainer;

namespace Scrapfire.Characters.Weapon
{
    [AddComponentMenu("Scrapfire/Character/Weapon/Ranged Weapon")]
    public class RangedWeapon : Weapon
    {
        public WeaponData Data;
        [Min(0)] public int InitialReserveAmmo = 60;

        private WeaponAmmoSupply _supply;
        private PlayerInventory _inventory;
        private WeaponUseContext _context;
        private Vector3 _pendingDirection;
        private bool _triggerWarmed;

        public float AttackInterval => Data != null ? Data.TimeBetweenUse : 0f;
        public float ReloadDuration => Data != null ? Data.MagazineReloadInterval : 0f;
        public int EffectiveMagazineCapacity => Data != null ? Mathf.Max(1, Data.MagazineCapacity) : 0;
        public int MagazineAmmo => Data != null ? Supply.MagazineAmmo : 0;
        public int ReserveAmmo => Data != null && Data.Ammo != null ? Supply.ReserveAmmo : 0;
        public float CurrentSpread => Data != null ? WeaponSpread.GetAngle(Data, _context) : 0f;

        protected override float UseInterval => Data.TimeBetweenUse;
        protected override WeaponState AfterShotState => Data.Mode == WeaponData.FireMode.BoltAction
            ? WeaponState.Cycling : WeaponState.Firing;

        public event Action<int, int> AmmoChanged;
        public event Action<Vector3, Vector3, bool> ShotResolved;
        public event Action<RaycastHit> BulletHit;
        public event Action ReloadStepStarted;

        private WeaponAmmoSupply Supply
        {
            get
            {
                if (_supply == null)
                {
                    int initial = Data.MagazineUsed ? EffectiveMagazineCapacity : 0;
                    _supply = new WeaponAmmoSupply(Data, new ItemStack.WeaponState(initial),
                        standaloneReserve: InitialReserveAmmo);
                }

                return _supply;
            }
        }

        public void BindItem(ItemStack item, PlayerInventory inventory)
        {
            if (item == null || item.IsEmpty || item.Weapon == null || item.Data.Weapon == null)
            {
                throw new ArgumentException("A weapon stack with WeaponData is required.", nameof(item));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            CancelAction();
            UnsubscribeInventory();
            Data = item.Data.Weapon;
            _supply = new WeaponAmmoSupply(Data, item.Weapon, inventory);
            _inventory = inventory;
            _inventory.Changed += NotifyAmmoChanged;
            NotifyAmmoChanged();
        }

        public void SetUseContext(WeaponUseContext context)
        {
            _context = context;

            if (CurrentState == WeaponState.WarmingUp && context.IsSprinting && !Data.FireOnSprint)
            {
                CancelAction();
            }
        }

        // Semi-auto/bolt-action consume a press; automatic fire can continue while held.
        public bool UseTrigger(bool held, bool pressed, Vector3 direction)
        {
            if (!held)
            {
                ReleaseTrigger();
                return false;
            }

            if (Data == null)
            {
                return false;
            }

            if (IsFinite(direction) && direction.sqrMagnitude >= 0.0001f)
            {
                _pendingDirection = direction;
            }

            if (pressed)
            {
                _triggerWarmed = false;
            }

            if (!pressed && Data.Mode != WeaponData.FireMode.Automatic)
            {
                return false;
            }

            return RequestAttack(direction, _triggerWarmed);
        }

        public void ReleaseTrigger()
        {
            _triggerWarmed = false;

            if (CurrentState == WeaponState.WarmingUp)
            {
                CancelAction();
            }
        }

        public override bool TryAttack(Vector3 direction)
        {
            return RequestAttack(direction, false);
        }

        private bool RequestAttack(Vector3 direction, bool alreadyWarmed)
        {
            // Single-round loading can be interrupted by firing a loaded round.
            if (Data != null && CurrentState == WeaponState.Reloading
                && Data.MagazineReloadMode == WeaponData.ReloadMode.SingleRound && CanAttack())
            {
                CancelAction();
            }

            if (!CanStartAttack(direction))
            {
                return false;
            }

            if (Data.TimeDelayBeforeUse > 0f && !alreadyWarmed)
            {
                _pendingDirection = direction;
                StartAction(WeaponState.WarmingUp, Data.TimeDelayBeforeUse);
                return true;
            }

            return ExecuteAttack(direction);
        }

        protected override bool CanAttack()
        {
            return Data != null && Data.Ammo != null && Supply.CanShoot
                && (!_context.IsSprinting || Data.FireOnSprint);
        }

        protected override void PerformAttack(Vector3 origin, Vector3 direction)
        {
            Supply.SpendShot();
            NotifyAmmoChanged();
            WeaponAmmo ammo = Data.Ammo;
            float damageMultiplier = Owner is CharacterPlayer player ? player.DamageMultiplier : 1f;
            float spread = CurrentSpread;
            float range = Mathf.Max(0.01f, Data.Range);

            // One noise per actual discharge, including multi-pellet shots. Never emitted by dry fire or reload.
            var body = Owner.GetComponent<Collider>();
            Vector3 soundFloor = body != null
                ? new Vector3(Owner.transform.position.x, body.bounds.min.y + 0.02f, Owner.transform.position.z)
                : Owner.transform.position;
            WorldNoise.Emit(origin, soundFloor, Data.GunshotRadius);

            for (int pellet = 0; pellet < ammo.BulletPerShoot; pellet++)
            {
                Vector3 shotDirection = WeaponSpread.Apply(direction, spread);

                if (HitscanBullet.Cast(origin, shotDirection, range, HitLayers, Owner, out RaycastHit hit))
                {
                    Health health = hit.collider.GetComponentInParent<Health>();

                    if (health != null && health != Owner.CharacterHealth)
                    {
                        var source = new DamageSource(ammo.BulletDamage * damageMultiplier,
                            DamageType.Projectile, Owner.gameObject, health);
                        ammo.OnBulletHit(source);
                    }

                    BulletHit?.Invoke(hit);
                    ShotResolved?.Invoke(origin, hit.point, true);
                }
                else
                {
                    ShotResolved?.Invoke(origin, origin + shotDirection * range, false);
                }
            }
        }

        public bool TryReload()
        {
            if (!CanUse() || Data == null || CurrentState != WeaponState.Ready || !Supply.CanReload)
            {
                return false;
            }

            StartReloadStep();
            return true;
        }

        protected override void OnActionFinished(WeaponState previous)
        {
            if (previous == WeaponState.WarmingUp)
            {
                _triggerWarmed = true;
                ExecuteAttack(_pendingDirection);
                return;
            }

            if (previous != WeaponState.Reloading)
            {
                return;
            }

            Supply.ReloadStep();
            NotifyAmmoChanged();

            if (Data.MagazineReloadMode == WeaponData.ReloadMode.SingleRound && Supply.CanReload)
            {
                StartReloadStep();
            }
        }

        private void StartReloadStep()
        {
            StartAction(WeaponState.Reloading, Mathf.Max(0.01f, Data.MagazineReloadInterval));
            ReloadStepStarted?.Invoke();
        }

        public int AddReserveAmmo(int amount)
        {
            if (Data == null)
            {
                return 0;
            }

            int added = Supply.AddReserve(amount);
            NotifyAmmoChanged();
            return added;
        }

        public override void CancelAction()
        {
            _triggerWarmed = false;
            _pendingDirection = Vector3.zero;
            base.CancelAction();
        }

        private void NotifyAmmoChanged()
        {
            AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
        }

        private void UnsubscribeInventory()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= NotifyAmmoChanged;
                _inventory = null;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInventory();
        }
    }
}
