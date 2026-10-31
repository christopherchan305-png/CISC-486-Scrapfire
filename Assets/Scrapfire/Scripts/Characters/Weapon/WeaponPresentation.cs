using UnityEngine;
using Scrapfire.Pooling;

namespace Scrapfire.Characters.Weapon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedWeapon))]
    public sealed class WeaponPresentation : MonoBehaviour
    {
        public ParticleSystem[] MuzzleParticles;
        public GameObject ImpactPrefab;
        [Min(0.1f)] public float ImpactLifetime = 2f;

        private RangedWeapon _weapon;
        private SharedObjectPool _pool;

        private void Awake()
        {
            _weapon = GetComponent<RangedWeapon>();

            foreach (ParticleSystem particles in MuzzleParticles)
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void OnEnable()
        {
            _pool = SharedObjectPool.ForScene(gameObject.scene);

            if (ImpactPrefab != null)
            {
                _pool.Prewarm(ImpactPrefab, 24);
            }

            _weapon.Attacked += ShowMuzzle;
            _weapon.BulletHit += ShowImpact;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.Attacked -= ShowMuzzle;
                _weapon.BulletHit -= ShowImpact;
            }

            foreach (ParticleSystem particles in MuzzleParticles)
            {
                if (particles != null)
                {
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void ShowMuzzle(Vector3 origin, Vector3 direction)
        {
            foreach (ParticleSystem particles in MuzzleParticles)
            {
                if (particles != null)
                {
                    particles.Play(false);
                }
            }
        }

        private void ShowImpact(RaycastHit hit)
        {
            if (ImpactPrefab == null)
            {
                return;
            }

            PooledObject effect = _pool.Rent(ImpactPrefab, hit.point,
                Quaternion.LookRotation(hit.normal));
            effect.GetComponent<PooledEffect>().Play(ImpactLifetime);
        }
    }
}
