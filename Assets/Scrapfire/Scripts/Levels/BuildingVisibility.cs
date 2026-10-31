using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Scrapfire.Levels
{
    [Serializable]
    public struct BuildingVisualStorey
    {
        [Min(0)] public int Storey;
        public Transform Root;
        public BuildingVisualStorey(int storey, Transform root) { Storey = storey; Root = root; }
    }

    /// <summary>Per-camera upper-storey cutaway. Renderers and shadows are restored after each view;
    /// GameObjects, colliders and gameplay remain active throughout.</summary>
    [DisallowMultipleComponent]
    public sealed class BuildingVisibility : MonoBehaviour
    {
        public Vector2 FootprintCenter = new Vector2(-4f, 11.5f);
        public Vector2 FootprintSize = new Vector2(40f, 25f);
        public float GroundHeight = 0.03f;
        [Min(0.1f)] public float StoreyHeight = 3.8f;
        [Min(1)] public int StoreyCount = 2;
        [Min(0f)] public float FadeDuration = 0.5f;
        [Tooltip("Transparent material references keep the required URP shader variants in player builds.")]
        public Material[] FadeVariants = Array.Empty<Material>();
        [Tooltip("Ground floor = 0, upper floor = 1, roof = StoreyCount.")]
        public BuildingVisualStorey[] VisualStoreys = Array.Empty<BuildingVisualStorey>();
        private readonly List<(Renderer renderer, int storey)> _renderers = new List<(Renderer, int)>();
        private readonly Stack<ViewState> _views = new Stack<ViewState>();
        private readonly Dictionary<Renderer, RendererState> _baseline = new Dictionary<Renderer, RendererState>();
        private readonly Dictionary<Camera, CameraFade> _fades = new Dictionary<Camera, CameraFade>();
        private static readonly HashSet<BuildingVisibility> Buildings = new HashSet<BuildingVisibility>();

        private struct RendererState
        {
            public Renderer Renderer;
            public bool ForcedOff;
            public ShadowCastingMode Shadows;
            public Material[] Materials;
        }
        private sealed class CameraFade
        {
            public double LastTime = Time.unscaledTimeAsDouble;
            public readonly Dictionary<int, float> Opacity = new Dictionary<int, float>();
            public readonly Dictionary<(Material, int), Material> Materials = new Dictionary<(Material, int), Material>();
        }
        private sealed class ViewState
        {
            public Camera Camera;
            public readonly List<RendererState> Previous = new List<RendererState>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetBuildings() => Buildings.Clear();
        private void OnEnable()
        {
            Buildings.Add(this);
            RefreshRenderers();
            RenderPipelineManager.beginCameraRendering += BeginView;
            RenderPipelineManager.endCameraRendering += EndView;
        }
        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginView;
            RenderPipelineManager.endCameraRendering -= EndView;
            RestoreAll();
            ReleaseFadeMaterials();
            Buildings.Remove(this);
        }

        public void RefreshRenderers()
        {
            RestoreAll();
            ReleaseFadeMaterials();
            _renderers.Clear();
            var seen = new HashSet<Renderer>();
            foreach (var visual in VisualStoreys)
                if (visual.Root != null)
                    foreach (var renderer in visual.Root.GetComponentsInChildren<Renderer>(true))
                        if (seen.Add(renderer)) _renderers.Add((renderer, visual.Storey));
        }

        public int StoreyAt(Vector3 worldPoint)
        {
            Vector3 point = transform.InverseTransformPoint(worldPoint);
            Vector2 offset = new Vector2(point.x, point.z) - FootprintCenter;
            if (Mathf.Abs(offset.x) >= FootprintSize.x * 0.5f || Mathf.Abs(offset.y) >= FootprintSize.y * 0.5f
                || point.y < GroundHeight - 0.15f || point.y >= GroundHeight + StoreyCount * StoreyHeight)
                return -1;
            return Mathf.Clamp(Mathf.FloorToInt((point.y - GroundHeight + 0.05f) / StoreyHeight), 0, StoreyCount - 1);
        }

        public static Vector3 FootPosition(Transform target)
        {
            var motor = target.GetComponent<CharacterController>();
            return motor != null && motor.enabled
                ? new Vector3(target.position.x, motor.bounds.min.y + 0.02f, target.position.z)
                : target.position;
        }

        public static float SurfaceElevationFor(Transform target)
        {
            if (target == null) return 0f;
            Vector3 foot = FootPosition(target);
            foreach (var building in Buildings)
            {
                if (building == null || !building.isActiveAndEnabled) continue;
                int storey = building.StoreyAt(foot);
                if (storey >= 0) return building.transform.TransformPoint(Vector3.up * (building.GroundHeight + storey * building.StoreyHeight)).y;
            }
            return 0f;
        }

        public int StoreyFor(Camera camera)
        {
            var observer = camera != null ? camera.GetComponent<RegionViewObserver>() : null;
            return observer != null && observer.CanRender ? StoreyAt(FootPosition(observer.Target)) : -1;
        }

        public float GetStoreyOpacity(Camera camera, int storey) => camera != null
            && _fades.TryGetValue(camera, out var fade) && fade.Opacity.TryGetValue(storey, out float opacity) ? opacity : 1f;

        private CameraFade AdvanceFade(Camera camera, int current)
        {
            if (!_fades.TryGetValue(camera, out var fade))
            { fade = new CameraFade(); _fades.Add(camera, fade); }
            double now = Time.unscaledTimeAsDouble;
            float step = FadeDuration <= 0f ? 1f : (float)(now - fade.LastTime) / FadeDuration;
            fade.LastTime = now;
            foreach (var entry in _renderers)
            {
                if (!fade.Opacity.ContainsKey(entry.storey)) fade.Opacity.Add(entry.storey, 1f);
            }
            // Storeys, rather than renderer count, determine the transition speed.
            foreach (var storey in new List<int>(fade.Opacity.Keys))
                fade.Opacity[storey] = Mathf.MoveTowards(fade.Opacity[storey],
                    current >= 0 && storey > current ? 0f : 1f, step);
            return fade;
        }

        private static Material FadeMaterial(CameraFade fade, Material original, int storey, float opacity)
        {
            if (original == null) return null;
            var key = (original, storey);
            if (!fade.Materials.TryGetValue(key, out var material))
            {
                material = new Material(original) { name = original.name + " (camera fade)", hideFlags = HideFlags.HideAndDontSave };
                // The prototype uses URP Lit/Unlit; preserve their textures and lighting.
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.SetShaderPassEnabled("DepthOnly", false);
                fade.Materials.Add(key, material);
            }
            string colorProperty = original.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            if (original.HasProperty(colorProperty))
            {
                Color color = original.GetColor(colorProperty);
                color.a *= opacity;
                material.SetColor(colorProperty, color);
            }
            return material;
        }

        private void ReleaseFadeMaterials()
        {
            foreach (var fade in _fades.Values)
                foreach (var material in fade.Materials.Values)
                    if (material != null) Destroy(material);
            _fades.Clear();
        }

        private void BeginView(ScriptableRenderContext context, Camera camera)
        {
            if (_views.Count == 0) _baseline.Clear();
            int current = StoreyFor(camera);
            var fade = AdvanceFade(camera, current);
            var state = new ViewState { Camera = camera };
            foreach (var entry in _renderers)
            {
                var renderer = entry.renderer;
                if (renderer == null) continue;
                var previous = new RendererState { Renderer = renderer, ForcedOff = renderer.forceRenderingOff,
                    Shadows = renderer.shadowCastingMode, Materials = renderer.sharedMaterials };
                state.Previous.Add(previous);
                if (!_baseline.TryGetValue(renderer, out var original))
                { original = previous; _baseline.Add(renderer, original); }
                float opacity = fade.Opacity[entry.storey];
                renderer.forceRenderingOff = original.ForcedOff || opacity <= 0f;
                renderer.shadowCastingMode = opacity < 1f ? ShadowCastingMode.Off : original.Shadows;
                var materials = (Material[])original.Materials.Clone();
                if (opacity > 0f && opacity < 1f)
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = FadeMaterial(fade, materials[i], entry.storey, opacity);
                renderer.sharedMaterials = materials;
            }
            _views.Push(state);
        }
        private void EndView(ScriptableRenderContext context, Camera camera)
        {
            if (_views.Count == 0 || _views.Peek().Camera != camera) return;
            Restore(_views.Pop());
            if (_views.Count == 0) _baseline.Clear();
        }
        private static void Restore(ViewState state)
        {
            foreach (var original in state.Previous)
                if (original.Renderer != null)
                {
                    original.Renderer.forceRenderingOff = original.ForcedOff;
                    original.Renderer.shadowCastingMode = original.Shadows;
                    original.Renderer.sharedMaterials = original.Materials;
                }
        }
        private void RestoreAll()
        {
            while (_views.Count > 0) Restore(_views.Pop());
            _baseline.Clear();
        }
    }
}
