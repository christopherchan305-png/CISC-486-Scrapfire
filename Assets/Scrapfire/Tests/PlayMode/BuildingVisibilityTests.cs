using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Levels;

namespace Scrapfire.Tests
{
    public sealed class BuildingVisibilityTests
    {
        private readonly List<Object> _created = new List<Object>();
        private Scene _scene;
        private GameObject Go(string name, Vector3 position, bool active = true)
        {
            var go = new GameObject(name);
            go.SetActive(active); go.transform.position = position; _created.Add(go); return go;
        }
        [TearDown]
        public void Cleanup()
        {
            foreach (var item in _created)
                if (item is GameObject go && go != null && go.TryGetComponent<Camera>(out var camera)) camera.targetTexture = null;
            for (int i = _created.Count - 1; i >= 0; i--) if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }
        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [Test]
        public void StoreyLookupUsesFootprintHeightAndBuildingTransform()
        {
            var go = Go("Building", new Vector3(100f, 0f, 100f), false);
            go.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            var building = go.AddComponent<BuildingVisibility>();
            building.FootprintCenter = Vector2.zero;
            building.FootprintSize = Vector2.one * 10f;
            building.GroundHeight = 0f;
            building.StoreyHeight = 4f;
            Assert.AreEqual(0, building.StoreyAt(go.transform.TransformPoint(new Vector3(0f, 1f, 0f))));
            Assert.AreEqual(1, building.StoreyAt(go.transform.TransformPoint(new Vector3(0f, 5f, 0f))));
            Assert.AreEqual(-1, building.StoreyAt(go.transform.TransformPoint(new Vector3(6f, 1f, 0f))));
            Assert.AreEqual(-1, building.StoreyAt(go.transform.TransformPoint(new Vector3(0f, 9f, 0f))));
        }

        private Transform Plane(Transform parent, float height, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Storey at " + height;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.up * height;
            go.transform.localScale = new Vector3(6f, 0.2f, 6f);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            _created.Add(material);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }
        private Camera View(Transform target, Vector3 origin, out RenderTexture texture)
        {
            var go = Go("View", origin + Vector3.up * 15f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 4f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = false; camera.farClipPlane = 30f;
            texture = new RenderTexture(64, 64, 24); texture.Create(); _created.Add(texture);
            camera.targetTexture = texture;
            go.AddComponent<RegionViewObserver>().Target = target;
            return camera;
        }
        private Color Read(RenderTexture texture)
        {
            var sample = new Texture2D(64, 64, TextureFormat.RGB24, false); _created.Add(sample);
            var previous = RenderTexture.active;
            try { RenderTexture.active = texture; sample.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); sample.Apply(); }
            finally { RenderTexture.active = previous; }
            Color sum = Color.clear;
            foreach (var color in sample.GetPixels()) sum += color;
            return sum / (64f * 64f);
        }

        [UnityTest]
        public IEnumerator TwoCamerasIndependentlySeeGroundUpperOrFullBuildingAndKeepPhysicsActive()
        {
            Vector3 origin = Vector3.right * 3000f;
            var root = Go("Building", origin, false);
            var ground = Plane(root.transform, 0f, Color.red);
            var upper = Plane(root.transform, 4f, Color.blue);
            var roof = Plane(root.transform, 8f, Color.green);
            var building = root.AddComponent<BuildingVisibility>();
            building.FootprintCenter = Vector2.zero; building.FootprintSize = Vector2.one * 10f;
            building.GroundHeight = 0f; building.StoreyHeight = 4f;
            building.VisualStoreys = new[] { new BuildingVisualStorey(0, ground), new BuildingVisualStorey(1, upper), new BuildingVisualStorey(2, roof) };
            var a = Go("A inside", origin + Vector3.up);
            var b = Go("B outside", origin + Vector3.right * 10f + Vector3.up);
            var cameraA = View(a.transform, origin, out var imageA);
            var cameraB = View(b.transform, origin, out var imageB);
            root.SetActive(true);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(building.GetStoreyOpacity(cameraA, 2), Is.InRange(0.05f, 0.95f), "Roof must fade, not pop out.");
            Assert.AreEqual(1f, building.GetStoreyOpacity(cameraB, 2), "Other camera remains fully outside.");
            var midFade = Read(imageA);
            Assert.Greater(midFade.g, 0.01f, "Partially visible roof must still render.");
            Assert.Greater(midFade.r + midFade.b, 0.01f, "Underlying floors must show through the fading roof.");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(0, building.StoreyFor(cameraA));
            Assert.AreEqual(-1, building.StoreyFor(cameraB));
            var colorA = Read(imageA); var colorB = Read(imageB);
            Assert.Greater(colorA.r, colorA.g + 0.1f, "A inside ground floor must see the red ground.");
            Assert.Greater(colorB.g, colorB.r + 0.1f, "B outside must still see the green roof.");
            Assert.IsTrue(roof.GetComponent<Collider>().enabled);
            Assert.IsFalse(roof.GetComponent<Renderer>().forceRenderingOff, "Restore render state after the camera completes.");
            Assert.AreEqual(ShadowCastingMode.On, roof.GetComponent<Renderer>().shadowCastingMode);
            a.transform.position = origin + Vector3.up * 5f;
            yield return new WaitForSecondsRealtime(0.6f);
            colorA = Read(imageA); colorB = Read(imageB);
            Assert.Greater(colorA.b, colorA.g + 0.1f, "A on upper floor must see blue, with roof cut away.");
            Assert.Greater(colorB.g, colorB.b + 0.1f, "B outside remains unaffected.");
            a.transform.position = origin + Vector3.right * 10f + Vector3.up;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(building.GetStoreyOpacity(cameraA, 2), Is.InRange(0.05f, 0.95f), "Roof must also fade in on exit.");
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, building.GetStoreyOpacity(cameraA, 2));
            building.enabled = false;
            for (int i = 0; i < 3; i++) yield return null;
            colorA = Read(imageA);
            Assert.Greater(colorA.g, colorA.b + 0.1f, "Disabling cutaway restores the complete building.");
            Assert.IsTrue(upper.GetComponent<Collider>().enabled);
        }

        [UnityTest]
        public IEnumerator SavedBuildingHasAccessibleUpperFloorViaPhysicalStairs()
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
            foreach (var brain in roots.SelectMany(g => g.GetComponentsInChildren<Scrapfire.Enemies.AI.EnemyBrain>())) brain.enabled = false;
            foreach (var wave in roots.SelectMany(g => g.GetComponentsInChildren<Scrapfire.Waves.WaveDirector>())) wave.enabled = false;
            var player = roots.Single(g => g.name == "Capsule").GetComponent<CharacterPlayer>();
            var building = roots.Single(g => g.name == "PrototypeBuilding").GetComponent<BuildingVisibility>();
            // Physical stair traversal is checked after the two access gates open.
            foreach (var region in roots.SelectMany(g => g.GetComponentsInChildren<MapRegion>()))
                Assert.IsTrue(region.TryUnlock());
            var driver = player.GetComponent<PlayerInputDriver>(); driver.enabled = false;
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = new Vector3(-8f, 1.03f, 4f); motor.enabled = true;
            Physics.SyncTransforms();
            float deadline = Time.time + 5f;
            while (player.transform.position.z < 16f && Time.time < deadline)
            {
                driver.Move(Vector2.up);
                yield return null;
            }
            Assert.GreaterOrEqual(player.transform.position.z, 15.5f, "Player must reach the upstairs landing.");
            Assert.Greater(motor.bounds.min.y, 3.6f, "Stairs must physically carry the player up.");
            Assert.AreEqual(1, building.StoreyFor(player.PlayerCamera));
        }
    }
}
