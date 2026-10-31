using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using Scrapfire.Characters.Core;
using Scrapfire.Enemies;
using Scrapfire.Enemies.AI;
using Scrapfire.Levels;
using Scrapfire.Session;
using Scrapfire.Spawning;
using Scrapfire.Waves;
using Object = UnityEngine.Object;

namespace Scrapfire.Editor
{
    /// <summary>Explicit authoring tool. Runtime AI does not know this sample scene or the demo asset paths.</summary>
    public static class EnemyAIPrototypeSetup
    {
        private const string Folder = "Assets/Scrapfire/Settings/Survival";
        private const string AgentName = "Scrapfire Zombie";

        [MenuItem("Scrapfire/Configure Enemy AI And Bake Navigation")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scrapfire/Scenes/SampleScene.unity") throw new InvalidOperationException("Open Scrapfire SampleScene first.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Configure enemy AI prototype");
            int agentType = ConfigureAgentType();
            var seek = Profile("ZombieSeek", EnemyChaseMode.Seek);
            var pursue = Profile("ZombiePursue", EnemyChaseMode.Pursue);
            var animator = AnimationController();
            var root = GameObject.Find("SurvivalNavigation");
            if (root == null) { root = new GameObject("SurvivalNavigation"); Undo.RegisterCreatedObjectUndo(root, "Create navigation"); }
            var surface = root.GetComponent<NavMeshSurface>() ?? Undo.AddComponent<NavMeshSurface>(root);
            Undo.RecordObject(surface, "Configure navigation surface");
            surface.agentTypeID = agentType;
            surface.collectObjects = CollectObjects.Volume;
            surface.center = new Vector3(0f, 4f, 0f); surface.size = new Vector3(52f, 14f, 52f);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = LayerMask.GetMask("Default", "Ground", "Obstacles");
            surface.overrideVoxelSize = true; surface.voxelSize = 0.08f;
            surface.buildHeightMesh = true;
            var roof = GameObject.Find("PrototypeBuilding").transform.Find("Roof");
            var modifier = roof.GetComponent<NavMeshModifier>() ?? Undo.AddComponent<NavMeshModifier>(roof.gameObject);
            modifier.overrideArea = true; modifier.area = 1;
            EditorUtility.SetDirty(modifier);
            foreach (var region in Object.FindObjectsByType<MapRegion>(FindObjectsSortMode.None))
                if (region.GetComponent<RegionNavigationGate>() == null) Undo.AddComponent<RegionNavigationGate>(region.gameObject);
            foreach (var player in Object.FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None))
                if (player.GetComponent<TargetMotion>() == null) Undo.AddComponent<TargetMotion>(player.gameObject);

            ConfigurePrefab("Assets/Scrapfire/Prefabs/SurvivalEnemy.prefab", seek, agentType, animator);
            const string pursuitPath = "Assets/Scrapfire/Prefabs/PursuitEnemy.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(pursuitPath) == null)
                AssetDatabase.CopyAsset("Assets/Scrapfire/Prefabs/SurvivalEnemy.prefab", pursuitPath);
            ConfigurePrefab(pursuitPath, pursue, agentType, animator);
            var sceneEnemy = GameObject.Find("Enemy01");
            if (sceneEnemy != null) ConfigureActor(sceneEnemy, seek, agentType, animator);
            foreach (var point in Object.FindObjectsByType<EnemySpawnPoint>(FindObjectsSortMode.None))
            {
                Undo.RecordObject(point, "Require safe navigation spawn");
                point.RequireNavMesh = true; point.NavigationAgentType = agentType;
                EditorUtility.SetDirty(point);
            }
            var sequence = AssetDatabase.LoadAssetAtPath<WaveSequence>(Folder + "/Waves.asset");
            var seeker = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Scrapfire/Prefabs/SurvivalEnemy.prefab").GetComponent<Enemy>();
            var pursuer = AssetDatabase.LoadAssetAtPath<GameObject>(pursuitPath).GetComponent<Enemy>();
            if (sequence != null && sequence.Waves.Length >= 3 && sequence.Waves[1].Groups.Length == 1 && sequence.Waves[2].Groups.Length == 1)
            {
                Undo.RecordObject(sequence, "Include pursuing enemies in later waves");
                sequence.Waves[1].Groups = new[] { new WaveSpawnGroup { Prefab = seeker, Count = 6, Interval = 1f }, new WaveSpawnGroup { Prefab = pursuer, Count = 2, Interval = 1f } };
                sequence.Waves[2].Groups = new[] { new WaveSpawnGroup { Prefab = seeker, Count = 8, Interval = 1f }, new WaveSpawnGroup { Prefab = pursuer, Count = 4, Interval = 1f } };
                EditorUtility.SetDirty(sequence);
            }

            Bake(surface);
            AssetDatabase.SaveAssets();
            Undo.CollapseUndoOperations(undo);
            Undo.FlushUndoRecordObjects();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
        }

        private static int ConfigureAgentType()
        {
            int id = -1;
            for (int i = 0; i < NavMesh.GetSettingsCount(); i++)
                if (NavMesh.GetSettingsNameFromID(NavMesh.GetSettingsByIndex(i).agentTypeID) == AgentName) id = NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (id == -1) id = NavMesh.CreateSettings().agentTypeID;
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("NavMeshProjectSettings"));
            var list = settings.FindProperty("m_Settings");
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("agentTypeID").intValue != id) continue;
                settings.FindProperty("m_SettingNames").GetArrayElementAtIndex(i).stringValue = AgentName;
                entry.FindPropertyRelative("agentRadius").floatValue = 0.4f;
                entry.FindPropertyRelative("agentHeight").floatValue = 1.8f;
                entry.FindPropertyRelative("agentClimb").floatValue = 0.35f;
                entry.FindPropertyRelative("agentSlope").floatValue = 50f;
            }
            settings.ApplyModifiedProperties();
            return id;
        }

        private static EnemyAIProfile Profile(string name, EnemyChaseMode mode)
        {
            string path = Folder + "/" + name + ".asset";
            var profile = AssetDatabase.LoadAssetAtPath<EnemyAIProfile>(path);
            if (profile != null) return profile;
            profile = ScriptableObject.CreateInstance<EnemyAIProfile>(); profile.ChaseMode = mode;
            profile.SightBlockingLayers = LayerMask.GetMask("Default", "Ground", "Obstacles");
            profile.AlertBlockingLayers = profile.SightBlockingLayers;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }

        private static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__"));
        private static AnimatorController AnimationController()
        {
            const string path = Folder + "/ZombieAnimator.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller != null) return controller;
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            var sm = controller.layers[0].stateMachine;
            var locomotion = sm.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle and walk", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Clip("Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Idle.fbx"), 0f);
            tree.AddChild(Clip("Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Walking.fbx"), 3f);
            locomotion.motion = tree; sm.defaultState = locomotion;
            var attack = sm.AddState("Attack");
            attack.motion = Clip("Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Suit/LoftSuit@StandingMeleeAttackHorizontal.fbx");
            var enter = sm.AddAnyStateTransition(attack); enter.hasExitTime = false; enter.duration = 0.08f; enter.canTransitionToSelf = false;
            enter.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            var exit = attack.AddTransition(locomotion); exit.hasExitTime = true; exit.exitTime = 0.8f; exit.duration = 0.1f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigurePrefab(string path, EnemyAIProfile profile, int type, AnimatorController animator)
        {
            var go = PrefabUtility.LoadPrefabContents(path);
            try { ConfigureActor(go, profile, type, animator); PrefabUtility.SaveAsPrefabAsset(go, path); }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        private static void ConfigureActor(GameObject go, EnemyAIProfile profile, int type, AnimatorController controller)
        {
            go.transform.localScale = Vector3.one;
            var collider = go.GetComponent<BoxCollider>();
            if (collider != null) { collider.size = new Vector3(0.8f, 2f, 0.8f); collider.center = Vector3.zero; }
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent == null) agent = go.AddComponent<NavMeshAgent>();
            agent.enabled = false; agent.agentTypeID = type;
            agent.radius = 0.4f; agent.height = 1.8f; agent.baseOffset = 1f;
            agent.speed = 3f; agent.acceleration = 12f; agent.angularSpeed = 360f;
            agent.autoBraking = true; agent.autoRepath = true;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            var brain = go.GetComponent<EnemyBrain>() ?? go.AddComponent<EnemyBrain>();
            brain.Profile = profile;
            if (go.GetComponent<EnemyHearing>() == null) go.AddComponent<EnemyHearing>();
            var visual = go.transform.Find("ZombieVisual");
            if (visual == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TopDownEngine/Demos/Loft3D/Models/Characters/Tie/LoftTie@Idle.fbx");
                var model = Object.Instantiate(source, go.transform, false);
                model.name = "ZombieVisual"; model.transform.localPosition = Vector3.down;
                model.transform.localRotation = Quaternion.identity;
                visual = model.transform;
            }
            var rootRenderer = go.GetComponent<MeshRenderer>(); if (rootRenderer != null) Object.DestroyImmediate(rootRenderer);
            var rootMesh = go.GetComponent<MeshFilter>(); if (rootMesh != null) Object.DestroyImmediate(rootMesh);
            // Some animation FBX files also contain the DCC preview camera/light.
            // Keep only their model and animation; those cameras must never render the game.
            foreach (var camera in visual.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(camera.gameObject);
            foreach (var listener in visual.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(listener);
            foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light.gameObject);
            var animator = visual.GetComponentInChildren<Animator>();
            animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
            var animation = go.GetComponent<EnemyAnimationDriver>() ?? go.AddComponent<EnemyAnimationDriver>();
            animation.Animator = animator;
            go.GetComponent<CharacterAi>().CharacterModel = visual.gameObject;
            foreach (var component in go.GetComponents<Component>()) if (component != null) EditorUtility.SetDirty(component);
        }

        public static void Bake(NavMeshSurface surface)
        {
            var original = surface.navMeshData;
            surface.BuildNavMesh();
            if (surface.navMeshData == null) throw new InvalidOperationException("Navigation bake produced no data.");
            if (original != null && AssetDatabase.Contains(original))
            {
                var fresh = surface.navMeshData;
                surface.RemoveData(); EditorUtility.CopySerialized(fresh, original);
                surface.navMeshData = original; surface.AddData(); Object.DestroyImmediate(fresh);
                EditorUtility.SetDirty(original);
            }
            else AssetDatabase.CreateAsset(surface.navMeshData, Folder + "/SurvivalNavigation.asset");
            EditorUtility.SetDirty(surface);
        }

        [MenuItem("Scrapfire/Rebake Survival Navigation")]
        public static void Rebake()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var surface = GameObject.Find("SurvivalNavigation")?.GetComponent<NavMeshSurface>();
            if (surface == null) throw new InvalidOperationException("Configure the prototype navigation first.");
            Bake(surface);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(surface.gameObject.scene);
            EditorSceneManager.SaveScene(surface.gameObject.scene);
        }
    }
}
