using UnityEngine;

namespace Scrapfire.Characters.Core
{
    /// <summary>Per-character attributes. Health remains the single owner of current HP/state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class CharacterAttributes : MonoBehaviour
    {
        [Min(0.01f)] public float MaximumHealth = 100f;
        [Min(0f)] public float MovementSpeed = 6f;
        [Min(1f)] public float SprintMultiplier = 1.6f;
        [Min(0f)] public float RegenerationDelay = 5f;
        [Min(0f)] public float RegenerationPerSecond = 5f;
        private Health _health;
        private float _lastDamageTime;
        public float CurrentHealth => _health.CurrentHealthValue;
        public HealthState State => _health.CurrentHealthState;

        private void Awake()
        {
            _health = GetComponent<Health>();
            ApplyMaximumHealth();
            _health.InitialHealthValue = _health.MaximumHealthValue;
            _health.ResetHealth();
        }

        private void OnEnable()
        {
            if (_health == null) _health = GetComponent<Health>();
            _lastDamageTime = Time.time;
            _health.Damaged += OnDamaged;
            _health.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.StateChanged -= OnStateChanged;
        }

        public void ApplyMaximumHealth()
        {
            if (_health == null) _health = GetComponent<Health>();
            _health.MaximumHealthValue = Mathf.Max(0.01f, MaximumHealth);
            _health.ClampToMaximum();
        }

        private void OnDamaged(DamageSource source) => _lastDamageTime = Time.time;
        private void OnStateChanged(HealthState previous, HealthState current) => _lastDamageTime = Time.time;

        private void Update()
        {
            if (_health.CurrentHealthState == HealthState.Alive
                && Time.time - _lastDamageTime >= RegenerationDelay)
                _health.Heal(RegenerationPerSecond * Time.deltaTime);
        }
    }
}
