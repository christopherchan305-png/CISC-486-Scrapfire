using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Weapon;

namespace Scrapfire.Tests
{
    public class WeaponSystemTests
    {
        private readonly List<Object> _created = new List<Object>();
        private static readonly Vector3 Origin = new Vector3(11000f, 11000f, 11000f);

        private T Asset<T>() where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        private RangedWeapon CreateGun(int capacity = 6, int reserve = 20,
            bool magazineUsed = true)
        {
            Item ammoItem = Asset<Item>();
            ammoItem.ItemCategory = Item.Category.Ammo;
            ammoItem.StackSize = 100;
            var ammo = Asset<TestWeaponAmmo>();
            ammo.Item = ammoItem;
            WeaponData data = Asset<WeaponData>();
            data.Ammo = ammo;
            data.MagazineCapacity = capacity;
            data.MagazineUsed = magazineUsed;
            data.TimeBetweenUse = 0.02f;
            data.MagazineReloadInterval = 0.04f;
            data.RecoilValue = 0f;
            data.RecoilValueOnAim = 0f;
            Item item = Asset<Item>();
            item.ItemCategory = Item.Category.Weapon;
            item.Weapon = data;
            var ownerObject = new GameObject("gunOwner");
            ownerObject.transform.position = Origin;
            _created.Add(ownerObject);
            var owner = ownerObject.AddComponent<CharacterPlayer>();
            owner.CharacterInventory.Add(new ItemStack(item));
            if (reserve > 0)
            {
                owner.CharacterInventory.Add(new ItemStack(ammoItem, reserve));
            }

            var gun = ownerObject.AddComponent<RangedWeapon>();
            gun.BindItem(owner.CharacterInventory.Slots[0].Content, owner.CharacterInventory);
            owner.EquipWeapon(gun);
            return gun;
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [UnityTest]
        public IEnumerator PelletsConsumePerShotAndInvokeAmmoHitPerPellet()
        {
            RangedWeapon gun = CreateGun();
            var ammo = (TestWeaponAmmo)gun.Data.Ammo;
            ammo.AmmoConsumptionPerShoot = 2;
            ammo.BulletPerShoot = 5;
            ammo.BulletDamage = 3f;
            var targetObject = new GameObject("pelletTarget");
            _created.Add(targetObject);
            targetObject.transform.position = Origin + Vector3.forward * 3f;
            var target = targetObject.AddComponent<Health>();
            targetObject.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            int pellets = 0;
            gun.ShotResolved += (start, end, hit) => pellets++;
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
            Assert.AreEqual(4, gun.MagazineAmmo);
            Assert.AreEqual(20, gun.ReserveAmmo);
            Assert.AreEqual(5, pellets);
            Assert.AreEqual(5, ammo.Hits);
            Assert.AreEqual(85f, target.CurrentHealthValue);
            Assert.AreSame(gun.Owner.gameObject, ammo.LastSource.CausedBy);
            Assert.AreSame(target, ammo.LastSource.HealthOnVictim);
            yield return new WaitForSeconds(0.05f);
            ammo.AmmoConsumptionPerShoot = 5;
            Assert.IsFalse(gun.TryAttack(Vector3.forward));
            Assert.AreEqual(4, gun.MagazineAmmo);
            Assert.AreEqual(5, pellets);
        }

        [UnityTest]
        public IEnumerator SemiAutomaticAndBoltRequirePressWhileAutomaticRepeats()
        {
            RangedWeapon gun = CreateGun(10);
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            yield return new WaitForSeconds(0.05f);
            Assert.IsFalse(gun.UseTrigger(true, false, Vector3.forward));
            Assert.AreEqual(9, gun.MagazineAmmo);
            gun.Data.Mode = WeaponData.FireMode.Automatic;
            Assert.IsTrue(gun.UseTrigger(true, false, Vector3.forward));
            yield return new WaitForSeconds(0.05f);
            Assert.IsTrue(gun.UseTrigger(true, false, Vector3.forward));
            yield return new WaitForSeconds(0.05f);
            gun.ReleaseTrigger();
            gun.Data.Mode = WeaponData.FireMode.BoltAction;
            gun.Data.TimeBetweenUse = 0.1f;
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            Assert.AreEqual(WeaponState.Cycling, gun.CurrentState);
            Assert.IsFalse(gun.UseTrigger(true, true, Vector3.forward));
            yield return new WaitForSeconds(0.15f);
            Assert.IsFalse(gun.UseTrigger(true, false, Vector3.forward));
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            Assert.AreEqual(5, gun.MagazineAmmo);
        }

        [UnityTest]
        public IEnumerator WarmupCancelsOnReleaseAndContinuedAutomaticFireWarmsOnlyOnce()
        {
            RangedWeapon gun = CreateGun();
            gun.Data.Mode = WeaponData.FireMode.Automatic;
            gun.Data.TimeDelayBeforeUse = 0.1f;
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            Assert.AreEqual(WeaponState.WarmingUp, gun.CurrentState);
            Assert.AreEqual(6, gun.MagazineAmmo);
            gun.ReleaseTrigger();
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(6, gun.MagazineAmmo);
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(5, gun.MagazineAmmo);
            Assert.IsTrue(gun.UseTrigger(true, false, Vector3.forward));
            Assert.AreEqual(4, gun.MagazineAmmo); // no second warmup while held
            yield return new WaitForSeconds(0.05f);
            gun.ReleaseTrigger();
            Assert.IsTrue(gun.UseTrigger(true, true, Vector3.forward));
            gun.SetUseContext(new WeaponUseContext(true, true, false));
            Assert.AreEqual(WeaponState.Ready, gun.CurrentState);
            Assert.IsFalse(gun.TryAttack(Vector3.forward));
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(4, gun.MagazineAmmo);
            gun.Data.FireOnSprint = true;
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
            gun.enabled = false;
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(4, gun.MagazineAmmo);
        }

        [UnityTest]
        public IEnumerator SingleRoundReloadKeepsCompletedShellAndInterruptsToFire()
        {
            RangedWeapon gun = CreateGun(3, 4);
            var player = (CharacterPlayer)gun.Owner;
            player.CharacterInventory.Slots[0].Content.Weapon.Spend(3);
            gun.Data.MagazineReloadMode = WeaponData.ReloadMode.SingleRound;
            gun.Data.MagazineReloadInterval = 0.12f;
            Assert.IsTrue(gun.TryReload());
            yield return new WaitForSeconds(0.16f);
            Assert.AreEqual(1, gun.MagazineAmmo);
            Assert.AreEqual(3, gun.ReserveAmmo);
            Assert.AreEqual(WeaponState.Reloading, gun.CurrentState);
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
            Assert.AreEqual(0, gun.MagazineAmmo);
            yield return new WaitForSeconds(0.05f);
            Assert.AreEqual(3, gun.ReserveAmmo);
            Assert.IsTrue(gun.TryReload());
            yield return new WaitForSeconds(0.16f);
            gun.CancelAction();
            Assert.AreEqual(1, gun.MagazineAmmo);
            Assert.AreEqual(2, gun.ReserveAmmo);
            yield return new WaitForSeconds(0.16f);
            Assert.AreEqual(1, gun.MagazineAmmo);
        }

        [UnityTest]
        public IEnumerator NoMagazineConsumesBackpackAmmoAndCannotReload()
        {
            RangedWeapon gun = CreateGun(6, 3, false);
            gun.Data.Ammo.AmmoConsumptionPerShoot = 2;
            Assert.AreEqual(0, gun.MagazineAmmo);
            Assert.IsFalse(gun.TryReload());
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
            Assert.AreEqual(1, gun.ReserveAmmo);
            Assert.AreEqual(0, gun.MagazineAmmo);
            yield return new WaitForSeconds(0.05f);
            Assert.IsFalse(gun.TryAttack(Vector3.forward));
            Assert.AreEqual(1, gun.ReserveAmmo);
            Assert.AreEqual(2, gun.AddReserveAmmo(2));
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator AimSlowsMovementOverridesSprintAndUsesAimSpread()
        {
            RangedWeapon gun = CreateGun();
            gun.Data.RecoilValue = 8f;
            gun.Data.RecoilValueOnAim = 1f;
            gun.Data.RecoilMultiplierOnMove = 2f;
            var driver = gun.Owner.gameObject.AddComponent<PlayerInputDriver>();
            driver.enabled = false;
            var aim = gun.Owner.GetComponent<PlayerAim>();
            yield return null;

            Assert.AreEqual(driver.MoveSpeed * driver.SprintMultiplier, driver.GetMoveSpeed(true));
            aim.SetAiming(true);
            Assert.AreEqual(driver.MoveSpeed * 0.4f, driver.GetMoveSpeed(true), 0.001f);
            Vector3 before = gun.Owner.transform.position;
            driver.Move(Vector2.right, true);
            Assert.IsTrue(driver.IsMoving);
            Assert.IsFalse(driver.IsSprinting);
            Assert.AreEqual(driver.GetMoveSpeed(false) * Time.deltaTime,
                gun.Owner.transform.position.x - before.x, 0.002f);
            Assert.AreEqual(2f, gun.CurrentSpread);
            Assert.IsTrue(gun.TryAttack(Vector3.forward));

            aim.SetAiming(false);
            driver.Move(Vector2.right, true);
            Assert.IsTrue(driver.IsSprinting);
            Assert.IsFalse(gun.TryAttack(Vector3.forward));
            aim.SetAiming(true);
            aim.enabled = false;
            Assert.IsFalse(aim.IsAiming);
        }

        [UnityTest]
        public IEnumerator ReloadAudioFollowsMagazineShellStepsAndCancellation()
        {
            RangedWeapon gun = CreateGun(3, 6);
            var audio = gun.gameObject.AddComponent<WeaponReloadAudio>();
            var source = gun.GetComponent<AudioSource>();
            var magazine = AudioClip.Create("magazineReload", 44100, 1, 44100, false);
            var shell = AudioClip.Create("shellReload", 44100, 1, 44100, false);
            _created.Add(magazine);
            _created.Add(shell);
            audio.MagazineReloadClip = magazine;
            audio.SingleRoundReloadClip = shell;
            var stack = ((CharacterPlayer)gun.Owner).CharacterInventory.Slots[0].Content;
            Assert.IsFalse(gun.TryReload());
            Assert.IsNull(source.clip);
            stack.Weapon.Spend(3);
            Assert.IsTrue(gun.TryReload());
            Assert.AreSame(magazine, source.clip);
            gun.CancelAction();
            Assert.IsNull(source.clip);
            Assert.AreEqual(0, gun.MagazineAmmo);

            gun.Data.MagazineReloadMode = WeaponData.ReloadMode.SingleRound;
            gun.Data.MagazineReloadInterval = 0.12f;
            int steps = 0;
            gun.ReloadStepStarted += () => steps++;
            Assert.IsTrue(gun.TryReload());
            Assert.AreSame(shell, source.clip);
            yield return new WaitForSeconds(0.16f);
            Assert.AreEqual(2, steps);
            Assert.AreEqual(1, gun.MagazineAmmo);
            Assert.AreSame(shell, source.clip);
            Assert.IsTrue(gun.TryAttack(Vector3.forward));
            Assert.IsNull(source.clip);
            yield return new WaitForSeconds(0.05f);
            Assert.IsTrue(gun.TryReload());
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(3, gun.MagazineAmmo);
            Assert.IsNull(source.clip);
        }

        [Test]
        public void SpreadUsesAimBaseAndExclusiveMovementOrSprintMultiplier()
        {
            WeaponData data = Asset<WeaponData>();
            data.RecoilValue = 4f;
            data.RecoilValueOnAim = 1f;
            data.RecoilMultiplierOnMove = 2f;
            data.RecoilMultiplierOnSprint = 3f;
            Assert.AreEqual(4f, WeaponSpread.GetAngle(data, default));
            Assert.AreEqual(8f, WeaponSpread.GetAngle(data, new WeaponUseContext(true, false, false)));
            Assert.AreEqual(12f, WeaponSpread.GetAngle(data, new WeaponUseContext(true, true, false)));
            Assert.AreEqual(1f, WeaponSpread.GetAngle(data, new WeaponUseContext(false, false, true)));
            Assert.AreEqual(3f, WeaponSpread.GetAngle(data, new WeaponUseContext(true, true, true)));
            bool above = false;
            bool below = false;
            for (int i = 0; i < 100; i++)
            {
                Vector3 direction = WeaponSpread.Apply(Vector3.forward, 4f);
                Assert.LessOrEqual(Vector3.Angle(Vector3.forward, direction), 4.01f);
                Assert.AreEqual(1f, direction.magnitude, 0.0001f);
                above |= direction.y > 0f;
                below |= direction.y < 0f;
            }
            Assert.IsTrue(above);
            Assert.IsTrue(below);
            Assert.Less(Vector3.Angle(Vector3.up, WeaponSpread.Apply(Vector3.up, 4f)), 4.01f);
        }
    }

    public sealed class TestWeaponAmmo : WeaponAmmo
    {
        public int Hits;
        public DamageSource LastSource;

        public override float OnBulletHit(DamageSource source)
        {
            Hits++;
            LastSource = source;
            return base.OnBulletHit(source);
        }
    }
}
