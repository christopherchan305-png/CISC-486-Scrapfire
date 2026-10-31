using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Economy;
using Scrapfire.Enemies;
using Scrapfire.Interaction;
using Scrapfire.Levels;
using Scrapfire.Session;
using Scrapfire.Spawning;
using Scrapfire.Waves;

namespace Scrapfire.Tests
{
    public sealed class SurvivalSystemsTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private GameObject Go(string name, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            _objects.Add(go);
            return go;
        }
        private T Asset<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _objects.Add(asset);
            return asset;
        }
        private CharacterPlayer Player(string name)
        {
            var player = Go(name).AddComponent<CharacterPlayer>();
            player.gameObject.AddComponent<PlayerInteractor>().ReadLocalInput = false;
            return player;
        }
        private MapRegion Region()
        {
            var go = Go("region", false);
            var region = go.AddComponent<MapRegion>();
            go.AddComponent<RegionUnlockInteraction>();
            region.UnlockCost = 400;
            region.Volumes = new[] {
                new RegionVolume(new Vector3(5f, 0f, 0f), new Vector3(4f, 8f, 4f)),
                new RegionVolume(new Vector3(5f, 0f, 4f), new Vector3(4f, 8f, 4f))
            };
            go.SetActive(true);
            return region;
        }
        private static bool Purchase(MapRegion region, CharacterPlayer player)
            => region.GetComponent<RegionUnlockInteraction>().TryInteract(player);
        private Enemy Template()
        {
            var go = Go("enemyTemplate", false);
            var enemy = go.AddComponent<Enemy>();
            enemy.Definition = Asset<EnemyDefinition>();
            go.AddComponent<EnemyBounty>();
            return enemy;
        }
        private EntitySpawner Spawner()
        {
            var spawner = Go("spawner").AddComponent<EntitySpawner>();
            var a = Go("pointA").AddComponent<EnemySpawnPoint>();
            var b = Go("pointB").AddComponent<EnemySpawnPoint>();
            b.transform.position = Vector3.right * 5f;
            spawner.Points = new[] { a, b };
            return spawner;
        }
        private WaveDirector Director(CharacterPlayer player, Enemy prefab, EntitySpawner spawner, int count = 2)
        {
            var roster = Go("roster").AddComponent<SessionPlayers>();
            roster.Register(player);
            var go = Go("director", false);
            var director = go.AddComponent<WaveDirector>();
            director.AutoStart = false;
            director.Players = roster;
            director.Spawner = spawner;
            director.DifficultySettings = Asset<DifficultySettings>();
            director.Sequence = Asset<WaveSequence>();
            director.Sequence.Waves = new[] { new WaveDefinition {
                PreparationSeconds = 0f, MaximumAlive = 1,
                Groups = new[] { new WaveSpawnGroup { Prefab = prefab, Count = count, Interval = 0.01f } }
            } };
            go.SetActive(true);
            return director;
        }
        private static void Damage(Health health, float amount, GameObject attacker = null)
            => health.Damage(new DamageSource(amount, DamageType.Projectile, attacker, health));

        [TearDown]
        public void Cleanup()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void BalancesAreIndependentAndRejectInvalidTransactions()
        {
            var a = Player("A").Balance;
            var b = Player("B").Balance;
            Assert.IsTrue(a.TrySpend(400));
            Assert.AreEqual(100, a.Amount);
            Assert.AreEqual(500, b.Amount);
            Assert.IsFalse(a.TrySpend(101));
            Assert.IsFalse(a.TrySpend(-1));
            Assert.IsFalse(a.Credit(int.MaxValue));
            Assert.IsFalse(a.Credit(-1));
            Assert.AreEqual(100, a.Amount);
            a.gameObject.SetActive(false);
            a.gameObject.SetActive(true);
            Assert.AreEqual(100, a.Amount, "Disabling must not reset session money.");
        }

        [UnityTest]
        public IEnumerator RegionUsesUnionBlocksEntryAndChargesOnlyNearbyPurchaserOnce()
        {
            var player = Player("buyer");
            var other = Player("other");
            var region = Region();
            Assert.IsTrue(region.Contains(new Vector3(5f, 0f, 4f)));
            Assert.IsFalse(region.Contains(Vector3.zero));
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(Vector3.zero, Vector3.right, 10f));
            Assert.IsFalse(Purchase(region, player), "Remote purchase must fail.");
            player.transform.position = Vector3.right * 2f;
            int unlocked = 0;
            region.Unlocked += _ => { unlocked++; Assert.IsFalse(Purchase(region, other)); };
            Assert.IsTrue(Purchase(region, player));
            Assert.AreEqual(100, player.Balance.Amount);
            Assert.AreEqual(500, other.Balance.Amount);
            Assert.IsFalse(Purchase(region, player));
            Assert.AreEqual(1, unlocked);
            Physics.SyncTransforms();
            Assert.IsFalse(Physics.Raycast(Vector3.zero, Vector3.right, 10f));
            yield return null;
        }

        [Test]
        public void RegionRefusesInsufficientFundsAndDownedPlayers()
        {
            var player = Player("buyer");
            player.transform.position = Vector3.right * 2f;
            var region = Region();
            player.Balance.TrySpend(200);
            Assert.IsFalse(Purchase(region, player));
            Assert.IsFalse(region.IsUnlocked);
            Assert.AreEqual(300, player.Balance.Amount);
            player.Balance.Credit(200);
            Damage(player.CharacterHealth, 1000f);
            Assert.IsFalse(Purchase(region, player));
            Assert.AreEqual(500, player.Balance.Amount);
        }

        [UnityTest]
        public IEnumerator AttributesDriveMovementAndRegenWaitsThenStopsWhenDowned()
        {
            var player = Player("patient");
            var attributes = player.Attributes;
            attributes.RegenerationDelay = 0.08f;
            attributes.RegenerationPerSecond = 100f;
            var input = player.gameObject.AddComponent<PlayerInputDriver>();
            input.enabled = false;
            attributes.MovementSpeed = 9f;
            Assert.AreEqual(9f, input.GetMoveSpeed(false));
            Damage(player.CharacterHealth, 50f);
            yield return null;
            Assert.AreEqual(50f, attributes.CurrentHealth);
            yield return new WaitForSeconds(0.15f);
            Assert.Greater(attributes.CurrentHealth, 50f);
            Damage(player.CharacterHealth, 1000f);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(HealthState.Downed, attributes.State);
            Assert.AreEqual(0f, attributes.CurrentHealth);
            Assert.IsTrue(player.CharacterHealth.Revive(25f));
            Assert.AreEqual(25f, attributes.CurrentHealth);
        }

        [Test]
        public void DifficultyUsesBothEditableMultipliers()
        {
            var settings = Asset<DifficultySettings>();
            settings.PlayerCountMultiplier = AnimationCurve.Linear(1f, 1f, 4f, 4f);
            settings.Presets = new[] { new DifficultyPreset(GameDifficulty.Hard, 2f) };
            Assert.AreEqual(6f, settings.GetMultiplier(3, GameDifficulty.Hard), 0.001f);
            Assert.Throws<System.InvalidOperationException>(() => settings.GetMultiplier(1, GameDifficulty.Easy));
        }

        [Test]
        public void AuthoredMaximumHealthIsAppliedAtSpawn()
        {
            var go = Go("configuredPlayer", false);
            go.AddComponent<CharacterAttributes>().MaximumHealth = 180f;
            var player = go.AddComponent<CharacterPlayer>();
            go.SetActive(true);
            Assert.AreEqual(180f, player.CharacterHealth.MaximumHealthValue);
            Assert.AreEqual(180f, player.CharacterHealth.CurrentHealthValue);
        }

        [UnityTest]
        public IEnumerator CharacterControllerCannotCrossLockedRegionButCanCrossAfterPurchase()
        {
            var player = Player("walker");
            var motor = player.gameObject.AddComponent<CharacterController>();
            motor.radius = 0.5f;
            player.transform.position = Vector3.right * 2f;
            var region = Region();
            Physics.SyncTransforms();
            motor.Move(Vector3.right * 3f);
            Assert.Less(player.transform.position.x, 3f);
            player.GetComponent<PlayerInteractor>().RefreshTarget();
            Assert.AreSame(region.GetComponent<RegionUnlockInteraction>(), player.GetComponent<PlayerInteractor>().Target);
            Assert.IsTrue(player.GetComponent<PlayerInteractor>().TryInteract());
            Physics.SyncTransforms();
            motor.Move(Vector3.right * 3f);
            Assert.Greater(player.transform.position.x, 4f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerCountAndDifficultyScaleWaveAndAreSnapshottedUntilNextWave()
        {
            var player = Player("first");
            var second = Player("second");
            var spawner = Spawner();
            var director = Director(player, Template(), spawner, 1);
            director.Players.Register(second);
            director.Difficulty = GameDifficulty.Hard;
            director.DifficultySettings.PlayerCountMultiplier = AnimationCurve.Linear(1f, 1f, 4f, 4f);
            director.DifficultySettings.Presets = new[] { new DifficultyPreset(GameDifficulty.Hard, 2f) };
            Enemy spawned = null;
            spawner.Spawned += enemy => spawned = enemy;
            Assert.IsTrue(director.BeginSession());
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(4f, director.AppliedMultiplier, 0.001f);
            Assert.AreEqual(3, director.PendingCount);
            Assert.AreEqual(400f, spawned.Health.MaximumHealthValue, 0.001f);
            Assert.AreEqual(40f, spawned.AttackDamage, 0.001f);
            director.Players.Unregister(second);
            yield return null;
            Assert.AreEqual(4f, director.AppliedMultiplier, "Do not mutate live enemies or pending wave counts on disconnect.");
        }

        [UnityTest]
        public IEnumerator SpawnWaitsForRegionAndTracksDeathDisableAndDestroy()
        {
            var spawner = Spawner();
            var template = Template();
            var region = Region();
            foreach (var point in spawner.Points) point.RequiredRegion = region;
            Assert.IsFalse(spawner.TrySpawn(template, 1f, 1f, out _));
            var player = Player("buyer");
            player.transform.position = Vector3.right * 2f;
            Assert.IsTrue(Purchase(region, player));
            Assert.IsTrue(spawner.TrySpawn(template, 2f, 3f, out var enemy));
            Assert.AreEqual(200f, enemy.Health.MaximumHealthValue);
            Assert.AreEqual(30f, enemy.AttackDamage);
            Assert.IsFalse(enemy.CanDestroyObstacles);
            Assert.AreEqual(1, spawner.AliveCount);
            Damage(enemy.Health, 1000f, player.gameObject);
            Assert.AreEqual(0, spawner.AliveCount);
            Assert.AreEqual(200, player.Balance.Amount);
            Assert.IsTrue(spawner.TrySpawn(template, 1f, 1f, out var second));
            second.gameObject.SetActive(false);
            Assert.AreEqual(0, spawner.AliveCount);
            Assert.IsTrue(spawner.TrySpawn(template, 1f, 1f, out var third));
            Object.Destroy(third.gameObject);
            yield return null;
            Assert.AreEqual(0, spawner.AliveCount);
        }

        [UnityTest]
        public IEnumerator LethalBountyWorksWhenCharacterAiDisablesBeforeDamageNotification()
        {
            var player = Player("killer");
            var go = Go("target", false);
            go.AddComponent<CharacterAi>().DisableOnDeath = true;
            go.AddComponent<EnemyBounty>();
            go.SetActive(true);
            Damage(go.GetComponent<Health>(), 1000f, player.gameObject);
            Assert.IsFalse(go.activeSelf);
            Assert.AreEqual(600, player.Balance.Amount);
            Damage(go.GetComponent<Health>(), 1000f, player.gameObject);
            Assert.AreEqual(600, player.Balance.Amount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WavesRespectAliveCapPendingSpawnsAndCompleteAfterClear()
        {
            var player = Player("player");
            var spawner = Spawner();
            var director = Director(player, Template(), spawner);
            var spawned = new List<Enemy>();
            spawner.Spawned += spawned.Add;
            Assert.IsTrue(director.BeginSession());
            yield return new WaitForSeconds(0.08f);
            Assert.AreEqual(1, spawned.Count);
            Assert.AreEqual(1, director.PendingCount);
            Assert.AreEqual(WaveState.Spawning, director.State);
            Damage(spawned[0].Health, 1000f);
            yield return new WaitForSeconds(0.05f);
            Assert.AreEqual(2, spawned.Count);
            Assert.AreEqual(WaveState.Clearing, director.State);
            Damage(spawned[1].Health, 1000f);
            yield return null;
            yield return null;
            Assert.AreEqual(WaveState.Completed, director.State);
        }

        [UnityTest]
        public IEnumerator BlockedSpawnsStayPendingAndAllDownedEndsSession()
        {
            var player = Player("player");
            var spawner = Spawner();
            spawner.Points = System.Array.Empty<EnemySpawnPoint>();
            var director = Director(player, Template(), spawner);
            director.BeginSession();
            yield return new WaitForSeconds(0.06f);
            Assert.AreEqual(2, director.PendingCount);
            Assert.AreEqual(WaveState.Spawning, director.State);
            Assert.IsNotEmpty(director.StatusMessage);
            Damage(player.CharacterHealth, 1000f);
            yield return null;
            Assert.AreEqual(WaveState.Failed, director.State);
            Assert.AreEqual(1, director.Players.ActiveCount, "Downed players still count as participants.");
        }
    }
}
