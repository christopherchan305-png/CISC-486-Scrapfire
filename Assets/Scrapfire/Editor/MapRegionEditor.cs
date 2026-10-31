using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Scrapfire.Levels;

namespace Scrapfire.Editor
{
    [CustomEditor(typeof(MapRegion))]
    public sealed class MapRegionEditor : UnityEditor.Editor
    {
        private readonly BoxBoundsHandle _bounds = new BoxBoundsHandle();
        private bool _editing = true;
        private bool _previousToolsHidden;
        private int _selected;

        private void OnEnable()
        {
            _previousToolsHidden = Tools.hidden;
            Tools.hidden = _editing;
            Undo.undoRedoPerformed += OnUndoRedo;
        }
        private void OnDisable()
        {
            Tools.hidden = _previousToolsHidden;
            Undo.undoRedoPerformed -= OnUndoRedo;
        }
        private void OnUndoRedo()
        {
            if (target != null) ((MapRegion)target).NotifyGeometryChanged();
            SceneView.RepaintAll();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("Enable volume editing to drag the selected box's center and face handles in Scene. Disable it to move/rotate/scale the entire region normally. Undo is supported.", HelpBoxMessageType.Info));
            var edit = new Toggle("Edit volumes in Scene") { value = _editing };
            edit.RegisterValueChangedCallback(e => { _editing = e.newValue; Tools.hidden = _editing || _previousToolsHidden; SceneView.RepaintAll(); });
            root.Add(edit);
            var selected = new IntegerField("Selected volume (0-based)") { value = _selected };
            selected.RegisterValueChangedCallback(e => { _selected = Mathf.Max(0, e.newValue); SceneView.RepaintAll(); });
            root.Add(selected);
            root.Add(new Button(FrameVolume) { text = "Frame selected volume" });
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            root.TrackSerializedObjectValue(serializedObject, _ => {
                if (target != null) ((MapRegion)target).NotifyGeometryChanged();
                SceneView.RepaintAll();
            });
            return root;
        }

        private void FrameVolume()
        {
            var region = (MapRegion)target;
            if (region.Volumes == null || region.Volumes.Length == 0 || SceneView.lastActiveSceneView == null) return;
            _selected = Mathf.Clamp(_selected, 0, region.Volumes.Length - 1);
            var volume = region.Volumes[_selected];
            var scale = region.transform.lossyScale;
            var size = Vector3.Scale(volume.Size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            SceneView.lastActiveSceneView.Frame(new Bounds(region.transform.TransformPoint(volume.Center), size), false);
        }

        private void OnSceneGUI()
        {
            var region = (MapRegion)target;
            if (!_editing || region.Volumes == null || region.Volumes.Length == 0) return;
            _selected = Mathf.Clamp(_selected, 0, region.Volumes.Length - 1);
            var volume = region.Volumes[_selected];
            using (new Handles.DrawingScope(region.transform.localToWorldMatrix))
            {
                Handles.Label(volume.Center, $"{region.RegionName} / Volume {_selected}");
                _bounds.center = volume.Center;
                _bounds.size = volume.Size;
                _bounds.handleColor = new Color(1f, 0.6f, 0.1f);
                _bounds.wireframeColor = new Color(1f, 0.6f, 0.1f);
                EditorGUI.BeginChangeCheck();
                _bounds.DrawHandle();
                Vector3 center = Handles.PositionHandle(_bounds.center, Quaternion.identity);
                if (!EditorGUI.EndChangeCheck()) return;
                Undo.RecordObject(region, "Edit region volume");
                region.Volumes[_selected] = new RegionVolume(center, new Vector3(
                    Mathf.Max(0.01f, _bounds.size.x), Mathf.Max(0.01f, _bounds.size.y), Mathf.Max(0.01f, _bounds.size.z)));
                PrefabUtility.RecordPrefabInstancePropertyModifications(region);
                EditorUtility.SetDirty(region);
                region.NotifyGeometryChanged();
            }
        }
    }
}
