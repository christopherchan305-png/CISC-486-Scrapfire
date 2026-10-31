using System;
using UnityEngine;

namespace Scrapfire.Levels
{
    [Serializable]
    public struct RegionVolume
    {
        public Vector3 Center;
        public Vector3 Size;
        public RegionVolume(Vector3 center, Vector3 size) { Center = center; Size = size; }
    }

    /// <summary>Union of local-space boxes. Solid volumes exclude entry until purchased.</summary>
    [DisallowMultipleComponent]
    public sealed class MapRegion : MonoBehaviour
    {
        public string RegionName = "New area";
        [Min(0)] public int UnlockCost = 500;
        public bool InitiallyUnlocked;
        public RegionVolume[] Volumes = { new RegionVolume(Vector3.zero, new Vector3(10f, 8f, 10f)) };
        public bool IsUnlocked { get; private set; }
        public event Action<MapRegion> Unlocked;
        public event Action GeometryChanged;
        private GameObject _blockers;

        private void Awake()
        {
            IsUnlocked = InitiallyUnlocked;
            RebuildBlockers();
        }

        public void RebuildBlockers()
        {
            if (_blockers != null)
            {
                _blockers.SetActive(false);
                Destroy(_blockers);
            }
            _blockers = new GameObject("Region blockers");
            int barrierLayer = LayerMask.NameToLayer(RegionMovementBarrier.LayerName);
            _blockers.layer = barrierLayer >= 0 ? barrierLayer : gameObject.layer;
            _blockers.AddComponent<RegionMovementBarrier>();
            _blockers.transform.SetParent(transform, false);
            foreach (var volume in Volumes ?? Array.Empty<RegionVolume>())
            {
                var collider = _blockers.AddComponent<BoxCollider>();
                collider.center = volume.Center;
                collider.size = PositiveSize(volume.Size);
            }
            _blockers.SetActive(!IsUnlocked);
            GeometryChanged?.Invoke();
        }

        public void NotifyGeometryChanged()
        {
            if (Application.isPlaying) RebuildBlockers();
            else GeometryChanged?.Invoke();
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            foreach (var volume in Volumes ?? Array.Empty<RegionVolume>())
                if (new Bounds(volume.Center, PositiveSize(volume.Size)).Contains(local)) return true;
            return false;
        }

        public float DistanceTo(Vector3 position)
        {
            float nearest = float.PositiveInfinity;
            foreach (var volume in Volumes ?? Array.Empty<RegionVolume>())
            {
                Vector3 local = transform.InverseTransformPoint(position);
                Vector3 closest = new Bounds(volume.Center, PositiveSize(volume.Size)).ClosestPoint(local);
                nearest = Mathf.Min(nearest, Vector3.Distance(position, transform.TransformPoint(closest)));
            }
            return nearest;
        }

        public bool IsWithinReach(Vector3 position, float reach) => DistanceTo(position) <= reach;

        // Purchase/quest/network adapters authorize access outside the geometry module.
        public bool TryUnlock()
        {
            if (!isActiveAndEnabled || IsUnlocked) return false;
            IsUnlocked = true;
            _blockers.SetActive(false);
            Unlocked?.Invoke(this);
            return true;
        }

        private static Vector3 PositiveSize(Vector3 size) => new Vector3(
            Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));

        private void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = (Application.isPlaying ? IsUnlocked : InitiallyUnlocked)
                ? new Color(0f, 1f, 0.4f, 0.25f) : new Color(1f, 0.5f, 0f, 0.3f);
            if (Volumes == null) return;
            foreach (var volume in Volumes)
            {
                Gizmos.DrawCube(volume.Center, PositiveSize(volume.Size));
                Gizmos.DrawWireCube(volume.Center, PositiveSize(volume.Size));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
