using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;
using Scrapfire.Levels;

namespace Scrapfire.Editor
{
    /// <summary>Authoring utility only: scene geometry stays as ordinary editable GameObjects.</summary>
    public static class PrototypeEnvironmentSetup
    {
        private struct Opening
        {
            public float From, To, Sill, Head;
            public Opening(float from, float to, float sill, float head)
            { From = from; To = to; Sill = sill; Head = head; }
        }

        [MenuItem("Scrapfire/Configure Prototype Environment")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            ConfigureBarrierLayers();
            foreach (var player in UnityEngine.Object.FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None))
            {
                CharacterCollisionLayers.Assign(player.gameObject, "Player");
                if (player.PlayerCamera == null) continue;
                var observer = player.PlayerCamera.GetComponent<RegionViewObserver>();
                if (observer == null) observer = Undo.AddComponent<RegionViewObserver>(player.PlayerCamera.gameObject);
                observer.Target = player.transform;
                var follow = player.PlayerCamera.GetComponent<TopDownCameraFollow>();
                Undo.RecordObject(follow, "Tilt prototype camera");
                follow.Offset = new Vector3(16f, 20f, -20f);
                Undo.RecordObject(player.PlayerCamera, "Use perspective camera");
                Undo.RecordObject(player.PlayerCamera.transform, "Tilt prototype camera");
                player.PlayerCamera.orthographic = false;
                player.PlayerCamera.fieldOfView = 55f;
                follow.SnapToTarget();
                EditorUtility.SetDirty(observer); EditorUtility.SetDirty(follow); EditorUtility.SetDirty(player.PlayerCamera);
            }
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<Characters.Core.CharacterAi>(FindObjectsSortMode.None))
                CharacterCollisionLayers.Assign(enemy.gameObject, "Enemies");
            if (GameObject.Find("PrototypeBuilding") == null) BuildStructure();
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        public static void ConfigureBarrierLayers()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
            var serialized = new SerializedObject(settings);
            var layers = serialized.FindProperty("layers");
            int barrier = LayerMask.NameToLayer(RegionMovementBarrier.LayerName);
            if (barrier < 0)
            {
                for (int i = 8; i < 32; i++)
                    if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                    { barrier = i; layers.GetArrayElementAtIndex(i).stringValue = RegionMovementBarrier.LayerName; break; }
                if (barrier < 0) throw new InvalidOperationException("No free layer for RegionBarrier.");
                serialized.ApplyModifiedProperties();
            }
            int player = LayerMask.NameToLayer("Player"), enemy = LayerMask.NameToLayer("Enemies");
            if (player < 0 || enemy < 0) throw new InvalidOperationException("Player and Enemies layers must exist.");
            for (int i = 0; i < 32; i++) Physics.IgnoreLayerCollision(barrier, i, i != player && i != enemy);
            var physics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset")[0];
            EditorUtility.SetDirty(physics);
            AssetDatabase.SaveAssets();
        }

        private static Material Material(string name, Color color)
        {
            string path = "Assets/Scrapfire/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void BuildStructure()
        {
            var root = new GameObject("PrototypeBuilding");
            Undo.RegisterCreatedObjectUndo(root, "Build prototype structure");
            var wall = Material("PrototypeConcrete", new Color(0.48f, 0.53f, 0.58f));
            var trim = Material("PrototypeStructureTrim", new Color(0.22f, 0.29f, 0.34f));
            var floor = Material("PrototypeBuildingFloor", new Color(0.19f, 0.23f, 0.26f));
            // Footprint x=[-24,16], z=[-1,24]: 40*25=1000 m², 40% of the 50*50 arena.
            Cube(root.transform, "Floor_40x25", new Vector3(-4f, -0.01f, 11.5f), new Vector3(40f, 0.08f, 25f), floor, "Ground");
            Wall(root.transform, "South", false, -1f, -24f, 16f, wall, new[] {
                new Opening(-18f,-14f,1f,2.5f), new Opening(-10f,-6f,1f,2.5f),
                new Opening(-2f,2f,0f,2.7f), new Opening(6f,10f,1f,2.5f) });
            Wall(root.transform, "North", false, 24f, -24f, 16f, wall, new[] {
                new Opening(-16f,-12f,1f,2.5f),new Opening(-4f,0f,1f,2.5f),new Opening(8f,12f,1f,2.5f) });
            Wall(root.transform, "West", true, -24f, -1f, 24f, wall, new[] {
                new Opening(4f,8f,1f,2.5f),new Opening(13f,17f,1f,2.5f) });
            Wall(root.transform, "East", true, 16f, -1f, 24f, wall, new[] {
                new Opening(7f,10f,0f,2.7f),new Opening(15f,19f,1f,2.5f) });
            foreach (float x in new[] { -24f, 16f })
                foreach (float z in new[] { -1f, 24f })
                    Cube(root.transform, "Corner pillar", new Vector3(x, 1.85f, z), new Vector3(0.6f, 3.7f, 0.6f), trim, "Obstacles");
            Cube(root.transform, "North roof rim", new Vector3(-4f, 3.65f, 24f), new Vector3(40f, 0.16f, 0.65f), trim, "Obstacles");
            Cube(root.transform, "South roof rim", new Vector3(-4f, 3.65f, -1f), new Vector3(40f, 0.16f, 0.65f), trim, "Obstacles");
            // Open roof intentionally keeps the interior playable from the top-down camera.
        }

        private static void Wall(Transform parent, string name, bool alongZ, float fixedPosition,
            float from, float to, Material material, Opening[] openings)
        {
            var group = new GameObject(name + " wall").transform;
            group.SetParent(parent, false);
            float cursor = from;
            foreach (var opening in openings.OrderBy(o => o.From))
            {
                Section(cursor, opening.From, 0f, 3.6f, "Solid");
                Section(opening.From, opening.To, 0f, opening.Sill, "Window sill");
                Section(opening.From, opening.To, opening.Head, 3.6f, "Opening lintel");
                cursor = opening.To;
            }
            Section(cursor, to, 0f, 3.6f, "Solid");

            void Section(float start, float end, float bottom, float top, string label)
            {
                if (end <= start || top <= bottom) return;
                float middle = (start + end) * 0.5f, y = (bottom + top) * 0.5f;
                Vector3 position = alongZ ? new Vector3(fixedPosition, y, middle) : new Vector3(middle, y, fixedPosition);
                Vector3 size = alongZ ? new Vector3(0.35f, top - bottom, end - start) : new Vector3(end - start, top - bottom, 0.35f);
                Cube(group, label, position, size, material, "Obstacles");
            }
        }

        private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material, string layerName)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = size;
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) cube.layer = layer;
            cube.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
