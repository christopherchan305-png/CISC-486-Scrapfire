using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Inventory.Use;
using Scrapfire.Characters.Player;
using Scrapfire.Characters.Weapon;
using PlayerInventory = Scrapfire.Characters.Inventory.Inventory;

namespace Scrapfire.Tests
{
    public sealed class InventorySystemTests
    {
        private readonly List<Object> _created = new List<Object>();
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>(); _created.Add(asset); return asset;
        }
        private CharacterPlayer Player(string name)
        {
            var go = new GameObject(name); _created.Add(go); return go.AddComponent<CharacterPlayer>();
        }
        private Item Item(Item.Category category, int stack = 60)
        {
            var item = Asset<Item>(); item.ItemCategory = category; item.StackSize = stack;
            if (category == Characters.Inventory.Item.Category.Weapon)
            { item.Weapon = Asset<WeaponData>(); item.Weapon.MagazineCapacity = 5; }
            return item;
        }
        private static InventoryContainer Bag(int slots) => new InventoryContainer.Builder().AddSlots(InventorySlotKind.Backpack, slots).Build();
        [TearDown]
        public void Cleanup()
        {
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void EveryPlayerOwnsAComponentAndIndependentRuntimeSlots()
        {
            var a = Player("A"); var b = Player("B");
            Assert.AreSame(a.Inventory, a.GetComponent<PlayerInventory>());
            Assert.AreSame(a.CharacterInventory, a.Inventory.Contents);
            Assert.AreEqual(2, a.Inventory.WeaponSlots.Count());
            Assert.AreEqual(InventorySlotKind.Armor, a.Inventory.ArmorSlot.Kind);
            Assert.AreEqual(12, a.Inventory.BackpackSlots.Count());
            Assert.AreNotSame(a.CharacterInventory.Slots[3], b.CharacterInventory.Slots[3]);
            var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            a.Inventory.Add(new ItemStack(ammo, 75)); b.Inventory.Add(new ItemStack(ammo, 60));
            a.Inventory.Drop(3, 10);
            Assert.AreEqual(65, a.Inventory.Count(ammo));
            Assert.AreEqual(60, b.Inventory.Count(ammo));
            a.Inventory.enabled = false; a.Inventory.enabled = true;
            Assert.AreEqual(65, a.Inventory.Count(ammo), "Disabling is not a new session or a refill.");
        }

        [Test]
        public void AmmoMedicalAndSuppliesCompeteForTheSameFiniteSlots()
        {
            var bag = Bag(2);
            var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            var heal = Item(Characters.Inventory.Item.Category.Medical, 2);
            var supplies = Item(Characters.Inventory.Item.Category.Supplies, 4);
            Assert.IsTrue(bag.TryAddAll(new ItemStack(ammo, 60)));
            Assert.IsTrue(bag.TryAddAll(new ItemStack(heal, 2)));
            var rejected = new ItemStack(supplies, 1);
            Assert.IsFalse(bag.TryAddAll(rejected));
            Assert.AreEqual(1, rejected.Amount);
            Assert.AreEqual(2, bag.BackpackCapacity);
            bag.Drop(0);
            Assert.IsTrue(bag.TryAddAll(rejected));
            Assert.AreSame(supplies, bag.Slots[0].Content.Data);
        }

        [Test]
        public void BulkPickupStacksAndReturnsOverflowWithoutExpandingOrLosingItems()
        {
            var bag = Bag(2); var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            int changes = 0; bag.Changed += () => changes++;
            var incoming = new ItemStack(ammo, 130);
            Assert.AreSame(incoming, bag.Add(incoming));
            Assert.AreEqual(10, incoming.Amount); Assert.AreEqual(120, bag.Count(ammo));
            Assert.AreEqual(1, changes, "One pickup is one committed change event.");
            var dropped = bag.Drop(0, 10);
            Assert.AreEqual(10, dropped.Amount);
            Assert.IsTrue(bag.TryAddAll(incoming));
            Assert.AreEqual(120, bag.Count(ammo));
            Assert.IsTrue(incoming.IsEmpty);
        }

        [Test]
        public void GeneralSlotsAllowSplitMoveMergeAndSwapAcrossItemCategories()
        {
            var bag = Bag(3); var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            var med = Item(Characters.Inventory.Item.Category.Medical, 2);
            bag.Add(new ItemStack(ammo, 40), 0); bag.Add(new ItemStack(med, 2), 1);
            Assert.IsTrue(bag.TryMove(0, 2, 15));
            Assert.AreEqual(25, bag.Slots[0].Content.Amount); Assert.AreEqual(15, bag.Slots[2].Content.Amount);
            Assert.IsTrue(bag.TrySwap(1, 2));
            Assert.AreSame(med, bag.Slots[2].Content.Data);
            Assert.IsTrue(bag.TryMove(0, 1));
            Assert.AreEqual(40, bag.Slots[1].Content.Amount); Assert.IsTrue(bag.Slots[0].Content.IsEmpty);
            Assert.AreEqual(40, bag.Count(ammo)); Assert.AreEqual(2, bag.Count(med));
        }

        [Test]
        public void EquipmentHasDedicatedLimitsAndReplacementReturnsTheOldWeaponState()
        {
            var inventory = InventoryRules.CreateInventory();
            var gun = Item(Characters.Inventory.Item.Category.Weapon);
            var armor = Item(Characters.Inventory.Item.Category.Armor, 99);
            inventory.Add(new ItemStack(gun)); inventory.Add(new ItemStack(gun));
            var third = new ItemStack(gun);
            Assert.IsFalse(inventory.TryAddAll(third));
            Assert.IsFalse(inventory.Slots[3].Accepts(gun));
            Assert.AreEqual(1, armor.StackLimit);
            Assert.IsTrue(inventory.TryAddAll(new ItemStack(armor)));
            Assert.IsFalse(inventory.TryAddAll(new ItemStack(armor)));
            Assert.IsFalse(inventory.TrySwap(0, 3));
            inventory.Slots[0].Content.Weapon.Spend(2);
            Assert.IsTrue(inventory.TryEquip(0, third, out var displaced));
            Assert.AreEqual(3, displaced.Weapon.MagazineAmmo);
            Assert.IsTrue(third.IsEmpty);
            Assert.IsTrue(inventory.TryEquip(0, displaced, out var replacement));
            Assert.AreEqual(3, inventory.Slots[0].Content.Weapon.MagazineAmmo);
            Assert.AreEqual(5, replacement.Weapon.MagazineAmmo);
        }

        [Test]
        public void ForeignOwnedStacksCannotBeMutatedOrAddedToAnotherInventory()
        {
            var a = Bag(1); var b = Bag(1); var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            a.Add(new ItemStack(ammo, 20));
            Assert.Throws<System.ArgumentException>(() => b.Add(a.Slots[0].Content));
            Assert.Throws<System.InvalidOperationException>(() => a.Slots[0].Content.Reset());
            Assert.Throws<System.InvalidOperationException>(() => a.Slots[0].Content.Split(5));
            Assert.AreEqual(20, a.Count(ammo)); Assert.AreEqual(0, b.Count(ammo));
        }

        [Test]
        public void TransfersAreAtomicAndNotifyOnlyAfterBothInventoriesAreUpdated()
        {
            var a = Bag(1); var b = Bag(1); var ammo = Item(Characters.Inventory.Item.Category.Ammo);
            a.Add(new ItemStack(ammo, 30)); b.Add(new ItemStack(ammo, 50));
            int changes = 0; a.Changed += () => { changes++; Assert.AreEqual(60, b.Count(ammo)); };
            Assert.IsFalse(a.TryTransferTo(b, 0, 11));
            Assert.IsFalse(a.TryTransferTo(b, 0, 40));
            Assert.AreEqual(30, a.Count(ammo)); Assert.AreEqual(50, b.Count(ammo)); Assert.AreEqual(0, changes);
            Assert.IsTrue(a.TryTransferTo(b, 0, 10));
            Assert.AreEqual(20, a.Count(ammo)); Assert.AreEqual(60, b.Count(ammo)); Assert.AreEqual(1, changes);
        }

        [Test]
        public void BackpackExpansionPreservesExistingSlotReferencesAndHasAnUpperLimit()
        {
            var inventory = InventoryRules.CreateInventory(); var first = inventory.Slots.ToArray();
            Assert.IsTrue(inventory.TryExpandBackpack(16));
            for (int i = 0; i < first.Length; i++) Assert.AreSame(first[i], inventory.Slots[i]);
            Assert.IsFalse(inventory.TryExpandBackpack(4));
            Assert.IsTrue(inventory.TryExpandBackpack(24)); Assert.IsFalse(inventory.TryExpandBackpack(25));
            Assert.AreEqual(2, inventory.GetSlots(InventorySlotKind.Weapon).Count());
            Assert.AreEqual(1, inventory.GetSlots(InventorySlotKind.Armor).Count());
        }

        [UnityTest]
        public IEnumerator ConsumableCategoryCanHealFromTheSharedBackpackWithoutChangingWeapons()
        {
            var a = Player("A"); var b = Player("B");
            var med = Item(Characters.Inventory.Item.Category.Consumable, 2);
            var use = Asset<HealingItemUseBehaviour>(); use.HealAmount = 25f; med.UseBehaviour = use;
            a.Inventory.Add(new ItemStack(med, 2)); b.Inventory.Add(new ItemStack(med, 2));
            a.CharacterHealth.Damage(new DamageSource(50f, DamageType.Melee, null, a.CharacterHealth));
            var medical = a.gameObject.AddComponent<MedicalItemUser>();
            var selected = a.SelectedWeaponSlot;
            Assert.IsTrue(medical.UseRecommended());
            Assert.AreEqual(75f, a.CharacterHealth.CurrentHealthValue);
            Assert.AreEqual(1, a.Inventory.Count(med)); Assert.AreEqual(2, b.Inventory.Count(med));
            Assert.AreSame(selected, a.SelectedWeaponSlot);
            yield return null;
        }

        [Test]
        public void AllAmmoIsFiniteAndMovingOrRebindingAWeaponDoesNotRefillIt()
        {
            var inventory = InventoryRules.CreateInventory();
            var ammo = Item(Characters.Inventory.Item.Category.Ammo, 10);
            var gun = Item(Characters.Inventory.Item.Category.Weapon);
            gun.Weapon.MagazineCapacity = 2;
            gun.Weapon.Ammo = Asset<WeaponAmmo>(); gun.Weapon.Ammo.Item = ammo;
            inventory.Add(new ItemStack(gun)); inventory.Add(new ItemStack(ammo, 3));
            var supply = new WeaponAmmoSupply(gun.Weapon, inventory.Slots[0].Content.Weapon, inventory);
            Assert.IsTrue(supply.SpendShot()); Assert.IsTrue(supply.SpendShot()); Assert.IsFalse(supply.SpendShot());
            Assert.AreEqual(2, supply.ReloadStep()); Assert.AreEqual(1, supply.ReserveAmmo);
            Assert.IsTrue(supply.SpendShot()); Assert.IsTrue(supply.SpendShot());
            Assert.AreEqual(1, supply.ReloadStep()); Assert.IsTrue(supply.SpendShot());
            Assert.AreEqual(0, supply.MagazineAmmo); Assert.AreEqual(0, supply.ReserveAmmo); Assert.IsFalse(supply.CanReload);
            Assert.IsTrue(inventory.TryMove(0, 1));
            supply = new WeaponAmmoSupply(gun.Weapon, inventory.Slots[1].Content.Weapon, inventory);
            Assert.IsFalse(supply.CanShoot); Assert.IsFalse(supply.CanReload);
        }
    }
}
