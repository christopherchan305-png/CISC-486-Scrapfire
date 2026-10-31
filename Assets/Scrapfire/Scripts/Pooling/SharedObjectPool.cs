using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Scrapfire.Pooling
{
    // One scene-owned service, with a separate bucket for each prefab.
    [DisallowMultipleComponent]
    public sealed class SharedObjectPool : MonoBehaviour
    {
        private sealed class Bucket
        {
            public readonly Stack<PooledObject> Available = new Stack<PooledObject>();
            public int Created;
            public int Active;
        }

        private static readonly Dictionary<Scene, SharedObjectPool> Scenes = new Dictionary<Scene, SharedObjectPool>();
        private readonly Dictionary<GameObject, Bucket> _buckets = new Dictionary<GameObject, Bucket>();
        private Transform _inactiveRoot;

        private void Awake()
        {
            Scenes[gameObject.scene] = this;
            var storage = new GameObject("InactiveFactory");
            storage.transform.SetParent(transform, false);
            storage.SetActive(false);
            _inactiveRoot = storage.transform;
        }

        public static SharedObjectPool ForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("A loaded scene is required.", nameof(scene));
            }

            if (Scenes.TryGetValue(scene, out SharedObjectPool existing) && existing != null)
            {
                return existing;
            }

            var root = new GameObject("SharedObjectPool");
            SceneManager.MoveGameObjectToScene(root, scene);
            return root.AddComponent<SharedObjectPool>();
        }

        private Bucket GetBucket(GameObject prefab)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (!_buckets.TryGetValue(prefab, out Bucket bucket))
            {
                bucket = new Bucket();
                _buckets.Add(prefab, bucket);
            }

            return bucket;
        }

        private PooledObject Create(GameObject prefab, Bucket bucket)
        {
            // Instantiating beneath an inactive parent defers OnEnable until the lease is ready.
            GameObject instance = Instantiate(prefab, _inactiveRoot, false);
            instance.name = prefab.name + " (Pooled)";
            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            PooledObject pooled = instance.GetComponent<PooledObject>();

            if (pooled == null)
            {
                pooled = instance.AddComponent<PooledObject>();
            }

            pooled.Bind(this, prefab);
            bucket.Created++;
            return pooled;
        }

        public void Prewarm(GameObject prefab, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Bucket bucket = GetBucket(prefab);

            while (bucket.Created < count)
            {
                bucket.Available.Push(Create(prefab, bucket));
            }
        }

        public PooledObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            Bucket bucket = GetBucket(prefab);
            PooledObject instance = null;

            while (bucket.Available.Count > 0 && instance == null)
            {
                instance = bucket.Available.Pop();

                if (instance == null)
                {
                    bucket.Created--;
                }
            }

            if (instance == null)
            {
                instance = Create(prefab, bucket);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = prefab.transform.localScale;
            instance.InUse = true;
            bucket.Active++;
            instance.gameObject.SetActive(true);
            return instance;
        }

        internal void Return(PooledObject instance)
        {
            if (instance == null || !instance.InUse)
            {
                return;
            }

            instance.InUse = false;
            instance.gameObject.SetActive(false);
            Bucket bucket = _buckets[instance.Prefab];
            bucket.Active--;
            bucket.Available.Push(instance);
        }

        public int GetCreatedCount(GameObject prefab)
        {
            return GetBucket(prefab).Created;
        }

        public int GetActiveCount(GameObject prefab)
        {
            return GetBucket(prefab).Active;
        }

        private void OnDestroy()
        {
            Scene scene = gameObject.scene;

            if (Scenes.TryGetValue(scene, out SharedObjectPool pool) && pool == this)
            {
                Scenes.Remove(scene);
            }
        }
    }
}
