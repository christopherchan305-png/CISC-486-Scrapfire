using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Scrapfire.Characters.Core
{
    public enum HealthState {
        Alive,
        Downed,
        Dead
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Scrapfire/Character/Core/Health")]
    public class Health : MonoBehaviour
    {
        [Min(0.01f)] public float InitialHealthValue = 100f;
        [Min(0.01f)] public float MaximumHealthValue = 100f;
        [FormerlySerializedAs("CurrentHealthValue")]
        [SerializeField] private float _currentHealthValue;
        public bool ResetOnEnable = true;
        public bool ImmuneToDamage;
        // Reserved for a later motor/knockback implementation; damage does not apply forces.
        public bool ImmuneToKnockback;

        private bool _initialized;
        [Tooltip("Enter Downed instead of Dead when health reaches zero. Configure on the prefab.")]
        public bool CanBeDowned;
        private HealthState _state = HealthState.Alive;
        public float CurrentHealthValue { get { EnsureInitialized(); return _currentHealthValue; } }
        public HealthState CurrentHealthState { get { EnsureInitialized(); return _state; } }
        public event Action<float, float> HealthChanged;
        public event Action<HealthState, HealthState> StateChanged;
        public event Action<DamageSource> Damaged;

        private void Awake() => EnsureInitialized();

        private void OnEnable()
        {
            if (!_initialized || ResetOnEnable)
                ResetHealth();
        }

        private void OnValidate()
        {
            MaximumHealthValue = Mathf.Max(0.01f, MaximumHealthValue);
            InitialHealthValue = Mathf.Clamp(InitialHealthValue, 0.01f, MaximumHealthValue);
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _currentHealthValue = Mathf.Clamp(InitialHealthValue, 0.01f, Mathf.Max(0.01f, MaximumHealthValue));
            _initialized = true;
        }

        private void SetState(HealthState next)
        {
            if (_state == next) return;
            HealthState previous = _state;
            _state = next;
            StateChanged?.Invoke(previous, next);
        }

        public float Damage(DamageSource source)
        {
            EnsureInitialized();
            if (!isActiveAndEnabled || ImmuneToDamage || CurrentHealthState != HealthState.Alive
                || !IsPositiveFinite(source.Damage) || source.HealthOnVictim != this
                || source.Victim != gameObject) return 0f;

            float applied = Mathf.Min(source.Damage, _currentHealthValue);
            _currentHealthValue -= applied;
            if (_currentHealthValue == 0f)
                SetState(CanBeDowned ? HealthState.Downed : HealthState.Dead);
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
            Damaged?.Invoke(source);
            return applied;
        }

        public float Heal(float amount)
        {
            EnsureInitialized();
            if (!isActiveAndEnabled || CurrentHealthState != HealthState.Alive || !IsPositiveFinite(amount)) return 0f;
            float restored = Mathf.Max(0f, Mathf.Min(amount, MaximumHealthValue - _currentHealthValue));
            if (restored <= 0f) return 0f;
            _currentHealthValue += restored;
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
            return restored;
        }

        public bool Revive(float restoredHealth)
        {
            EnsureInitialized();
            if (!isActiveAndEnabled || CurrentHealthState != HealthState.Downed || !IsPositiveFinite(restoredHealth)) return false;
            _currentHealthValue = Mathf.Min(restoredHealth, Mathf.Max(0.01f, MaximumHealthValue));
            SetState(HealthState.Alive);
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
            return true;
        }

        public bool Kill()
        {
            EnsureInitialized();
            if (CurrentHealthState == HealthState.Dead) return false;
            _currentHealthValue = 0f;
            SetState(HealthState.Dead);
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
            return true;
        }

        public void ClampToMaximum()
        {
            EnsureInitialized();
            _currentHealthValue = Mathf.Min(_currentHealthValue, Mathf.Max(0.01f, MaximumHealthValue));
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
        }

        // Explicit respawn/pool reset is the only way to leave Dead.
        public void ResetHealth()
        {
            EnsureInitialized();
            _currentHealthValue = Mathf.Clamp(InitialHealthValue, 0.01f, Mathf.Max(0.01f, MaximumHealthValue));
            SetState(HealthState.Alive);
            HealthChanged?.Invoke(_currentHealthValue, MaximumHealthValue);
        }

        private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value) && !float.IsNaN(value);
    }
}
