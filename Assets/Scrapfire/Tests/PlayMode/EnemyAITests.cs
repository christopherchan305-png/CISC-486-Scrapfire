using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Characters.Inventory;
using Scrapfire.Characters.Weapon;
using Scrapfire.Enemies;
using Scrapfire.Enemies.AI;
using Scrapfire.Levels;
using Scrapfire.Session;
using Scrapfire.Spawning;
using Scrapfire.Waves;

namespace Scrapfire.Tests
{
    public sealed class EnemyAITests
    {
        private readonly List<Object> _created = new List<Object>();
        private readonly Vector3 _origin = new Vector3(18000f, 0f, 18000f);
        private NavMeshDataInstance _nav;
        private Scene _scene;
        private SessionPlayers _roster;
        private T Asset<T>() where T : ScriptableObject
        { var value = ScriptableObject.CreateInstance<T>(); _created.Add(value); return value; }
        private GameObject Go(string name, Vector3 local)
        { var go = new GameObject(name); go.transform.position = _origin + local; _created.Add(go); return go; }
        private SessionPlayers Roster => _roster != null ? _roster : _roster = Go("Roster", Vector3.zero).AddComponent<SessionPlayers>();
        private CharacterPlayer Player(Vector3 position)
        {
            var go = Go("Player", position);
            var controller = go.AddComponent<CharacterController>(); controller.height = 2f; controller.radius = 0.4f;
            var player = go.AddComponent<CharacterPlayer>(); Roster.Register(player); return player;
        }
        private EnemyAIProfile Profile()
        {
            var profile = Asset<EnemyAIProfile>();
            profile.FieldOfView = 360f; profile.ThinkInterval = 0.05f;
            profile.DefaultBehaviour = EnemyDefaultBehaviour.Idle;
            return profile;
        }
        private EnemyBrain Zombie(Vector3 position, EnemyAIProfile profile, float speed = 3f)
        {
            var go = Go("Zombie", position); go.SetActive(false);
            go.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2f, 0.8f);
            var agent = go.AddComponent<NavMeshAgent>(); agent.enabled = false;
            agent.agentTypeID = 0;
            agent.radius = 0.3f; agent.height = 1.8f; agent.baseOffset = 1f; agent.acceleration = 20f;
            var enemy = go.AddComponent<Enemy>();
            enemy.Definition = Asset<EnemyDefinition>(); enemy.Definition.MovementSpeed = speed;
            enemy.Definition.AttackInterval = 0.2f;
            var brain = go.AddComponent<EnemyBrain>(); brain.Profile = profile; brain.Players = Roster;
            go.SetActive(true);
            Assert.IsTrue(brain.Navigation.EnsureReady(), brain.Navigation.LastFailure + " at " + brain.Navigation.FootPosition);
            return brain;
        }
        private GameObject Wall(Vector3 position, Vector3 size)
        {
            var go = Go("Opaque wall", position); go.AddComponent<BoxCollider>().size = size;
            Physics.SyncTransforms(); return go;
        }
        private void BakeTestFloor()
        {
            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f; settings.agentHeight = 1.8f; settings.agentClimb = 0.35f;
            settings.overrideVoxelSize = true; settings.voxelSize = 0.1f;
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource {
                shape = NavMeshBuildSourceShape.Box, size = new Vector3(80f, 0.2f, 80f),
                transform = Matrix4x4.TRS(_origin - Vector3.up * 0.1f, Quaternion.identity, Vector3.one), area = 0
            }};
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(_origin, new Vector3(84f, 8f, 84f)), Vector3.zero, Quaternion.identity);
            Assert.IsNotNull(data); _created.Add(data); _nav = NavMesh.AddNavMeshData(data);
            Assert.IsTrue(NavMesh.SamplePosition(_origin, out _, 1f, new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas }), "Test navigation floor must exist.");
        }

        [TearDown]
        public void Cleanup()
        {
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
            if (_nav.valid) _nav.Remove();
            _roster = null;
        }
        [UnityTearDown]
        public IEnumerator Unload()
        { if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene); }

        [Test]
        public void SeekAndPursuePredictOnlyHorizontalObservedMotionWithBoundedLookAhead()
        {
            Vector3 target = new Vector3(0f, 4f, 10f), velocity = new Vector3(5f, 100f, 0f);
            Assert.AreEqual(target, EnemyBrain.ChaseDestination(EnemyChaseMode.Seek, Vector3.zero, target, velocity, 3f, 0.8f));
            Assert.AreEqual(new Vector3(4f, 4f, 10f), EnemyBrain.ChaseDestination(EnemyChaseMode.Pursue, Vector3.zero, target, velocity, 3f, 0.8f));
            Assert.AreEqual(target, EnemyBrain.ChaseDestination(EnemyChaseMode.Pursue, Vector3.zero, target, Vector3.zero, 3f, 0.8f));
        }

        [Test]
        public void DetectionRequiresRangeFieldOfViewClearSightAndStandingTarget()
        {
            var observer = Go("Eyes", Vector3.up).AddComponent<EnemyPerception>();
            var profile = Profile(); profile.FieldOfView = 90f; profile.SightRadius = 10f;
            var player = Player(new Vector3(0f, 1f, 5f)); Physics.SyncTransforms();
            Assert.IsTrue(observer.CanSee(player, profile));
            observer.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Assert.IsFalse(observer.CanSee(player, profile));
            observer.transform.rotation = Quaternion.identity;
            profile.SightRadius = 3f; Assert.IsFalse(observer.CanSee(player, profile)); profile.SightRadius = 10f;
            var wall = Wall(new Vector3(0f, 1.5f, 2.5f), new Vector3(3f, 3f, 0.3f));
            Assert.IsFalse(observer.CanSee(player, profile));
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.IsTrue(observer.CanSee(player, profile));
            player.CharacterHealth.Kill(); Assert.IsFalse(observer.CanSee(player, profile));
        }

        [UnityTest]
        public IEnumerator DefaultWanderMovesButIdleStaysStillAndActualMotionIgnoresTeleports()
        {
            BakeTestFloor();
            var wander = Profile(); wander.DefaultBehaviour = EnemyDefaultBehaviour.Wander;
            wander.SightRadius = 0.1f; wander.WanderRadius = 5f; wander.WanderPause = Vector2.zero;
            var moving = Zombie(Vector3.up, wander);
            var stationary = Zombie(new Vector3(12f, 1f, 0f), Profile());
            Vector3 start = moving.transform.position, idleStart = stationary.transform.position;
            float moved = 0f;
            float until = Time.time + 5f;
            while (Time.time < until && moved <= 0.5f) { moved = Mathf.Max(moved, Vector3.Distance(start, moving.transform.position)); yield return null; }
            Assert.Greater(moved, 0.5f, "Wander must make progress: " + moving.State + ", " + moving.Navigation.LastFailure);
            Assert.Less(Vector3.Distance(idleStart, stationary.transform.position), 0.15f);
            Assert.AreEqual(EnemyBrainState.Wander, moving.State);
            var player = Player(new Vector3(25f, 1f, 0f)); yield return null;
            for (int i = 0; i < 4; i++) { player.transform.position += Vector3.right * 0.02f; yield return null; }
            Assert.Greater(player.GetComponent<TargetMotion>().Velocity.x, 0f);
            player.transform.position += Vector3.right * 10f; yield return null; yield return null;
            Assert.AreEqual(Vector3.zero, player.GetComponent<TargetMotion>().Velocity);
        }

        [UnityTest]
        public IEnumerator EnemyChasesAttacksAndStopsImmediatelyOnDeath()
        {
            BakeTestFloor();
            var player = Player(new Vector3(0f, 1f, 6f));
            var brain = Zombie(Vector3.up, Profile());
            float deadline = Time.time + 5f;
            while (player.CharacterHealth.CurrentHealthValue >= 100f && Time.time < deadline) yield return null;
            Assert.Less(player.CharacterHealth.CurrentHealthValue, 100f, "Enemy must actually reach and damage its target.");
            Assert.AreEqual(EnemyBrainState.Attack, brain.State);
            Assert.Less(Vector3.Distance(brain.transform.position, player.transform.position), 1.85f);
            brain.GetComponent<Health>().Kill(); yield return null;
            Assert.AreEqual(EnemyBrainState.Dead, brain.State);
            Assert.IsFalse(brain.Navigation.Agent.enabled);
            float health = player.CharacterHealth.CurrentHealthValue;
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(health, player.CharacterHealth.CurrentHealthValue);
        }

        [UnityTest]
        public IEnumerator AlertsPropagateAcrossNeighboursWithoutSightButRespectHopLimitAndOriginalTimestamp()
        {
            BakeTestFloor();
            var player = Player(new Vector3(0f, 1f, 6f));
            var sourceProfile = Profile(); sourceProfile.SightRadius = 7f;
            sourceProfile.AlertRadius = 4.5f; sourceProfile.BroadcastInterval = 0.1f; sourceProfile.MaximumAlertHops = 2;
            var followerProfile = Object.Instantiate(sourceProfile); _created.Add(followerProfile); followerProfile.SightRadius = 0.1f;
            var a = Zombie(Vector3.up, sourceProfile, 0f);
            var b = Zombie(new Vector3(4f, 1f, 0f), followerProfile, 0f);
            var c = Zombie(new Vector3(8f, 1f, 0f), followerProfile, 0f);
            var d = Zombie(new Vector3(12f, 1f, 0f), followerProfile, 0f);
            float deadline = Time.time + 2f;
            while (c.Target == null && Time.time < deadline) yield return null;
            Assert.AreSame(player, b.Target); Assert.AreSame(player, c.Target);
            Assert.IsNull(d.Target, "Third hop must not receive an alert with a two-hop limit.");
            Assert.IsFalse(b.HasVisualContact); Assert.IsFalse(c.HasVisualContact);
            Assert.AreEqual(b.LastSeenTime, c.LastSeenTime, 0.0001f, "Relaying must preserve the original observation time.");
            Assert.Greater(a.AlertsDelivered, 0); Assert.Greater(b.AlertsDelivered, 0);
            float observed = c.LastSeenTime; Vector3 remembered = c.LastKnownPosition;
            player.transform.position = _origin + new Vector3(30f, 1f, 20f); Physics.SyncTransforms();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(observed, c.LastSeenTime); Assert.AreEqual(remembered, c.LastKnownPosition);
            var expired = new EnemyAlertMessage(player, remembered, Vector3.zero, Time.time - 10f, Time.time - 1f, 999, 0);
            Assert.IsFalse(d.TryReceiveAlert(expired));
        }

        [UnityTest]
        public IEnumerator LostSightDoesNotReadHiddenPositionAndSearchEventuallyReturnsToDefault()
        {
            BakeTestFloor();
            var player = Player(new Vector3(0f, 1f, 6f));
            var profile = Profile(); profile.MemorySeconds = 0.2f; profile.SearchSeconds = 0.15f;
            profile.WanderTravelTimeout = 0.25f; profile.UnreachableTimeout = 0.1f;
            var brain = Zombie(Vector3.up, profile, 0f);
            yield return new WaitForSeconds(0.2f);
            Assert.AreSame(player, brain.Target);
            Vector3 remembered = brain.LastKnownPosition;
            Wall(new Vector3(0f, 1.5f, 3f), new Vector3(30f, 3f, 0.3f));
            player.transform.position += Vector3.right * 4f; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.12f);
            Assert.IsFalse(brain.HasVisualContact); Assert.AreEqual(remembered, brain.LastKnownPosition);
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(EnemyBrainState.Idle, brain.State); Assert.IsNull(brain.Target);
        }

        [Test]
        public void AlertOcclusionAndInvalidTargetsAreRespected()
        {
            BakeTestFloor();
            var player = Player(new Vector3(0f, 1f, 6f));
            var profile = Profile();
            var a = Zombie(Vector3.up, profile, 0f);
            var b = Zombie(new Vector3(4f, 1f, 0f), profile, 0f);
            var message = new EnemyAlertMessage(player, player.transform.position, Vector3.zero, Time.time, Time.time + 3f, 101, 0);
            var wall = Wall(new Vector3(2f, 1.5f, 0f), new Vector3(0.3f, 3f, 4f));
            Assert.AreEqual(0, a.GetComponent<EnemyAlertRelay>().Broadcast(message, profile, a.GetComponent<EnemyPerception>()));
            Assert.IsNull(b.Target);
            wall.SetActive(false); Physics.SyncTransforms();
            Assert.AreEqual(1, a.GetComponent<EnemyAlertRelay>().Broadcast(message, profile, a.GetComponent<EnemyPerception>()));
            Wall(new Vector3(0f, 3.8f, 0f), new Vector3(20f, 0.2f, 20f));
            Assert.IsFalse(a.GetComponent<EnemyPerception>().HasClearLine(a.transform.position, a.transform.position + Vector3.up * 5f, profile.AlertBlockingLayers));
            player.CharacterHealth.Kill();
            Assert.IsFalse(a.TryReceiveAlert(message));
        }

        [UnityTest]
        public IEnumerator GunshotHearingInvestigatesWithoutIdentityAndOnlyRevealsPlayerOnSight()
        {
            BakeTestFloor();
            var player = Player(new Vector3(0f, 1f, 8f));
            var profile = Profile(); profile.SightRadius = 0.1f; profile.SearchSeconds = 0.15f;
            profile.WanderTravelTimeout = 2f;
            var listener = Zombie(Vector3.up, profile);
            var distant = Zombie(new Vector3(25f, 1f, 0f), profile, 0f);
            yield return null;
            Vector3 start = listener.transform.position;
            Vector3 heardPosition = BuildingVisibility.FootPosition(player.transform);
            Assert.IsTrue(WorldNoise.Emit(player.transform.position, heardPosition, 12f));
            Assert.AreEqual(EnemyBrainState.Investigate, listener.State);
            Assert.IsNull(listener.Target, "A sound contains a location, never player identity.");
            Assert.IsNull(distant.Target); Assert.AreEqual(EnemyBrainState.Idle, distant.State);
            Assert.AreEqual(heardPosition, listener.LastKnownPosition);
            player.transform.position += Vector3.right * 10f; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector3.Distance(start, listener.transform.position), 0.3f);
            Assert.IsNull(listener.Target); Assert.AreEqual(heardPosition, listener.LastKnownPosition);
            Assert.AreEqual(0, listener.AlertsDelivered, "Anonymous noise must not turn into an identified alert.");
            float deadline = Time.time + 5f;
            while (listener.State != EnemyBrainState.Idle && Time.time < deadline) yield return null;
            Assert.AreEqual(EnemyBrainState.Idle, listener.State, "An unconfirmed sound must eventually be forgotten.");
            WorldNoise.Emit(player.transform.position, BuildingVisibility.FootPosition(player.transform), 20f);
            Assert.IsNull(listener.Target);
            profile.SightRadius = 30f;
            yield return new WaitForSeconds(0.15f);
            Assert.AreSame(player, listener.Target, "Normal visual confirmation can promote the investigation to chase.");
            Assert.AreEqual(EnemyBrainState.Chase, listener.State);
        }

        [UnityTest]
        public IEnumerator ProjectileHitImmediatelyTargetsShooterButDoesNotTrackHiddenMovement()
        {
            BakeTestFloor();
            var a = Player(new Vector3(0f, 1f, 8f));
            var b = Player(new Vector3(8f, 1f, 0f));
            var profile = Profile(); profile.SightRadius = 0.1f;
            var brain = Zombie(Vector3.up, profile, 0f);
            var health = brain.GetComponent<Health>();
            Assert.AreEqual(10f, health.Damage(new DamageSource(10f, DamageType.Projectile, a.gameObject, health)));
            Assert.AreSame(a, brain.Target); Assert.AreEqual(EnemyBrainState.Chase, brain.State);
            Assert.IsFalse(brain.HasVisualContact);
            Vector3 remembered = brain.LastKnownPosition;
            a.transform.position += Vector3.right * 10f; Physics.SyncTransforms();
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(remembered, brain.LastKnownPosition);
            WorldNoise.Emit(brain.transform.position, brain.Navigation.FootPosition, 30f);
            Assert.AreSame(a, brain.Target, "Anonymous noise must not interrupt confirmed combat.");
            health.Damage(new DamageSource(10f, DamageType.Projectile, b.gameObject, health));
            Assert.AreSame(b, brain.Target, "A new attacker takes priority over the previous target.");
            health.Damage(new DamageSource(5f, DamageType.Environmental, a.gameObject, health));
            Assert.AreSame(b, brain.Target);
            health.ImmuneToDamage = true;
            Assert.AreEqual(0f, health.Damage(new DamageSource(10f, DamageType.Projectile, a.gameObject, health)));
            Assert.AreSame(b, brain.Target, "Rejected damage must not aggro.");
            health.ImmuneToDamage = false;
            health.Damage(new DamageSource(100f, DamageType.Projectile, a.gameObject, health));
            Assert.AreEqual(EnemyBrainState.Dead, brain.State);
            WorldNoise.Emit(brain.transform.position, brain.Navigation.FootPosition, 30f);
            Assert.AreEqual(EnemyBrainState.Dead, brain.State, "A lethal hit or later sound must not resurrect the brain.");
        }

        [UnityTest]
        public IEnumerator RealMultiPelletShotsMakeOneNoiseWhileDryFireReloadWarmupAndSilentShotsDoNot()
        {
            BakeTestFloor();
            var player = Player(Vector3.up);
            var profile = Profile(); profile.SightRadius = 0.1f;
            var hit = Zombie(new Vector3(0f, 1f, 6f), profile, 0f);
            var unhit = Zombie(new Vector3(8f, 1f, 6f), profile, 0f);
            var ammoItem = Asset<Item>(); ammoItem.ItemCategory = Item.Category.Ammo;
            var ammo = Asset<WeaponAmmo>(); ammo.Item = ammoItem; ammo.BulletPerShoot = 6; ammo.BulletDamage = 5f;
            var data = Asset<WeaponData>(); data.Ammo = ammo; data.MagazineCapacity = 1;
            data.TimeBetweenUse = 0.05f; data.MagazineReloadInterval = 0.05f; data.RecoilValue = 0f; data.GunshotRadius = 12f;
            var weapon = Go("Test shotgun", Vector3.up).AddComponent<RangedWeapon>();
            weapon.Data = data; weapon.InitialReserveAmmo = 0; weapon.AttackOrigin = weapon.transform;
            player.EquipWeapon(weapon); Physics.SyncTransforms();
            int noises = 0;
            System.Action<NoiseSignal> listen = signal => { noises++; Assert.AreEqual(data.GunshotRadius, signal.Radius); };
            WorldNoise.Emitted += listen;
            try
            {
                Assert.IsTrue(weapon.TryAttack(Vector3.forward));
                Assert.AreEqual(1, noises, "Pellets must not multiply noise emissions.");
                Assert.AreEqual(70f, hit.GetComponent<Health>().CurrentHealthValue);
                Assert.AreSame(player, hit.Target); Assert.AreEqual(EnemyBrainState.Chase, hit.State);
                Assert.IsNull(unhit.Target); Assert.AreEqual(EnemyBrainState.Investigate, unhit.State);
                yield return new WaitForSeconds(0.1f);
                Assert.IsFalse(weapon.TryAttack(Vector3.forward)); Assert.AreEqual(1, noises);
                Assert.IsFalse(weapon.TryReload()); Assert.AreEqual(1, noises);
                weapon.AddReserveAmmo(2);
                Assert.IsTrue(weapon.TryReload()); Assert.AreEqual(1, noises);
                yield return new WaitForSeconds(0.1f);
                data.TimeDelayBeforeUse = 0.2f;
                Assert.IsTrue(weapon.TryAttack(Vector3.forward)); Assert.AreEqual(1, noises);
                weapon.CancelAction(); data.TimeDelayBeforeUse = 0f;
                Assert.AreEqual(1, noises);
                Assert.IsTrue(weapon.TryAttack(Vector3.forward)); Assert.AreEqual(2, noises);
                yield return new WaitForSeconds(0.1f);
                Assert.IsTrue(weapon.TryReload()); yield return new WaitForSeconds(0.1f);
                data.GunshotRadius = 0f;
                Assert.IsTrue(weapon.TryAttack(Vector3.forward)); Assert.AreEqual(2, noises);
            }
            finally { WorldNoise.Emitted -= listen; }
        }

        [UnityTest]
        public IEnumerator SavedNavigationRespectsBothLocksAndAgentsUseRealStairRouteAfterUnlock()
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
            roots.SelectMany(g => g.GetComponentsInChildren<WaveDirector>()).Single().enabled = false;
            foreach (var brain in roots.SelectMany(g => g.GetComponentsInChildren<EnemyBrain>())) brain.enabled = false;
            var regions = roots.SelectMany(g => g.GetComponentsInChildren<MapRegion>()).ToArray();
            var main = regions.Single(r => r.name == "BuildingEntranceRegion");
            var upper = regions.Single(r => r.name == "UpperFloorAndStairsRegion");
            var spawner = roots.SelectMany(g => g.GetComponentsInChildren<EntitySpawner>()).Single();
            int type = spawner.Points[0].NavigationAgentType;
            var filter = new NavMeshQueryFilter { agentTypeID = type, areaMask = NavMesh.AllAreas };
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(NavMesh.SamplePosition(new Vector3(0f, 0.03f, 2f), out _, 0.3f, filter));
            Assert.IsFalse(spawner.IsPointAvailable(spawner.Points.Single(p => p.name == "SpawnNorthB")));
            Assert.IsTrue(main.TryUnlock()); yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(NavMesh.SamplePosition(new Vector3(0f, 0.03f, 2f), out _, 0.3f, filter));
            Assert.IsFalse(NavMesh.SamplePosition(new Vector3(-8f, 0.03f, 6f), out _, 0.3f, filter), "Stair gate must remain closed after entering building.");
            Assert.IsTrue(upper.TryUnlock()); yield return new WaitForSeconds(0.3f);
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(new Vector3(0f, 0f, -8f), new Vector3(-8f, 3.83f, 16f), filter, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
#if UNITY_EDITOR
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scrapfire/Prefabs/SurvivalEnemy.prefab");
            var actor = Object.Instantiate(prefab, new Vector3(-8f, 1f, 3.5f), Quaternion.identity); _created.Add(actor);
            actor.GetComponent<EnemyBrain>().enabled = false;
            var navigation = actor.GetComponent<EnemyNavigation>();
            Assert.IsTrue(navigation.EnsureReady()); navigation.Agent.speed = 6f;
            Assert.IsTrue(navigation.TryMoveTo(new Vector3(-8f, 3.83f, 16f)));
            float deadline = Time.time + 6f;
            while (!navigation.Arrived && Time.time < deadline) yield return null;
            Assert.Greater(actor.transform.position.y, 4.5f, "The agent must climb the physical stair route to floor two.");
            Assert.Greater(actor.transform.position.z, 15f);
            Assert.AreEqual(0, actor.GetComponentsInChildren<Camera>().Length, "FBX preview cameras must not enter the game.");
#endif
            var point = Go("Off-floor spawn", new Vector3(0f, 20f, 0f)).AddComponent<EnemySpawnPoint>();
            point.RequireNavMesh = true; point.NavigationAgentType = type;
            Assert.IsFalse(point.HasNavigation());
        }
    }
}
