using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Economy;
using Scrapfire.Enemies;
using Scrapfire.Interaction;
using Scrapfire.Levels;
using Scrapfire.Prototype;
using Scrapfire.Session;
using Scrapfire.Spawning;
using Scrapfire.Waves;

namespace Scrapfire.Editor
{
    /// <summary>One-time sample authoring only. Runtime rules never depend on this setup script.</summary>
    public static class SurvivalPrototypeSetup
    {
        [MenuItem("Scrapfire/Configure Survival Prototype")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play mode before configuring.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scrapfire/Scenes/SampleScene.unity")
                throw new System.InvalidOperationException("Open Scrapfire SampleScene first.");
            var player = Object.FindFirstObjectByType<CharacterPlayer>();
            if (player == null) throw new System.InvalidOperationException("SampleScene player missing.");
            Ensure<Balance>(player.gameObject);
            Ensure<CharacterAttributes>(player.gameObject);
            Ensure<PlayerInteractor>(player.gameObject);
            player.CharacterHealth.CanBeDowned = true;
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(player.CharacterHealth);

            const string configFolder = "Assets/Scrapfire/Settings/Survival";
            if (!AssetDatabase.IsValidFolder(configFolder)) AssetDatabase.CreateFolder("Assets/Scrapfire/Settings", "Survival");
            var enemyDefinition = Asset<EnemyDefinition>(configFolder + "/BasicEnemy.asset");
            var difficulty = Asset<DifficultySettings>(configFolder + "/Difficulty.asset");
            var existingEnemy = GameObject.Find("Enemy01");
            if (existingEnemy != null)
            {
                Ensure<EnemyBounty>(existingEnemy);
                var actor = Ensure<Enemy>(existingEnemy);
                if (actor.Definition == null) actor.Definition = enemyDefinition;
                EditorUtility.SetDirty(actor);
            }
            string prefabPath = "Assets/Scrapfire/Prefabs/SurvivalEnemy.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                var source = GameObject.Find("Enemy01");
                if (source == null) throw new System.InvalidOperationException("Enemy01 sample missing.");
                var copy = Object.Instantiate(source);
                copy.name = "SurvivalEnemy";
                copy.transform.position = Vector3.zero;
                var actor = Ensure<Enemy>(copy);
                actor.Definition = enemyDefinition;
                Ensure<EnemyBounty>(copy);
                var ai = copy.GetComponent<CharacterAi>();
                if (ai != null) ai.DisableOnDeath = true;
                prefab = PrefabUtility.SaveAsPrefabAsset(copy, prefabPath);
                Object.DestroyImmediate(copy);
            }

            var sequence = AssetDatabase.LoadAssetAtPath<WaveSequence>(configFolder + "/Waves.asset");
            if (sequence == null)
            {
                sequence = ScriptableObject.CreateInstance<WaveSequence>();
                sequence.Waves = new[] {
                    Wave("First contact", 5, 1f, prefab.GetComponent<Enemy>()),
                    Wave("Containment", 8, 1.2f, prefab.GetComponent<Enemy>()),
                    Wave("Overrun", 12, 1.5f, prefab.GetComponent<Enemy>())
                };
                AssetDatabase.CreateAsset(sequence, configFolder + "/Waves.asset");
            }

            var root = GameObject.Find("SurvivalSession");
            if (root == null) { root = new GameObject("SurvivalSession"); Undo.RegisterCreatedObjectUndo(root, "Configure survival"); }
            var roster = Ensure<SessionPlayers>(root);
            var rosterData = new SerializedObject(roster);
            var list = rosterData.FindProperty("_players");
            if (list.arraySize == 0)
            {
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = player;
                rosterData.ApplyModifiedProperties();
            }
            Region(root.transform, "BuildingEntranceRegion", "Building Entrance", 500,
                new[] { new RegionVolume(new Vector3(-4f, 4f, 11.5f), new Vector3(40.4f, 8.4f, 25.4f)) });
            Region(root.transform, "UpperFloorAndStairsRegion", "Upper Floor and Stairs", 750,
                new[] {
                    new RegionVolume(new Vector3(-4f, 6f, 11.5f), new Vector3(40.4f, 4.4f, 25.4f)),
                    new RegionVolume(new Vector3(-8f, 4.1f, 10f), new Vector3(4f, 8.2f, 10f))
                });
            ConfigureRegionPresentation();
            var spawner = Ensure<EntitySpawner>(root);
            if (spawner.Points.Length == 0)
            {
                spawner.Points = new[] {
                    Point(root.transform, "SpawnNorthA", new Vector3(-6f, 1f, 13f), null),
                    Point(root.transform, "SpawnNorthB", new Vector3(0f, 1f, 15f), null),
                    Point(root.transform, "SpawnNorthC", new Vector3(6f, 1f, 13f), null),
                    Point(root.transform, "SpawnSouthA", new Vector3(-6f, 1f, -13f), null),
                    Point(root.transform, "SpawnSouthB", new Vector3(0f, 1f, -15f), null),
                    Point(root.transform, "SpawnSouthC", new Vector3(6f, 1f, -13f), null),
                    Point(root.transform, "SpawnEast", new Vector3(18f, 1f, 12f), null),
                    Point(root.transform, "SpawnWest", new Vector3(-18f, 1f, -12f), null)
                };
            }
            var waves = Ensure<WaveDirector>(root);
            waves.Players = roster;
            waves.Spawner = spawner;
            if (waves.Sequence == null) waves.Sequence = sequence;
            if (waves.DifficultySettings == null) waves.DifficultySettings = difficulty;
            var hud = Ensure<PrototypeSessionHUD>(root);
            hud.Player = player;
            hud.Waves = waves;
            foreach (var component in root.GetComponents<MonoBehaviour>()) EditorUtility.SetDirty(component);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
        }

        [MenuItem("Scrapfire/Configure Region Presentation")]
        public static void ConfigureRegionPresentation()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var player = Object.FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None)
                .FirstOrDefault(p => p.PlayerCamera != null);
            if (player == null) throw new System.InvalidOperationException("Assign a local player camera first.");
            const string path = "Assets/Scrapfire/Materials/LockedRegionBoundary.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Scrapfire/Locked Region Boundary");
                if (shader == null) throw new System.InvalidOperationException("Import the boundary shader first.");
                material = new Material(shader) { name = "LockedRegionBoundary" };
                AssetDatabase.CreateAsset(material, path);
            }
            foreach (var region in Object.FindObjectsByType<MapRegion>(FindObjectsSortMode.None))
            {
                var view = Ensure<RegionBoundaryVisual>(region.gameObject);
                Undo.RecordObject(view, "Configure region presentation");
                if (view.BoundaryMaterial == null) view.BoundaryMaterial = material;
                EditorUtility.SetDirty(view);
            }
            var observer = Ensure<RegionViewObserver>(player.PlayerCamera.gameObject);
            observer.Target = player.transform;
            EditorUtility.SetDirty(observer);
            AssetDatabase.SaveAssets();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static WaveDefinition Wave(string name, int count, float health, Enemy prefab) => new WaveDefinition {
            Name = name, MaximumAlive = 6, HealthMultiplier = health,
            Groups = new[] { new WaveSpawnGroup { Prefab = prefab, Count = count, Interval = 1f } }
        };
        private static T Ensure<T>(GameObject go) where T : Component => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
        private static MapRegion Region(Transform parent, string name, string label, int price, RegionVolume[] volumes)
        {
            var existing = parent.Find(name);
            if (existing != null)
            {
                Ensure<RegionUnlockInteraction>(existing.gameObject);
                return existing.GetComponent<MapRegion>();
            }
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var region = go.AddComponent<MapRegion>();
            go.AddComponent<RegionUnlockInteraction>();
            region.RegionName = label;
            region.UnlockCost = price;
            region.Volumes = volumes;
            return region;
        }
        private static EnemySpawnPoint Point(Transform parent, string name, Vector3 position, MapRegion region)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var point = go.AddComponent<EnemySpawnPoint>();
            point.RequiredRegion = region;
            return point;
        }
    }
}
