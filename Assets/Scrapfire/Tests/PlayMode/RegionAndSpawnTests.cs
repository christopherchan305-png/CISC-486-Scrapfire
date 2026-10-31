using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Enemies;
using Scrapfire.Levels;
using Scrapfire.Spawning;

namespace Scrapfire.Tests
{
    public sealed class RegionAndSpawnTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject Go(string name, Vector3 position = default, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            go.transform.position = position;
            _created.Add(go);
            return go;
        }
        private Enemy Template()
        {
            var enemy = Go("Template", active: false).AddComponent<Enemy>();
            enemy.Definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _created.Add(enemy.Definition);
            return enemy;
        }
        private EntitySpawner Manager()
        {
            var manager = Go("EntitySpawner").AddComponent<EntitySpawner>();
            manager.Points = new[] {
                Go("A", Vector3.zero).AddComponent<EnemySpawnPoint>(),
                Go("B", Vector3.right * 5f).AddComponent<EnemySpawnPoint>(),
                Go("C", Vector3.right * 10f).AddComponent<EnemySpawnPoint>()
            };
            return manager;
        }
        private MapRegion Gate(Vector3 center, Vector3 size)
        {
            var go = Go("Gate", active: false);
            var region = go.AddComponent<MapRegion>();
            region.Volumes = new[] { new RegionVolume(center, size) };
            go.SetActive(true);
            return region;
        }
        private Enemy SpawnAndMove(EntitySpawner manager, Enemy template)
        {
            Assert.IsTrue(manager.TrySpawn(template, 1f, 1f, out var enemy), manager.LastFailure);
            enemy.transform.position = new Vector3(100f + manager.AliveCount * 3f, 0f, 100f);
            return enemy;
        }
        [TearDown]
        public void Cleanup()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void SpawnsUseOnlyPresetPointsAndStayEvenAcrossTheWave()
        {
            var manager = Manager();
            var template = Template();
            manager.BeginWave();
            for (int i = 0; i < 14; i++)
            {
                Assert.IsTrue(manager.TrySpawn(template, 1f, 1f, out var enemy));
                CollectionAssert.Contains(new[] { Vector3.zero, Vector3.right * 5f, Vector3.right * 10f }, enemy.transform.position);
                enemy.transform.position = new Vector3(100f + i * 3f, 0f, 100f);
                int low = int.MaxValue, high = 0;
                foreach (var point in manager.Points)
                { low = Mathf.Min(low, manager.GetSpawnCount(point)); high = Mathf.Max(high, manager.GetSpawnCount(point)); }
                Assert.LessOrEqual(high - low, 1);
            }
            manager.BeginWave();
            foreach (var point in manager.Points) Assert.AreEqual(0, manager.GetSpawnCount(point));
        }

        [Test]
        public void PositionInsideLockedRegionIsExcludedWithoutManualBinding()
        {
            var manager = Manager();
            var template = Template();
            var gate = Gate(Vector3.right * 5f, new Vector3(2f, 4f, 4f));
            Assert.IsNull(manager.Points[1].RequiredRegion);
            Assert.IsFalse(manager.IsPointAvailable(manager.Points[1]));
            for (int i = 0; i < 6; i++) SpawnAndMove(manager, template);
            Assert.AreEqual(0, manager.GetSpawnCount(manager.Points[1]));
            Assert.IsTrue(gate.TryUnlock());
            Assert.IsTrue(manager.IsPointAvailable(manager.Points[1]));
            SpawnAndMove(manager, template);
            Assert.AreEqual(1, manager.GetSpawnCount(manager.Points[1]), "Newly opened point catches up within this wave.");
        }

        [Test]
        public void OverlappingRegionsAllGateThePointAndMovingPointsRechecksGeometry()
        {
            var manager = Manager();
            var a = Gate(Vector3.right * 5f, Vector3.one * 4f);
            var b = Gate(Vector3.right * 5f, Vector3.one * 2f);
            Assert.IsFalse(manager.IsPointAvailable(manager.Points[1]));
            a.TryUnlock();
            Assert.IsFalse(manager.IsPointAvailable(manager.Points[1]));
            b.TryUnlock();
            Assert.IsTrue(manager.IsPointAvailable(manager.Points[1]));
            var c = Gate(Vector3.right * 15f, Vector3.one * 2f);
            manager.Points[1].transform.position = Vector3.right * 15f;
            Assert.IsFalse(manager.IsPointAvailable(manager.Points[1]));
            manager.Points[1].transform.position = Vector3.right * 5f;
            Assert.IsTrue(manager.IsPointAvailable(manager.Points[1]));
        }

        [Test]
        public void BlockedAndDisabledPointsAreSkippedWithoutConsumingTheirQuota()
        {
            var manager = Manager();
            var template = Template();
            manager.ObstacleLayers = 1 << 31;
            var obstacle = Go("Obstacle", Vector3.right * 5f);
            obstacle.layer = 31;
            obstacle.AddComponent<BoxCollider>();
            manager.Points[2].gameObject.SetActive(false);
            Physics.SyncTransforms();
            SpawnAndMove(manager, template);
            SpawnAndMove(manager, template);
            Assert.AreEqual(2, manager.GetSpawnCount(manager.Points[0]));
            Assert.AreEqual(0, manager.GetSpawnCount(manager.Points[1]));
            Assert.AreEqual(0, manager.GetSpawnCount(manager.Points[2]));
            obstacle.SetActive(false);
            Physics.SyncTransforms();
            SpawnAndMove(manager, template);
            Assert.AreEqual(1, manager.GetSpawnCount(manager.Points[1]));
        }

        [Test]
        public void FailedGenerationKeepsQuotaAndDoesNotChooseRandomFallbackPosition()
        {
            var manager = Manager();
            foreach (var point in manager.Points) point.enabled = false;
            Assert.IsFalse(manager.TrySpawn(Template(), 1f, 1f, out var enemy));
            Assert.IsNull(enemy);
            Assert.AreEqual(0, manager.AliveCount);
            foreach (var point in manager.Points) Assert.AreEqual(0, manager.GetSpawnCount(point));
            Assert.IsNotEmpty(manager.LastFailure);
        }

        [Test]
        public void BoundaryMeshRemovesInternalFacesOfOverlappingBoxesAndHasNoCaps()
        {
            var mesh = RegionBoundaryMesh.Build(new[] {
                new RegionVolume(Vector3.zero, Vector3.one * 2f),
                new RegionVolume(Vector3.right, Vector3.one * 2f)
            });
            _created.Add(mesh);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float area = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var cross = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]);
                Assert.AreEqual(0f, cross.y, 0.001f, "Only side walls should be generated.");
                area += cross.magnitude * 0.5f;
            }
            Assert.AreEqual(20f, area, 0.001f, "Union is a 3 by 2 footprint, height 2: perimeter times height = 20.");
        }

        [UnityTest]
        public IEnumerator BoundaryViewAppearsNearObserverAndDisappearsOnUnlock()
        {
            var observer = Go("Observer", Vector3.left * 30f);
            var go = Go("VisibleRegion", active: false);
            var region = go.AddComponent<MapRegion>();
            region.Volumes = new[] { new RegionVolume(Vector3.right * 4f, new Vector3(4f, 8f, 4f)) };
            var view = go.AddComponent<RegionBoundaryVisual>();
            var shader = Shader.Find("Scrapfire/Locked Region Boundary");
            Assert.IsNotNull(shader);
            var material = new Material(shader);
            _created.Add(material);
            view.BoundaryMaterial = material;
            var camera = Go("Observer camera").AddComponent<Camera>();
            camera.gameObject.AddComponent<RegionViewObserver>().Target = observer.transform;
            go.SetActive(true);
            yield return null;
            Assert.IsFalse(view.IsVisible);
            observer.transform.position = Vector3.zero;
            yield return null;
            Assert.IsTrue(view.IsVisible);
            view.enabled = false;
            Assert.IsFalse(view.IsVisible);
            Assert.IsFalse(region.IsUnlocked, "Disabling presentation cannot unlock collision.");
            view.enabled = true;
            yield return null;
            Assert.IsTrue(view.IsVisible);
            region.TryUnlock();
            Assert.IsFalse(view.IsVisible, "Unlock hides the wall immediately.");
            yield return null;
            Assert.IsFalse(view.IsVisible);
        }

        [Test]
        public void ContourBandsGiveEdgeOnSidesVisibleSurfaceArea()
        {
            var mesh = RegionBoundaryMesh.Build(new[] {
                new RegionVolume(Vector3.up, new Vector3(2f, 2f, 2f))
            }, 0.12f);
            _created.Add(mesh);
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            float horizontalArea = 0f;
            for (int i = 0; i < triangles.Length; i += 3)
                horizontalArea += Mathf.Abs(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]],
                    vertices[triangles[i + 2]] - vertices[triangles[i]]).y) * 0.5f;
            Assert.Greater(horizontalArea, 0f, "Orthographic cameras must not lose all edge-on side contours.");
        }
    }
}
