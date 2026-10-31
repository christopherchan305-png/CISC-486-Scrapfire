using System;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Enemies
{
    /// <summary>Enemy stats and combat entry points only. No targeting/navigation/behaviour AI.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class Enemy : MonoBehaviour
    {
        public EnemyDefinition Definition;
        public float MovementSpeed { get; private set; }
        public float AttackDamage { get; private set; }
        public float DestructionSpeed { get; private set; }
        public bool CanDestroyObstacles => DestructionSpeed > 0f;
        public Health Health { get; private set; }
        public bool IsAlive => isActiveAndEnabled && Health.CurrentHealthState == HealthState.Alive;
        public event Action<Enemy> Removed;
        public event Action Attacked;
        private float _nextAttackTime;
        private bool _reportedRemoved;

        private void Awake()
        {
            CharacterCollisionLayers.Assign(gameObject, "Enemies");
            Health = GetComponent<Health>();
            Health.StateChanged += OnStateChanged;
        }

        private void OnEnable()
        {
            _reportedRemoved = false;
            _nextAttackTime = 0f;
            if (Definition != null) Configure(Definition, 1f, 1f);
        }

        public void Configure(EnemyDefinition definition, float healthMultiplier, float damageMultiplier)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            if (!PositiveFinite(healthMultiplier) || !PositiveFinite(damageMultiplier))
                throw new ArgumentOutOfRangeException(nameof(healthMultiplier));
            Definition = definition;
            if (Health == null) Health = GetComponent<Health>();
            Health.MaximumHealthValue = Mathf.Max(0.01f, definition.Health * healthMultiplier);
            Health.InitialHealthValue = Health.MaximumHealthValue;
            Health.CanBeDowned = false;
            Health.ResetHealth();
            MovementSpeed = Mathf.Max(0f, definition.MovementSpeed);
            AttackDamage = Mathf.Max(0f, definition.AttackDamage * damageMultiplier);
            DestructionSpeed = Mathf.Max(0f, definition.DestructionSpeed);
            _reportedRemoved = false;
            _nextAttackTime = 0f;
        }

        // A later behaviour component decides when and whom to attack.
        public bool TryAttack(Health target)
        {
            if (!IsAlive || Definition == null || target == null || target == Health
                || target.CurrentHealthState != HealthState.Alive || Time.time < _nextAttackTime
                || Vector3.Distance(transform.position, target.transform.position) > Definition.AttackRange) return false;
            if (target.Damage(new DamageSource(AttackDamage, DamageType.Melee, gameObject, target)) <= 0f) return false;
            _nextAttackTime = Time.time + Mathf.Max(0.01f, Definition.AttackInterval);
            Attacked?.Invoke();
            return true;
        }

        private static bool PositiveFinite(float value) => value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        private void OnStateChanged(HealthState previous, HealthState current)
        {
            if (current == HealthState.Dead) ReportRemoved();
        }
        private void OnDisable() => ReportRemoved();
        private void ReportRemoved()
        {
            if (_reportedRemoved) return;
            _reportedRemoved = true;
            Removed?.Invoke(this);
        }
        private void OnDestroy()
        {
            ReportRemoved();
            if (Health != null) Health.StateChanged -= OnStateChanged;
        }
    }
}
