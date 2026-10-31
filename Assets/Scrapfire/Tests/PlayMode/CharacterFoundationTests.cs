using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Inventory = Scrapfire.Characters.Inventory.InventoryContainer;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Weapon;
using Scrapfire.Characters.Inventory.Use;

namespace Scrapfire.Tests
{
    public class CharacterFoundationTests
    {
        private readonly List<Object> _created = new List<Object>();

        private GameObject CreateObject(string name, Vector3 position = default)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            _created.Add(go);
            return go;
        }

        private HealingItemUseBehaviour CreateHealingBehaviour(float healAmount)
        {
            var behaviour = ScriptableObject.CreateInstance<HealingItemUseBehaviour>();
            behaviour.HealAmount = healAmount;
            _created.Add(behaviour);
            return behaviour;
        }

        private static float Damage(Health health, float amount)
        {
            return health.Damage(new DamageSource(amount, DamageType.Melee, null, health));
        }

        [UnityTest]
        public IEnumerator DamageSourceCarriesAttributionAndRejectsWrongVictim()
        {
            var attacker = CreateObject("attacker");
            var health = CreateObject("victim").AddComponent<Health>();
            var other = CreateObject("other").AddComponent<Health>();
            DamageSource received = default;
            health.Damaged += source => received = source;
            yield return null;
            var damage = new DamageSource(20f, DamageType.Explosive, attacker, health);
            Assert.AreEqual(0f, other.Damage(damage));
            damage.Victim = other.gameObject;
            Assert.AreEqual(0f, health.Damage(damage));
            damage.Victim = health.gameObject;
            Assert.AreEqual(20f, health.Damage(damage));
            Assert.AreEqual(20f, received.Damage);
            Assert.AreEqual(DamageType.Explosive, received.Type);
            Assert.AreSame(attacker, received.CausedBy);
            Assert.AreSame(health.gameObject, received.Victim);
            Assert.AreSame(health, received.HealthOnVictim);
        }

        [UnityTest]
        public IEnumerator CameraFollowsPlayerWithoutFollowingPlayerRotation()
        {
            var player = CreateObject("cameraPlayer", new Vector3(3000f, 1000f, 3000f)).AddComponent<CharacterPlayer>();
            var camera = CreateObject("testCamera").AddComponent<Camera>();
            player.BindCamera(camera);
            var follow = camera.GetComponent<Scrapfire.Characters.Player.TopDownCameraFollow>();
            follow.FollowSpeed = 0f;
            Quaternion originalRotation = camera.transform.rotation;
            player.transform.position += Vector3.right * 4f;
            player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            yield return null;
            yield return null;
            Assert.Less(Vector3.Distance(player.transform.position + follow.Offset, camera.transform.position), 0.001f);
            Assert.Less(Quaternion.Angle(originalRotation, camera.transform.rotation), 0.01f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created) if (obj != null) Object.DestroyImmediate(obj);
            _created.Clear();
        }

        [UnityTest]
        public IEnumerator DowningIsHealthConfigurationIndependentOfCharacterType()
        {
            var player = CreateObject("player").AddComponent<CharacterPlayer>();
            var ai = CreateObject("ai").AddComponent<CharacterAi>();
            player.CharacterHealth.CanBeDowned = false;
            ai.CharacterHealth.CanBeDowned = true;
            int changes = 0;
            ai.CharacterHealth.StateChanged += (previous, current) => changes++;
            yield return null;
            Damage(player.CharacterHealth, 200f);
            Damage(ai.CharacterHealth, 200f);
            Assert.AreEqual(HealthState.Dead, player.HealthState);
            Assert.AreEqual(HealthState.Downed, ai.HealthState);
            Assert.IsFalse(player.CharacterHealth.Revive(25f));
            Assert.IsTrue(ai.CharacterHealth.Revive(25f));
            Assert.AreEqual(2, changes);
            Assert.AreEqual(HealthState.Dead, player.HealthState);
            ai.CharacterHealth.Kill();
            Assert.IsFalse(ai.CharacterHealth.Kill());
            Assert.IsFalse(ai.CharacterHealth.Revive(25f));
        }

        [UnityTest]
        public IEnumerator PlayerDownsAndRevivesButAiDies()
        {
            var player = CreateObject("player").AddComponent<CharacterPlayer>();
            var ai = CreateObject("ai").AddComponent<CharacterAi>();
            player.CharacterHealth.CanBeDowned = true;
            yield return null;
            Assert.IsNotNull(player.CharacterInventory);
            Assert.AreEqual(100f, Damage(player.CharacterHealth, 200f));
            Assert.AreEqual(HealthState.Downed, player.HealthState);
            Assert.IsFalse(player.CanAct);
            Assert.AreEqual(0f, player.CharacterHealth.Heal(10f));
            Assert.IsTrue(player.CharacterHealth.Revive(25f));
            Assert.AreEqual(25f, player.CharacterHealth.CurrentHealthValue);
            Damage(ai.CharacterHealth, 200f);
            Assert.AreEqual(HealthState.Dead, ai.HealthState);
            Assert.IsFalse(ai.CharacterHealth.Revive(25f));
            ai.CharacterHealth.ResetHealth();
            Assert.AreEqual(HealthState.Alive, ai.HealthState);
        }

        [UnityTest]
        public IEnumerator HealthRejectsInvalidDamageAndClampsHealing()
        {
            var health = CreateObject("health").AddComponent<Health>();
            yield return null;
            Assert.AreEqual(0f, Damage(health, float.NaN));
            Assert.AreEqual(0f, Damage(health, float.PositiveInfinity));
            Assert.AreEqual(0f, Damage(health, -10f));
            health.ImmuneToDamage = true;
            Assert.AreEqual(0f, Damage(health, 10f));
            health.ImmuneToDamage = false;
            Damage(health, 30f);
            Assert.AreEqual(30f, health.Heal(50f));
            Assert.AreEqual(100f, health.CurrentHealthValue);
        }

        private Item CreateItem(Item.Category category, int stackSize = 10)
        {
            var item = ScriptableObject.CreateInstance<Item>();
            item.ItemCategory = category;
            item.StackSize = stackSize;
            _created.Add(item);

            if (category == Item.Category.Weapon)
            {
                item.Weapon = CreateWeaponData();
            }

            return item;
        }

        private WeaponData CreateWeaponData(Item ammoItem = null)
        {
            var data = ScriptableObject.CreateInstance<WeaponData>();
            var ammo = ScriptableObject.CreateInstance<WeaponAmmo>();
            _created.Add(data);
            _created.Add(ammo);
            ammo.Item = ammoItem != null ? ammoItem : CreateItem(Item.Category.Ammo, 100);
            data.Ammo = ammo;
            data.RecoilValue = 0f;
            data.RecoilValueOnAim = 0f;
            return data;
        }

        private static Inventory ResourceInventory(int count)
        {
            return new Inventory.Builder()
                .AddSlots(InventorySlotKind.Backpack, count, false)
                .Build();
        }

        [Test]
        public void AddRespectsCategoriesMergesFirstAndReturnsRemainder()
        {
            var inventory = new Inventory.Builder()
                .AddSlots(InventorySlotKind.Weapon, 1, false)
                .AddSlots(InventorySlotKind.Backpack, 2, false)
                .Build();
            var ammo = CreateItem(Item.Category.Ammo);
            var weapon = CreateItem(Item.Category.Weapon);
            var misplaced = new ItemStack(ammo, 6);
            Assert.AreSame(misplaced, inventory.Add(misplaced, 0));
            Assert.AreEqual(6, misplaced.Amount);
            Assert.AreSame(ItemStack.EMPTY, inventory.Add(misplaced, 2));
            var incoming = new ItemStack(ammo, 17);
            Assert.AreSame(incoming, inventory.Add(incoming));
            Assert.AreEqual(3, incoming.Amount);
            Assert.AreEqual(10, inventory.Slots[1].Content.Amount);
            Assert.AreEqual(10, inventory.Slots[2].Content.Amount);
            Assert.AreSame(ItemStack.EMPTY, inventory.Slots[0].Content);
            Assert.AreSame(ItemStack.EMPTY, inventory.Add(new ItemStack(weapon)));
            Assert.AreEqual(1, inventory.Count(weapon));
            Assert.AreEqual(20, inventory.Count(ammo));
        }

        [Test]
        public void GeneralSlotsUseItemLimitsAndEquipmentCountsAreFixed()
        {
            var inventory = ResourceInventory(1);
            var ammo = CreateItem(Item.Category.Ammo, 8);
            var incoming = inventory.Add(new ItemStack(ammo, 10));
            Assert.AreEqual(2, incoming.Amount);
            Assert.AreEqual(8, inventory.Slots[0].Content.Amount);
            int slot = inventory.AddSlot(InventorySlotKind.Backpack);
            Assert.AreEqual(1, slot);
            Assert.AreSame(ItemStack.EMPTY, inventory.Add(incoming));
            Assert.AreEqual(10, inventory.Count(ammo));
            var playerInventory = InventoryRules.CreateInventory();
            Assert.AreEqual(12, playerInventory.BackpackCapacity);
            Assert.IsTrue(playerInventory.TryExpandBackpack(14));
            Assert.IsFalse(playerInventory.TryExpandBackpack(14));
            Assert.IsFalse(playerInventory.TryExpandBackpack(25));
            Assert.IsFalse(playerInventory.CanAddSlot(InventorySlotKind.Weapon));
            Assert.Throws<System.ArgumentException>(() => playerInventory.AddSlot(InventorySlotKind.Weapon));
            Assert.Throws<System.ArgumentException>(() => inventory.AddSlot((InventorySlotKind)999));
        }

        [Test]
        public void BuilderProducesIndependentSlotsAndLocksBlockStorage()
        {
            var builder = new Inventory.Builder()
                .AddSlots(InventorySlotKind.Backpack, 1, false)
                .AddSlots(InventorySlotKind.Backpack, 1, true);

            var first = builder.Build();
            var second = builder.Build();
            Assert.AreNotSame(first.Slots[0], second.Slots[0]);
            Assert.AreEqual(InventorySlotKind.Backpack, first.Slots[1].Kind);
            var ammo = CreateItem(Item.Category.Ammo, 10);
            var remaining = first.Add(new ItemStack(ammo, 15));
            Assert.AreEqual(5, remaining.Amount);
            Assert.AreSame(remaining, first.Add(remaining, 1));
            Assert.AreSame(ItemStack.EMPTY, first.Remove(1));
            Assert.IsFalse(first.Take(new ItemStack(ammo, 11)));
            Assert.AreEqual(10, first.Count(ammo));
            int changes = 0;
            first.Changed += () => changes++;
            Assert.IsTrue(first.UnlockSlot(1));
            Assert.IsFalse(first.UnlockSlot(1));
            Assert.AreEqual(1, changes);
            Assert.AreSame(ItemStack.EMPTY, first.Add(remaining));
            Assert.AreEqual(15, first.Count(ammo));
            Assert.IsTrue(second.Slots[1].Locked);
            Assert.AreSame(ItemStack.EMPTY, second.Slots[0].Content);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => builder.AddSlots(InventorySlotKind.Backpack, 0, false));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => builder.AddSlots((InventorySlotKind)999, 1, false));
            Assert.Throws<System.ArgumentException>(() => builder.AddSlots(InventorySlotKind.Weapon, 5, false));
        }

        [Test]
        public void RemovePreservesStableSlotsAndEmptySingleton()
        {
            var inventory = ResourceInventory(3);
            var ammo = CreateItem(Item.Category.Ammo);
            inventory.Add(new ItemStack(ammo, 6), 2);
            var taken = inventory.Remove(2, 2);
            Assert.AreEqual(2, taken.Amount);
            Assert.AreEqual(4, inventory.Slots[2].Content.Amount);
            Assert.AreSame(ItemStack.EMPTY, inventory.Slots[0].Content);
            Assert.AreEqual(4, inventory.Remove(2, 99).Amount);
            Assert.AreSame(ItemStack.EMPTY, inventory.Slots[2].Content);
            Assert.AreSame(ItemStack.EMPTY, inventory.Remove(2));
            Assert.AreSame(ItemStack.EMPTY, ItemStack.EMPTY.Copy());
            Assert.AreSame(ItemStack.EMPTY, ItemStack.EMPTY.CopyAndReset());
            Assert.AreEqual(0, ItemStack.EMPTY.Amount);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => inventory.Remove(-1));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => inventory.Remove(0, 0));
            Assert.Throws<System.ArgumentNullException>(() => inventory.Add(null));
        }

        [Test]
        public void WeaponStateSurvivesRemovalAndTransferBetweenInventories()
        {
            var weapon = CreateItem(Item.Category.Weapon);
            var first = InventoryRules.CreateInventory();
            var second = InventoryRules.CreateInventory();
            first.Add(new ItemStack(weapon), 0);
            var original = first.Slots[0].Content;
            original.Weapon.SpendRound();
            var removed = first.Remove(0);
            Assert.AreSame(original, removed);
            var snapshot = removed.Copy();
            Assert.AreNotSame(snapshot.Weapon, removed.Weapon);
            Assert.AreSame(ItemStack.EMPTY, second.Add(removed, 0));
            Assert.AreEqual(11, second.Slots[0].Content.Weapon.MagazineAmmo);
            Assert.IsTrue(removed.IsEmpty);
            second.Slots[0].Content.Weapon.SpendRound();
            Assert.AreEqual(11, snapshot.Weapon.MagazineAmmo);
        }

        [Test]
        public void TakeIsAtomicAndMatchesInstanceDurability()
        {
            var inventory = InventoryRules.CreateInventory();
            var weapon = CreateItem(Item.Category.Weapon);
            weapon.Durability = 10f;
            inventory.Add(new ItemStack(weapon, durability: 4f), 0);
            inventory.Add(new ItemStack(weapon, durability: 2f), 1);
            Assert.IsFalse(inventory.Take(new ItemStack(weapon, durability: 3f)));
            Assert.AreEqual(2, inventory.Count(weapon));
            var request = new ItemStack(weapon, durability: 4f);
            Assert.IsTrue(inventory.Take(request));
            Assert.AreEqual(1, request.Amount);
            Assert.AreSame(ItemStack.EMPTY, inventory.Slots[0].Content);
            Assert.AreEqual(2f, inventory.Slots[1].Content.Durability);
            Assert.IsTrue(inventory.Take(ItemStack.EMPTY));
            var resources = ResourceInventory(2);
            var ammo = CreateItem(Item.Category.Ammo);
            resources.Add(new ItemStack(ammo, 13));
            Assert.IsFalse(resources.Take(new ItemStack(ammo, 14)));
            Assert.AreEqual(13, resources.Count(ammo));
            Assert.IsTrue(resources.Take(new ItemStack(ammo, 11)));
            Assert.AreEqual(2, resources.Count(ammo));
        }

        [Test]
        public void StorageNotificationsDoNotDependOnWeaponSelection()
        {
            var inventory = ResourceInventory(2);
            var ammo = CreateItem(Item.Category.Ammo);
            int changes = 0;
            inventory.Changed += () => changes++;
            inventory.Add(new ItemStack(ammo, 2));
            inventory.Add(new ItemStack(ammo, 3), 1);
            Assert.AreEqual(2, inventory.Slots[0].Content.Amount);
            Assert.AreEqual(3, inventory.Slots[1].Content.Amount);
            Assert.Throws<System.ArgumentException>(() => inventory.Add(inventory.Slots[0].Content, 1));
            var taken = inventory.Remove(1, 2);
            Assert.AreEqual(2, taken.Amount);
            Assert.AreEqual(3, changes);
            Assert.AreEqual(3, changes);
        }

        [UnityTest]
        public IEnumerator InventoryOwnsStacksAndSelectionAppliesAndRemovesEffects()
        {
            var item = CreateItem(Item.Category.Weapon);
            item.MoveSpeedMultiplier = 1.5f;
            item.Durability = 2f;
            item.StackSize = 99;
            Assert.AreEqual(1, item.StackLimit);
            var first = CreateObject("firstPlayer").AddComponent<CharacterPlayer>();
            var second = CreateObject("secondPlayer").AddComponent<CharacterPlayer>();
            first.CharacterInventory.Add(new ItemStack(item));
            second.CharacterInventory.Add(new ItemStack(item));
            Assert.AreNotSame(first.SelectedWeaponSlot.Content, second.SelectedWeaponSlot.Content);
            Assert.AreEqual(1.5f, first.MoveSpeedMultiplier);
            first.SelectWeapon(1);
            Assert.AreSame(ItemStack.EMPTY, first.SelectedWeaponSlot.Content);
            Assert.AreEqual(1f, first.MoveSpeedMultiplier);
            first.SelectWeapon(0);
            first.CharacterInventory.Remove(0);
            Assert.AreSame(ItemStack.EMPTY, first.SelectedWeaponSlot.Content);
            Assert.AreEqual(1f, first.MoveSpeedMultiplier);
            Assert.AreEqual(2f, second.SelectedWeaponSlot.Content.Durability);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ConsumableOnlyUsesQuantityWhenItHeals()
        {
            var item = CreateItem(Item.Category.Medical);
            item.UseBehaviour = CreateHealingBehaviour(25f);
            item.StackSize = 5;
            var player = CreateObject("consumer").AddComponent<CharacterPlayer>();
            player.CharacterInventory.Add(new ItemStack(item, 2), 4);
            var medical = player.gameObject.AddComponent<Scrapfire.Characters.Player.MedicalItemUser>();
            Assert.IsFalse(medical.UseRecommended());
            Assert.AreEqual(2, player.CharacterInventory.Slots[4].Content.Amount);
            Damage(player.CharacterHealth, 50f);
            Assert.IsTrue(medical.UseRecommended());
            Assert.AreEqual(75f, player.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(1, player.CharacterInventory.Slots[4].Content.Amount);
            Assert.IsTrue(medical.UseRecommended());
            Assert.AreSame(ItemStack.EMPTY, player.CharacterInventory.Slots[4].Content);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItemUserDispatchesBehaviourAndRejectsUnavailableOrForeignSlots()
        {
            var player = CreateObject("itemUser").AddComponent<CharacterPlayer>();
            var other = CreateObject("otherItemUser").AddComponent<CharacterPlayer>();
            var user = player.gameObject.AddComponent<Scrapfire.Characters.Player.ItemUser>();
            var item = CreateItem(Item.Category.Ammo, 10);
            item.UseBehaviour = CreateHealingBehaviour(25f);
            player.CharacterInventory.Add(new ItemStack(item, 2));
            other.CharacterInventory.Add(new ItemStack(item, 1));
            var slot = player.CharacterInventory.GetItems(Item.Category.Ammo).GetEnumerator();
            Assert.IsTrue(slot.MoveNext());
            var owned = slot.Current;
            slot.Dispose();
            var foreign = other.CharacterInventory.GetItems(Item.Category.Ammo).Single();

            Assert.IsFalse(user.TryUse(owned)); // full health
            Damage(player.CharacterHealth, 50f);
            Assert.IsFalse(user.TryUse(foreign));
            Assert.IsFalse(user.TryUse(player.CharacterInventory.Slots[7])); // empty general slot
            Assert.AreEqual(50f, player.CharacterHealth.CurrentHealthValue);
            Assert.IsTrue(user.TryUse(owned)); // dispatch is not tied to Medical category
            Assert.AreEqual(75f, player.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(1, owned.Content.Amount);
            Assert.AreEqual(1, foreign.Content.Amount);
            Assert.IsNull(owned.Content.Data.Model); // use needs no scene representation
            user.enabled = false;
            Assert.IsFalse(user.TryUse(owned));
            user.enabled = true;
            Damage(player.CharacterHealth, 100f);
            Assert.IsFalse(user.TryUse(owned));
            Assert.AreEqual(1, owned.Content.Amount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ItemUseFailureDoesNotConsumeAndReusableBehaviourPreservesQuantity()
        {
            var player = CreateObject("reusableItemUser").AddComponent<CharacterPlayer>();
            var user = player.gameObject.AddComponent<Scrapfire.Characters.Player.ItemUser>();
            var item = CreateItem(Item.Category.Attachment);
            var behaviour = ScriptableObject.CreateInstance<TestItemUseBehaviour>();
            _created.Add(behaviour);
            item.UseBehaviour = behaviour;
            player.CharacterInventory.Add(new ItemStack(item, 1));
            var slot = player.CharacterInventory.GetItems(Item.Category.Attachment).Single();

            behaviour.Consumption = 1;
            Assert.IsTrue(user.CanUse(slot));
            Assert.IsFalse(user.TryUse(slot)); // effect failed after CanUse
            Assert.AreEqual(1, slot.Content.Amount);
            behaviour.Succeeds = true;
            behaviour.Consumption = 0;
            Assert.IsTrue(user.TryUse(slot));
            Assert.IsTrue(user.TryUse(slot));
            Assert.AreEqual(1, slot.Content.Amount);
            behaviour.Consumption = 2;
            Assert.IsFalse(user.TryUse(slot)); // insufficient quantity
            behaviour.Consumption = 1;
            Assert.IsTrue(user.TryUse(slot));
            Assert.AreSame(ItemStack.EMPTY, slot.Content);
            Assert.IsFalse(user.TryUse(slot));
            yield return null;
        }

        [UnityTest]
        public IEnumerator MedicalTapHoldAndPanelCancellationDoNotChangeEquippedWeapon()
        {
            var player = CreateObject("shortcutPlayer").AddComponent<CharacterPlayer>();
            var weapon = CreateItem(Item.Category.Weapon);
            player.CharacterInventory.Add(new ItemStack(weapon));
            var small = CreateItem(Item.Category.Medical);
            small.UseBehaviour = CreateHealingBehaviour(25f);
            var large = CreateItem(Item.Category.Medical);
            large.UseBehaviour = CreateHealingBehaviour(75f);
            player.CharacterInventory.Add(new ItemStack(small, 3));
            player.CharacterInventory.Add(new ItemStack(large, 2));
            var controls = player.gameObject.AddComponent<Scrapfire.Characters.Player.PlayerInventoryControls>();
            var medical = player.GetComponent<Scrapfire.Characters.Player.MedicalItemUser>();
            var equipped = player.SelectedWeaponSlot;
            int opened = 0, closed = 0;
            controls.PanelVisibilityChanged += (panel, visible) => { if (visible) opened++; else closed++; };
            Damage(player.CharacterHealth, 20f);
            Assert.AreSame(small, medical.Recommend().Content.Data);
            controls.MedicalPressed(0f);
            controls.MedicalReleased(0.1f);
            Assert.AreEqual(100f, player.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(2, player.CharacterInventory.Count(small));
            Assert.AreSame(equipped, player.SelectedWeaponSlot);
            Damage(player.CharacterHealth, 60f);
            Assert.AreSame(large, medical.Recommend().Content.Data);
            controls.MedicalPressed(1f);
            controls.MedicalHeld(1.5f);
            Assert.IsTrue(controls.BlocksGameplay);
            Assert.AreEqual(Scrapfire.Characters.Player.PlayerInventoryControls.Panel.MedicalWheel, controls.ActivePanel);
            controls.MedicalReleased(1.6f);
            Assert.IsFalse(controls.BlocksGameplay);
            Assert.AreEqual(40f, player.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(2, player.CharacterInventory.Count(large));
            Assert.AreEqual(1, opened);
            Assert.AreEqual(1, closed);
            controls.MedicalPressed(2f);
            controls.ShowPanel(Scrapfire.Characters.Player.PlayerInventoryControls.Panel.Inventory);
            controls.MedicalReleased(2.1f);
            Assert.AreEqual(40f, player.CharacterHealth.CurrentHealthValue);
            controls.ClosePanels();
            Assert.IsFalse(controls.BlocksGameplay);
            Assert.AreSame(equipped, player.SelectedWeaponSlot);
            Assert.IsTrue(medical.UseRecommended());
            Assert.AreEqual(100f, player.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(1, player.CharacterInventory.Count(large));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReloadUsesInventoryAmmoOnlyAtCompletionAndWeaponRecreationKeepsMagazine()
        {
            var ammo = CreateItem(Item.Category.Ammo, 100);
            var weaponItem = CreateItem(Item.Category.Weapon);
            var prefab = CreateObject("rifleTemplate", new Vector3(8000f, 8000f, 8000f)).AddComponent<RangedWeapon>();
            prefab.Data = CreateWeaponData(ammo);
            prefab.Data.TimeBetweenUse = 0.01f;
            prefab.Data.MagazineReloadInterval = 0.03f;
            weaponItem.WeaponPrefab = prefab;
            weaponItem.Weapon = prefab.Data;
            weaponItem.Weapon.MagazineCapacity = 4;
            var player = CreateObject("inventoryShooter", new Vector3(7000f, 7000f, 7000f)).AddComponent<CharacterPlayer>();
            player.CharacterInventory.Add(new ItemStack(ammo, 3));
            var initialWeapon = new ItemStack(weaponItem);
            initialWeapon.Weapon.Spend(2);
            player.CharacterInventory.Add(initialWeapon);
            var weapon = (RangedWeapon)player.CurrentWeapon;
            Assert.AreEqual(2, weapon.MagazineAmmo);
            Assert.AreEqual(3, weapon.ReserveAmmo);
            Assert.IsTrue(weapon.TryReload());
            Assert.AreEqual(3, player.CharacterInventory.Count(ammo));
            player.SelectWeapon(1);
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(3, player.CharacterInventory.Count(ammo));
            player.SelectWeapon(0);
            weapon = (RangedWeapon)player.CurrentWeapon;
            Assert.AreEqual(2, weapon.MagazineAmmo);
            Assert.IsTrue(weapon.TryReload());
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(4, weapon.MagazineAmmo);
            Assert.AreEqual(1, player.CharacterInventory.Count(ammo));
            Assert.IsTrue(player.TryAttack(Vector3.forward));
            Assert.AreEqual(3, player.SelectedWeaponSlot.Content.Weapon.MagazineAmmo);
            player.SelectWeapon(1);
            player.SelectWeapon(0);
            weapon = (RangedWeapon)player.CurrentWeapon;
            Assert.AreEqual(3, weapon.MagazineAmmo);
            // Weapon indices are independent of absolute backpack slot indices.
            int purchased = 1;
            Assert.IsFalse(player.SelectWeapon(2));
            var pickedUp = player.CharacterInventory.Remove(0);
            player.CharacterInventory.Add(pickedUp, purchased);
            Assert.IsNull(player.CurrentWeapon);
            Assert.IsTrue(player.CycleWeapon(1));
            Assert.AreSame(player.CharacterInventory.Slots[purchased], player.SelectedWeaponSlot);
            Assert.AreEqual(3, ((RangedWeapon)player.CurrentWeapon).MagazineAmmo);
        }

        [UnityTest]
        public IEnumerator MeleeDamagesMultipleCollidersOnlyOnceAndExcludesOwner()
        {
            Vector3 origin = new Vector3(1000f, 1000f, 1000f);
            var owner = CreateObject("owner", origin).AddComponent<CharacterAi>();
            owner.gameObject.AddComponent<SphereCollider>();
            var weapon = owner.gameObject.AddComponent<MeleeWeapon>();
            owner.EquipWeapon(weapon);
            weapon.Range = 2f;
            var target = CreateObject("target", origin + Vector3.forward).AddComponent<CharacterAi>();
            target.gameObject.AddComponent<SphereCollider>().radius = 0.2f;
            var secondCollider = CreateObject("secondCollider");
            secondCollider.transform.SetParent(target.transform, false);
            secondCollider.AddComponent<SphereCollider>().radius = 0.2f;
            yield return null;
            Physics.SyncTransforms();
            Assert.IsTrue(owner.TryAttack(Vector3.forward));
            Assert.AreEqual(80f, target.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(100f, owner.CharacterHealth.CurrentHealthValue);
            Assert.IsFalse(owner.TryAttack(Vector3.forward));
        }

        [UnityTest]
        public IEnumerator RangedWeaponRespectsWallsAmmoReloadAndDeathCancellation()
        {
            Vector3 origin = new Vector3(2000f, 2000f, 2000f);
            var owner = CreateObject("owner", origin).AddComponent<CharacterAi>();
            owner.CharacterHealth.CanBeDowned = true;
            owner.gameObject.AddComponent<SphereCollider>();
            var weapon = owner.gameObject.AddComponent<RangedWeapon>();
            weapon.Data = CreateWeaponData();
            weapon.Data.TimeBetweenUse = 0.01f;
            weapon.Data.MagazineReloadInterval = 0.01f;
            owner.EquipWeapon(weapon);
            var target = CreateObject("target", origin + Vector3.forward * 3f).AddComponent<CharacterAi>();
            target.gameObject.AddComponent<SphereCollider>();
            var wall = CreateObject("wall", origin + Vector3.forward);
            wall.AddComponent<BoxCollider>();
            yield return null;
            Physics.SyncTransforms();
            Assert.IsFalse(owner.TryAttack(Vector3.zero));
            Assert.IsTrue(owner.TryAttack(Vector3.forward));
            Assert.AreEqual(100f, target.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(11, weapon.MagazineAmmo);
            wall.SetActive(false);
            yield return new WaitForSeconds(0.04f);
            Physics.SyncTransforms();
            Assert.IsTrue(owner.TryAttack(Vector3.forward));
            Assert.AreEqual(80f, target.CharacterHealth.CurrentHealthValue);
            yield return new WaitForSeconds(0.04f);
            Assert.IsTrue(weapon.TryReload());
            Assert.IsFalse(owner.TryAttack(Vector3.forward));
            yield return new WaitForSeconds(0.04f);
            Assert.AreEqual(12, weapon.MagazineAmmo);
            Assert.AreEqual(58, weapon.ReserveAmmo);
            Assert.IsTrue(owner.TryAttack(Vector3.forward));
            yield return new WaitForSeconds(0.04f);
            weapon.Data.MagazineReloadInterval = 10f;
            Assert.IsTrue(weapon.TryReload());
            Damage(owner.CharacterHealth, 1000f);
            Assert.AreEqual(WeaponState.Ready, weapon.CurrentState);
            Assert.AreEqual(11, weapon.MagazineAmmo);
            Assert.IsFalse(owner.TryAttack(Vector3.forward));
        }
    }

    public sealed class TestItemUseBehaviour : ItemUseBehaviour
    {
        public bool Succeeds;
        public int Consumption;

        public override int ConsumedAmount => Consumption;

        public override bool CanUse(CharacterPlayer user, ItemStack stack)
        {
            return true;
        }

        protected override bool Use(CharacterPlayer user, ItemStack stack)
        {
            return Succeeds;
        }
    }

}
