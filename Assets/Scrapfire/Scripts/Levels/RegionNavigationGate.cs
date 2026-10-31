using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Scrapfire.Levels
{
    /// <summary>Navigation adapter for an existing access region. Static geometry is baked open.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MapRegion))]
    public sealed class RegionNavigationGate : MonoBehaviour
    {
        private MapRegion _region;
        private readonly List<GameObject> _obstacles = new List<GameObject>();
        private void Awake() => _region = GetComponent<MapRegion>();
        private void OnEnable()
        {
            _region.Unlocked += Open;
            _region.GeometryChanged += Rebuild;
            Rebuild();
        }
        private void OnDisable()
        {
            _region.Unlocked -= Open;
            _region.GeometryChanged -= Rebuild;
            Clear();
        }
        private void Open(MapRegion region) => Clear();
        private void Clear()
        {
            foreach (var obstacle in _obstacles)
                if (obstacle != null) { obstacle.SetActive(false); Destroy(obstacle); }
            _obstacles.Clear();
        }
        private void Rebuild()
        {
            Clear();
            if (_region.IsUnlocked) return;
            foreach (var volume in _region.Volumes)
            {
                var go = new GameObject("Locked navigation volume");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = volume.Center;
                var obstacle = go.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(Mathf.Max(0.01f, volume.Size.x), Mathf.Max(0.01f, volume.Size.y), Mathf.Max(0.01f, volume.Size.z));
                obstacle.carving = true;
                obstacle.carveOnlyStationary = false;
                _obstacles.Add(go);
            }
        }
    }
}
