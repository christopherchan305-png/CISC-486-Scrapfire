using System;
using System.Collections.Generic;
using UnityEngine;
using Scrapfire.Enemies;
using Scrapfire.Levels;
using UnityEngine.Scripting.APIUpdating;

namespace Scrapfire.Spawning
{
    [MovedFrom(true, "Scrapfire.Spawning", "Scrapfire", "EnemySpawner")]
    public sealed class EntitySpawner : MonoBehaviour
    {
        public EnemySpawnPoint[] Points = Array.Empty<EnemySpawnPoint>();
        [Tooltip("Additional region gates. Active scene regions are also checked automatically by position.")]
        public MapRegion[] Regions = Array.Empty<MapRegion>();
        [Tooltip("Explicit obstacle layers only. Do not include the ground layer.")]
        public LayerMask ObstacleLayers;
        [Min(0.01f)] public float ClearanceRadius = 0.7f;
        private readonly HashSet<Enemy> _alive = new HashSet<Enemy>();
        private readonly Dictionary<EnemySpawnPoint, int> _spawnCounts = new Dictionary<EnemySpawnPoint, int>();
        private readonly HashSet<MapRegion> _regions = new HashSet<MapRegion>();
        private int _nextPoint;
        public int AliveCount => _alive.Count;
        public string LastFailure { get; private set; }
        public event Action<Enemy> Spawned;
        public event Action<int> AliveCountChanged;

        /// <summary>Reset successful-spawn quotas at a wave boundary, preserving the tie-break cursor.</summary>
        public void BeginWave() => _spawnCounts.Clear();
        public int GetSpawnCount(EnemySpawnPoint point) => point != null && _spawnCounts.TryGetValue(point, out int count) ? count : 0;

        private void RefreshRegions()
        {
            _regions.Clear();
            if (Regions != null)
                foreach (var region in Regions) if (region != null) _regions.Add(region);
            foreach (var region in FindObjectsByType<MapRegion>(FindObjectsSortMode.None)) _regions.Add(region);
        }

        public bool IsPointAvailable(EnemySpawnPoint point)
        {
            RefreshRegions();
            return IsPointUnlocked(point);
        }

        private bool IsPointUnlocked(EnemySpawnPoint point)
        {
            if (point == null || !point.Available || !point.HasNavigation()) return false;
            foreach (var region in _regions)
                if (region.gameObject.activeInHierarchy && !region.IsUnlocked && region.Contains(point.transform.position)) return false;
            return true;
        }

        public bool TrySpawn(Enemy prefab, float healthMultiplier, float damageMultiplier, out Enemy enemy)
        {
            enemy = null;
            if (prefab == null || prefab.Definition == null)
            {
                LastFailure = "Missing enemy prefab or definition.";
                return false;
            }
            RefreshRegions();
            int bestIndex = -1;
            int leastSpawned = int.MaxValue;
            int length = Points != null ? Points.Length : 0;
            for (int offset = 0; offset < length; offset++)
            {
                int index = (_nextPoint + offset) % length;
                var point = Points[index];
                if (!IsPointUnlocked(point) || !IsClear(point.transform.position)) continue;
                int count = GetSpawnCount(point);
                if (count >= leastSpawned) continue;
                bestIndex = index;
                leastSpawned = count;
            }
            if (bestIndex >= 0)
            {
                var point = Points[bestIndex];
                enemy = Instantiate(prefab, point.transform.position, point.transform.rotation, transform);
                enemy.gameObject.SetActive(true);
                enemy.Configure(prefab.Definition, healthMultiplier, damageMultiplier);
                enemy.Removed += OnRemoved;
                _alive.Add(enemy);
                _spawnCounts[point] = leastSpawned + 1;
                _nextPoint = (bestIndex + 1) % length;
                LastFailure = null;
                AliveCountChanged?.Invoke(_alive.Count);
                Spawned?.Invoke(enemy);
                return true;
            }
            LastFailure = "No unlocked, enabled and unblocked preset SpawnPoint. Wave is waiting.";
            return false;
        }

        private bool IsClear(Vector3 position)
        {
            if (Physics.CheckSphere(position, ClearanceRadius, ObstacleLayers, QueryTriggerInteraction.Ignore)) return false;
            foreach (var enemy in _alive)
                if (enemy != null && Vector3.Distance(position, enemy.transform.position) < ClearanceRadius * 2f) return false;
            return true;
        }

        private void OnRemoved(Enemy enemy)
        {
            enemy.Removed -= OnRemoved;
            if (_alive.Remove(enemy)) AliveCountChanged?.Invoke(_alive.Count);
            // Spawned instances belong to this spawner; do not accumulate inactive corpses.
            if (enemy != null) Destroy(enemy.gameObject);
        }

        public void DespawnAll()
        {
            foreach (var enemy in new List<Enemy>(_alive))
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            _alive.Clear();
        }

        private void OnDestroy()
        {
            foreach (var enemy in _alive) if (enemy != null) enemy.Removed -= OnRemoved;
            _alive.Clear();
        }
    }
}
