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
    public static class TwoStoreyBuildingSetup
    {
        private const float Rise = 3.8f;

        [MenuItem("Scrapfire/Upgrade Building To Two Storeys")]
        public static void Upgrade()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var building = GameObject.Find("PrototypeBuilding");
            if (building == null) throw new InvalidOperationException("PrototypeBuilding is missing.");
            Undo.IncrementCurrentGroup();
            int undo = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Build two-storey prototype");
            var ground = building.transform.Find("GroundFloor");
            if (ground == null)
            {
                var children = Enumerable.Range(0, building.transform.childCount).Select(building.transform.GetChild).ToArray();
                ground = Group(building.transform, "GroundFloor");
                foreach (var child in children) Undo.SetTransformParent(child, ground, "Group ground floor");
            }
            var upper = building.transform.Find("UpperFloor");
            var roof = building.transform.Find("Roof");
            if (upper == null)
            {
                upper = Group(building.transform, "UpperFloor");
                foreach (Transform source in ground)
                {
                    if (source.name == "Floor_40x25" || source.name == "Stairs") continue;
                    var copy = UnityEngine.Object.Instantiate(source.gameObject, upper, false);
                    copy.name = source.name;
                    copy.transform.localPosition += Vector3.up * Rise;
                }
                var floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scrapfire/Materials/PrototypeBuildingFloor.mat");
                var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scrapfire/Materials/PrototypeConcrete.mat");
                // Four slabs leave a real stairwell: x=-10..-6, z=5..15.
                Slab(upper, "West slab", -24f, -10f, -1f, 24f, floor);
                Slab(upper, "East slab", -6f, 16f, -1f, 24f, floor);
                Slab(upper, "South slab", -10f, -6f, -1f, 5f, floor);
                Slab(upper, "North slab", -10f, -6f, 15f, 24f, floor);
                Slab(upper, "Stair landing", -10f, -6f, 14f, 15f, floor);
                // Upper-floor copies of the ground entrances become windows, not fall-through doors.
                Cube(upper, "South window sill", new Vector3(0f, Rise + 0.5f, -1f), new Vector3(4f, 1f, 0.35f), wall, "Obstacles");
                Cube(upper, "East window sill", new Vector3(16f, Rise + 0.5f, 8.5f), new Vector3(0.35f, 1f, 3f), wall, "Obstacles");
                var stairs = Group(ground, "Stairs");
                var trim = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scrapfire/Materials/PrototypeStructureTrim.mat");
                for (int i = 1; i <= 16; i++)
                {
                    float height = Rise * i / 16f;
                    Cube(stairs, "Step " + i, new Vector3(-8f, 0.03f + height * 0.5f, 6f + (i - 0.5f) * 0.5f),
                        new Vector3(3f, height, 0.5f), trim, "Obstacles");
                }
            }
            if (roof == null)
            {
                roof = Group(building.transform, "Roof");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Scrapfire/Materials/PrototypeConcrete.mat");
                Cube(roof, "Roof slab", new Vector3(-4f, 0.03f + Rise * 2f - 0.1f, 11.5f),
                    new Vector3(40f, 0.2f, 25f), material, "Obstacles");
            }
            var visibility = building.GetComponent<BuildingVisibility>();
            if (visibility == null) visibility = Undo.AddComponent<BuildingVisibility>(building);
            Undo.RecordObject(visibility, "Configure storey cutaway");
            visibility.FootprintCenter = new Vector2(-4f, 11.5f);
            visibility.FootprintSize = new Vector2(40f, 25f);
            visibility.GroundHeight = 0.03f;
            visibility.StoreyHeight = Rise;
            visibility.StoreyCount = 2;
            visibility.VisualStoreys = new[] {
                new BuildingVisualStorey(0, ground), new BuildingVisualStorey(1, upper), new BuildingVisualStorey(2, roof)
            };
            EditorUtility.SetDirty(visibility);
            foreach (var player in UnityEngine.Object.FindObjectsByType<CharacterPlayer>(FindObjectsSortMode.None))
            {
                if (player.PlayerCamera == null) continue;
                var follow = player.PlayerCamera.GetComponent<TopDownCameraFollow>();
                Undo.RecordObject(follow, "Raise diagonal camera");
                Undo.RecordObject(player.PlayerCamera.transform, "Raise diagonal camera");
                follow.Offset = new Vector3(14.4f, 18f, -18f);
                follow.SnapToTarget();
                EditorUtility.SetDirty(follow);
            }
            Undo.CollapseUndoOperations(undo);
            Undo.FlushUndoRecordObjects();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = building;
        }

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create storey group");
            go.transform.SetParent(parent, false);
            return go.transform;
        }
        private static void Slab(Transform parent, string name, float minX, float maxX, float minZ, float maxZ, Material material)
        {
            Cube(parent, name, new Vector3((minX + maxX) * 0.5f, 0.03f + Rise - 0.1f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.2f, maxZ - minZ), material, "Ground");
        }
        private static void Cube(Transform parent, string name, Vector3 position, Vector3 size, Material material, string layerName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.layer = LayerMask.NameToLayer(layerName);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }
}
