using UnityEngine;
using Scrapfire.Pooling;

namespace Scrapfire.Characters.Weapon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedWeapon))]
    public class ShotTracer : MonoBehaviour
    {
        public GameObject TracePrefab;
        [Min(0)] public int PrewarmCount = 32;
        private RangedWeapon _weapon;
        private SharedObjectPool _pool;

        private void Awake()
        {
            _weapon = GetComponent<RangedWeapon>();
        }

        private void OnEnable()
        {
            _pool = SharedObjectPool.ForScene(gameObject.scene);

            if (TracePrefab != null)
            {
                _pool.Prewarm(TracePrefab, PrewarmCount);
            }

            _weapon.ShotResolved += Show;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ShotResolved -= Show;
            }
        }

        private void Show(Vector3 start, Vector3 end, bool hit)
        {
            if (TracePrefab == null)
            {
                return;
            }

            PooledObject instance = _pool.Rent(TracePrefab, start, Quaternion.identity);
            instance.GetComponent<BulletTrace>().Play(start, end);
        }
    }
}
