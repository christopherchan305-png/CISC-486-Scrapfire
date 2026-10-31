using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace Scrapfire.Levels
{
    /// <summary>Local-view presentation only; never changes access, collision or purchases.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MapRegion))]
    public sealed class RegionBoundaryVisual : MonoBehaviour
    {
        public Material BoundaryMaterial;
        [Min(0.1f)] public float RevealDistance = 6f;
        [Min(0f)] public float FullRevealDistance = 1.2f;
        [Tooltip("Visible thickness of contour bands, including sides edge-on to an orthographic camera.")]
        [Min(0.01f)] public float ContourWidth = 0.12f;
        [Range(0f, 1f)] public float AreaOpacity = 0.22f;
        [Tooltip("Local-space height of the translucent ground overlay.")]
        public float SurfaceHeight = 0.06f;
        public bool IsVisible => _visibleCameras.Count > 0;
        public bool IsVisibleTo(Camera camera) => camera != null && _visibleCameras.Contains(camera);
        private MapRegion _region;
        private Mesh _mesh;
        private Mesh _surface;
        private readonly HashSet<Camera> _visibleCameras = new HashSet<Camera>();
        private MaterialPropertyBlock _properties;
        private static readonly int ObserverPosition = Shader.PropertyToID("_ObserverPosition");
        private static readonly int Reveal = Shader.PropertyToID("_RevealDistance");
        private static readonly int FullReveal = Shader.PropertyToID("_FullRevealDistance");

        private void Awake()
        {
            _region = GetComponent<MapRegion>();
            _properties = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            _region.Unlocked += OnUnlocked;
            _region.GeometryChanged += Rebuild;
            Rebuild();
        }
        private void OnDisable()
        {
            _region.Unlocked -= OnUnlocked;
            _region.GeometryChanged -= Rebuild;
            _visibleCameras.Clear();
        }
        private void OnUnlocked(MapRegion region) => _visibleCameras.Clear();

        public void Rebuild()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_surface != null) Destroy(_surface);
            _mesh = RegionBoundaryMesh.Build(_region.Volumes, ContourWidth);
            _surface = RegionBoundaryMesh.BuildSurface(_region.Volumes, SurfaceHeight);
        }

        private void LateUpdate()
        {
            _visibleCameras.Clear();
            if (BoundaryMaterial == null || !_region.isActiveAndEnabled || _region.IsUnlocked) return;
            foreach (var observer in RegionViewObserver.ActiveViews)
            {
                if (observer == null || !observer.CanRender || _region.DistanceTo(observer.Target.position) >= RevealDistance) continue;
                Camera camera = observer.ViewCamera;
                _visibleCameras.Add(camera);
                _properties.SetVector(ObserverPosition, observer.Target.position);
                _properties.SetFloat(Reveal, RevealDistance);
                _properties.SetFloat(FullReveal, Mathf.Clamp(FullRevealDistance, 0f, RevealDistance - 0.001f));
                _properties.SetFloat("_SurfaceFill", 0f);
                DrawForCamera(_mesh, camera, transform.localToWorldMatrix);
                _properties.SetFloat("_SurfaceFill", 1f);
                _properties.SetFloat("_FillOpacity", AreaOpacity);
                float elevation = BuildingVisibility.SurfaceElevationFor(observer.Target);
                DrawForCamera(_surface, camera, Matrix4x4.Translate(Vector3.up * elevation) * transform.localToWorldMatrix);
            }
        }

        private void DrawForCamera(Mesh mesh, Camera camera, Matrix4x4 matrix)
        {
            // Always supply a camera. A null camera would leak this player's warning into every view.
            Graphics.DrawMesh(mesh, matrix, BoundaryMaterial, gameObject.layer,
                camera, 0, _properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
            if (_surface != null) Destroy(_surface);
        }
    }
}
