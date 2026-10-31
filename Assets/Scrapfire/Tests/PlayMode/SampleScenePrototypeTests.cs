using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Characters.Weapon;
using Scrapfire.Characters.Inventory;

namespace Scrapfire.Tests
{
    public class SampleScenePrototypeTests
    {
        private Scene _scene;

        [UnityTest]
        public IEnumerator SavedScenePlayerMovesCameraFollowsAndFiveShotsKillEnemy()
        {
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/Scrapfire/Scenes/SampleScene.unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            yield return SceneManager.LoadSceneAsync("SampleScene", LoadSceneMode.Additive);
#endif
            yield return null; // scene-only initial supplies run in Start
            _scene = SceneManager.GetSceneByPath("Assets/Scrapfire/Scenes/SampleScene.unity");
            Assert.IsTrue(_scene.IsValid());
            var roots = _scene.GetRootGameObjects();
            var player = roots.Single(go => go.name == "Capsule").GetComponent<CharacterPlayer>();
            var enemy = roots.Single(go => go.name == "Enemy01").GetComponent<CharacterAi>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(enemy);
            Assert.IsNotNull(player.PlayerCamera);
            // Weapon/movement regression is independent of the new enemy behaviour loop.
            foreach (var brain in roots.SelectMany(g => g.GetComponentsInChildren<Scrapfire.Enemies.AI.EnemyBrain>())) brain.enabled = false;
            var input = player.GetComponent<PlayerInputDriver>();
            Assert.IsNotNull(input);
            // Drive the same movement entry point without depending on a physical keyboard.
            input.enabled = false;
            var follow = player.PlayerCamera.GetComponent<TopDownCameraFollow>();
            Assert.AreSame(player.transform, follow.Target);
            follow.FollowSpeed = 0f;
            Vector3 start = player.transform.position;
            for (int i = 0; i < 5; i++)
            {
                input.Move(Vector2.up);
                yield return null;
            }
            Assert.Greater(player.transform.position.z, start.z);
            float beforeWalk = player.transform.position.x;
            float walkDeltaTime = Time.deltaTime;
            input.Move(Vector2.right);
            float walked = player.transform.position.x - beforeWalk;
            yield return null;
            float beforeSprint = player.transform.position.x;
            float sprintDeltaTime = Time.deltaTime;
            input.Move(Vector2.right, true);
            float sprinted = player.transform.position.x - beforeSprint;
            Assert.AreEqual(input.SprintMultiplier, (sprinted / sprintDeltaTime) / (walked / walkDeltaTime), 0.05f);
            yield return null;
            Assert.Less(Vector3.Distance(player.transform.position + follow.Offset, player.PlayerCamera.transform.position), 0.001f);

            // Keep this weapon check on open ground: the saved player now starts
            // outdoors, while the sample enemy remains inside the locked building.
            enemy.transform.position = player.transform.position + Vector3.right * 8f;
            var weapon = player.CurrentWeapon as RangedWeapon;
            Assert.IsNotNull(weapon);
            weapon.SetUseContext(default);
            weapon.Data.RecoilValue = 0f;
            int hits = 0;
            DamageSource lastDamage = default;
            enemy.CharacterHealth.Damaged += source => { hits++; lastDamage = source; };
            Vector3 facing = enemy.transform.position - player.transform.position;
            facing.y = 0f;
            player.transform.rotation = Quaternion.LookRotation(facing);
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++)
            {
                Vector3 direction = enemy.transform.position - weapon.AttackOrigin.position;
                Assert.IsTrue(player.TryAttack(direction), "Shot " + (i + 1) + " should fire.");
                yield return new WaitForSeconds(weapon.AttackInterval + 0.05f);
            }
            Assert.AreEqual(5, hits);
            Assert.AreEqual(7, weapon.MagazineAmmo);
            Assert.AreEqual(HealthState.Dead, enemy.HealthState);
            Assert.AreEqual(0f, enemy.CharacterHealth.CurrentHealthValue);
            Assert.IsFalse(enemy.gameObject.activeSelf);
            Assert.AreEqual(DamageType.Projectile, lastDamage.Type);
            Assert.AreSame(player.gameObject, lastDamage.CausedBy);
            Assert.AreSame(enemy.gameObject, lastDamage.Victim);
            Assert.AreSame(enemy.CharacterHealth, lastDamage.HealthOnVictim);
            Assert.AreEqual(2, player.Inventory.WeaponSlots.Count());
            Assert.AreEqual(12, player.Inventory.BackpackCapacity);
            Assert.AreEqual(1, player.Inventory.WeaponSlots.Count(slot => !slot.Content.IsEmpty));
            // A second weapon is now a pickup, never an automatic starting grant.
#if UNITY_EDITOR
            var shotgun = UnityEditor.AssetDatabase.LoadAssetAtPath<Item>("Assets/Scrapfire/Items/PrototypeShotgun.asset");
            Assert.IsTrue(player.CharacterInventory.TryAddAll(new ItemStack(shotgun)));
#endif
            player.SelectWeapon(1);
            Assert.IsNotNull(player.CurrentWeapon);
            Assert.AreEqual(WeaponData.ReloadMode.SingleRound, ((RangedWeapon)player.CurrentWeapon).Data.MagazineReloadMode);
            Assert.IsFalse(weapon.gameObject.activeSelf);
            player.SelectWeapon(0);
            Assert.AreNotSame(weapon, player.CurrentWeapon);
            weapon = player.CurrentWeapon as RangedWeapon;
            Assert.AreEqual(7, weapon.MagazineAmmo);
            Assert.IsTrue(weapon.gameObject.activeSelf);
            Assert.AreEqual(60, weapon.ReserveAmmo);
            Assert.IsTrue(weapon.TryReload());
            yield return new WaitForSeconds(weapon.ReloadDuration + 0.05f);
            Assert.AreEqual(12, weapon.MagazineAmmo);
            Assert.AreEqual(55, weapon.ReserveAmmo);
            weapon.Data.RecoilValue = 2f;
        }

        [UnityTearDown]
        public IEnumerator CleanupScene()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
