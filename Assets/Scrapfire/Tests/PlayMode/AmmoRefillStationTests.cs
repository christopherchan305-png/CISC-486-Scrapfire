using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Player;
using Scrapfire.Characters.Weapon;
using Scrapfire.Interaction;

namespace Scrapfire.Tests
{
    public sealed class AmmoRefillStationTests
    {
        private Scene _scene;
        private GameObject _other;
        private Item _filler;

        [UnityTest]
        public IEnumerator StationFillsOnlyBuyerReserveAndRejectsFullPoorDistantOrFullBackpack()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scrapfire/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
#endif
            _scene = SceneManager.GetSceneByPath("Assets/Scrapfire/Scenes/SampleScene.unity");
            yield return null;
            var roots = _scene.GetRootGameObjects();
            var player = roots.Single(g => g.name == "Capsule").GetComponent<CharacterPlayer>();
            var station = roots.SelectMany(g => g.GetComponentsInChildren<AmmoRefillStation>()).Single();
            player.GetComponent<PlayerInputDriver>().enabled = false;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = station.transform.position + new Vector3(0f, 0.5f, -1.4f);
            motor.enabled = true;
            Physics.SyncTransforms();
            var weapon = (RangedWeapon)player.CurrentWeapon;
            var ammo = weapon.Data.Ammo.Item;
            var bag = player.CharacterInventory;
            int magazine = weapon.MagazineAmmo;
            int money = player.Balance.Amount;
            Assert.IsFalse(station.TryInteract(player), "Already full must not charge.");
            Assert.AreEqual(money, player.Balance.Amount);
            bag.Remove(ammo, 30);
            _other = new GameObject("Other player");
            var other = _other.AddComponent<CharacterPlayer>();
            other.Inventory.TryAddAll(new ItemStack(ammo, 20));
            Assert.IsTrue(station.TryInteract(player));
            Assert.AreEqual(60, weapon.ReserveAmmo);
            Assert.AreEqual(money - 50, player.Balance.Amount);
            Assert.AreEqual(magazine, weapon.MagazineAmmo, "The crate does not auto-reload.");
            Assert.AreEqual(20, other.Inventory.Count(ammo));
            bag.Remove(ammo, 60);
            player.Balance.TrySpend(player.Balance.Amount);
            Assert.IsFalse(station.TryInteract(player));
            Assert.AreEqual(0, bag.Count(ammo));
            player.Balance.Credit(50);
            _filler = ScriptableObject.CreateInstance<Item>();
            _filler.ItemCategory = Item.Category.Supplies; _filler.StackSize = 1;
            while (bag.TryAddAll(new ItemStack(_filler))) { }
            Assert.IsFalse(station.TryInteract(player), "Full backpack must reject the entire purchase.");
            Assert.AreEqual(50, player.Balance.Amount);
            Assert.AreEqual(0, bag.Count(ammo));
            var occupied = bag.GetItems(Item.Category.Supplies).First();
            bag.Remove(bag.IndexOf(occupied));
            motor.enabled = false; player.transform.position += Vector3.back * 10f; motor.enabled = true;
            Physics.SyncTransforms();
            Assert.IsFalse(station.TryInteract(player), "Remote purchase must fail.");
            Assert.AreEqual(50, player.Balance.Amount);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_other != null) Object.Destroy(_other);
            if (_filler != null) Object.Destroy(_filler);
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
