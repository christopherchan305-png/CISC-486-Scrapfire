using UnityEngine;

namespace Scrapfire.Pooling
{
    [RequireComponent(typeof(PooledObject))]
    public sealed class PooledEffect : MonoBehaviour
    {
        private PooledObject _lease;
        private ParticleSystem[] _particles;
        private float _remaining;

        private void Awake()
        {
            _lease = GetComponent<PooledObject>();
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void Play(float lifetime)
        {
            _remaining = Mathf.Max(0.01f, lifetime);

            foreach (ParticleSystem particles in _particles)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles.Play(false);
            }
        }

        private void Update()
        {
            _remaining -= Time.deltaTime;

            if (_remaining <= 0f)
            {
                _lease.Release();
            }
        }

        private void OnDisable()
        {
            _remaining = 0f;

            if (_particles == null)
            {
                return;
            }

            foreach (ParticleSystem particles in _particles)
            {
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
