using UnityEngine;
using Scrapfire.Pooling;

namespace Scrapfire.Characters.Weapon
{
    // Visual flight only. RangedWeapon still resolves damage immediately with hitscan.
    [RequireComponent(typeof(PooledObject), typeof(LineRenderer))]
    public sealed class BulletTrace : MonoBehaviour
    {
        [Min(1f)] public float VisualSpeed = 100f;
        [Min(0.01f)] public float MinimumTravelTime = 0.12f;
        [Min(0.01f)] public float MaximumTravelTime = 0.35f;
        [Min(0.01f)] public float TailLength = 2.5f;
        [Min(0.01f)] public float FadeTime = 0.08f;

        private PooledObject _lease;
        private LineRenderer _line;
        private Vector3 _start;
        private Vector3 _direction;
        private float _distance;
        private float _travelTime;
        private float _elapsed;
        private float _width;

        private void Awake()
        {
            _lease = GetComponent<PooledObject>();
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _width = _line.widthMultiplier;
        }

        public void Play(Vector3 start, Vector3 end)
        {
            _start = start;
            Vector3 offset = end - start;
            _distance = offset.magnitude;
            _direction = _distance > 0f ? offset / _distance : Vector3.zero;
            float minimum = Mathf.Max(0.01f, MinimumTravelTime);
            float maximum = Mathf.Max(minimum, MaximumTravelTime);
            _travelTime = Mathf.Clamp(_distance / Mathf.Max(1f, VisualSpeed), minimum, maximum);
            _elapsed = 0f;
            _line.widthMultiplier = _width;
            _line.enabled = true;
            Render(Mathf.Min(_distance, 0.1f));
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float headDistance = _distance * Mathf.Clamp01(_elapsed / _travelTime);
            Render(headDistance);

            if (_elapsed > _travelTime)
            {
                float fade = 1f - Mathf.Clamp01((_elapsed - _travelTime) / Mathf.Max(0.01f, FadeTime));
                _line.widthMultiplier = _width * fade;
            }

            if (_elapsed >= _travelTime + Mathf.Max(0.01f, FadeTime))
            {
                _lease.Release();
            }
        }

        private void Render(float headDistance)
        {
            float tailDistance = Mathf.Max(0f, headDistance - TailLength);
            _line.SetPosition(0, _start + _direction * tailDistance);
            _line.SetPosition(1, _start + _direction * headDistance);
        }

        private void OnDisable()
        {
            if (_line != null)
            {
                _line.enabled = false;
                _line.widthMultiplier = _width;
            }

            _elapsed = 0f;
            _start = Vector3.zero;
            _direction = Vector3.zero;
            _distance = 0f;
        }
    }
}
