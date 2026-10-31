using System.Collections.Generic;
using UnityEngine;

namespace Scrapfire.Levels
{
    /// <summary>One local view's observer. Remote actors without a local camera do not register a view.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class RegionViewObserver : MonoBehaviour
    {
        public Transform Target;
        public Camera ViewCamera { get; private set; }
        private static readonly HashSet<RegionViewObserver> Views = new HashSet<RegionViewObserver>();
        public static IEnumerable<RegionViewObserver> ActiveViews => Views;
        public bool CanRender => isActiveAndEnabled && ViewCamera != null && ViewCamera.isActiveAndEnabled
            && Target != null && Target.gameObject.activeInHierarchy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetViews() => Views.Clear();
        private void Awake() => ViewCamera = GetComponent<Camera>();
        private void OnEnable() { ViewCamera = GetComponent<Camera>(); Views.Add(this); }
        private void OnDisable() => Views.Remove(this);
    }
}
