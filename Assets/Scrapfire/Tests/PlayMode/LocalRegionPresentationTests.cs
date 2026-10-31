using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Weapon;
using Scrapfire.Levels;

namespace Scrapfire.Tests
{
    public sealed class LocalRegionPresentationTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject Go(string name, Vector3 position, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active);
            go.transform.position = position;
            _created.Add(go);
            return go;
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var item in _created)
                if (item is GameObject go && go != null && go.TryGetComponent<Camera>(out var camera))
                    camera.targetTexture = null;
            for (int i = _created.Count - 1; i >= 0; i--)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        [Test]
        public void AreaFillCoversTheUnionOnceAtTheRequestedHeight()
        {
            var mesh = RegionBoundaryMesh.BuildSurface(new[] {
                new RegionVolume(Vector3.zero, Vector3.one * 2f),
                new RegionVolume(Vector3.right, Vector3.one * 2f)
            }, 0.06f);
            _created.Add(mesh);
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            float area = 0f;
            foreach (var vertex in vertices) Assert.AreEqual(0.06f, vertex.y, 0.001f);
            for (int i = 0; i < indices.Length; i += 3)
                area += Vector3.Cross(vertices[indices[i + 1]] - vertices[indices[i]],
                    vertices[indices[i + 2]] - vertices[indices[i]]).magnitude * 0.5f;
            Assert.AreEqual(6f, area, 0.001f, "A 3x2 union must not double-blend overlapping fill.");
        }

        private Camera CameraFor(Transform target, Vector3 focus, out RenderTexture texture)
        {
            var go = Go("Local view", focus + new Vector3(0f, 8f, -8f));
            go.transform.LookAt(focus);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4.8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = false;
            camera.farClipPlane = 50f;
            texture = new RenderTexture(64, 64, 24);
            texture.Create();
            _created.Add(texture);
            camera.targetTexture = texture;
            go.AddComponent<RegionViewObserver>().Target = target;
            return camera;
        }

        private float ReadRed(RenderTexture texture)
        {
            var sample = new Texture2D(64, 64, TextureFormat.RGB24, false);
            _created.Add(sample);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = texture;
                sample.ReadPixels(new Rect(0, 0, 64, 64), 0, 0);
                sample.Apply();
            }
            finally { RenderTexture.active = previous; }
            float red = 0f;
            foreach (var pixel in sample.GetPixels()) red += pixel.r;
            return red / (64f * 64f);
        }

        [UnityTest]
        public IEnumerator OnlyTheNearbyPlayersCameraReceivesTheWarningEvenWhenBothCamerasSeeTheSameWorld()
        {
            var origin = Vector3.right * 1000f;
            var a = Go("Player A observer", origin + new Vector3(-1f, 1f, 0f));
            var b = Go("Player B observer", origin + new Vector3(-20f, 1f, 0f));
            var regionObject = Go("Region", origin, false);
            var region = regionObject.AddComponent<MapRegion>();
            region.Volumes = new[] { new RegionVolume(new Vector3(2f, 1f, 0f), new Vector3(4f, 4f, 6f)) };
            var view = regionObject.AddComponent<RegionBoundaryVisual>();
            var material = new Material(Shader.Find("Scrapfire/Locked Region Boundary"));
            _created.Add(material);
            view.BoundaryMaterial = material;
            view.AreaOpacity = 0.6f;
            var cameraA = CameraFor(a.transform, origin + Vector3.right * 2f, out var imageA);
            var cameraB = CameraFor(b.transform, origin + Vector3.right * 2f, out var imageB);
            regionObject.SetActive(true);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(view.IsVisibleTo(cameraA));
            Assert.IsFalse(view.IsVisibleTo(cameraB));
            Assert.Greater(ReadRed(imageA), ReadRed(imageB) + 0.002f, "A must receive a draw visible only in A's render target.");

            a.transform.position = origin + new Vector3(-20f, 1f, 0f);
            b.transform.position = origin + new Vector3(-1f, 1f, 0f);
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(view.IsVisibleTo(cameraA));
            Assert.IsTrue(view.IsVisibleTo(cameraB));
            Assert.Greater(ReadRed(imageB), ReadRed(imageA) + 0.002f, "Changing proximity must not leave A's warning cached in B's view.");
        }

        [UnityTest]
        public IEnumerator MovementBarrierStopsBothActorTypesButBulletsStillHitTargetsAndRealWalls()
        {
            Vector3 origin = Vector3.right * 2000f;
            var regionObject = Go("Locked region", origin, false);
            var region = regionObject.AddComponent<MapRegion>();
            region.Volumes = new[] { new RegionVolume(new Vector3(4f, 1f, 0f), new Vector3(4f, 8f, 10f)) };
            regionObject.SetActive(true);
            var player = Go("Player", origin + new Vector3(0f, 1f, -2f)).AddComponent<CharacterPlayer>();
            var playerMotor = player.gameObject.AddComponent<CharacterController>();
            var enemy = Go("Enemy", origin + new Vector3(0f, 1f, 2f)).AddComponent<CharacterAi>();
            var enemyMotor = enemy.gameObject.AddComponent<CharacterController>();
            var target = Go("Bullet target", origin + new Vector3(8f, 1f, 0f));
            target.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            playerMotor.Move(Vector3.right * 5f);
            enemyMotor.Move(Vector3.right * 5f);
            Assert.Less(player.transform.position.x, origin.x + 2f);
            Assert.Less(enemy.transform.position.x, origin.x + 2f);
            Assert.IsTrue(HitscanBullet.Cast(origin + Vector3.up, Vector3.right, 12f, ~0, player, out var hit));
            Assert.AreSame(target, hit.collider.gameObject, "Only the movement barrier should be ignored by bullets.");
            var wall = Go("Real wall", origin + new Vector3(6.5f, 1f, 0f));
            wall.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            Assert.IsTrue(HitscanBullet.Cast(origin + Vector3.up, Vector3.right, 12f, ~0, player, out hit));
            Assert.AreSame(wall, hit.collider.gameObject, "Real building walls must remain bullet obstructions.");
            int layer = LayerMask.NameToLayer(RegionMovementBarrier.LayerName);
            Assert.GreaterOrEqual(layer, 0);
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(layer, LayerMask.NameToLayer("Projectile")));
            Assert.IsTrue(Physics.GetIgnoreLayerCollision(layer, 0), "Props do not collide with the actor-only barrier.");
            yield return null;
        }
    }
}
