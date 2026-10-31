using System;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Weapon
{
    [DisallowMultipleComponent]
    public abstract class Weapon : MonoBehaviour
    {
        public Transform AttackOrigin;
        public LayerMask HitLayers = ~0;

        private WeaponState _state = WeaponState.Ready;
        private float _remainingTime;
        private int _actionVersion;

        public Character Owner { get; private set; }
        public WeaponState CurrentState => _state;
        public event Action<WeaponState, WeaponState> StateChanged;
        public event Action<Vector3, Vector3> Attacked;

        protected abstract float UseInterval { get; }
        protected virtual WeaponState AfterShotState => WeaponState.Firing;
        protected virtual void Awake() { }

        private void SetState(WeaponState next)
        {
            if (_state == next)
            {
                return;
            }

            WeaponState previous = _state;
            _state = next;
            StateChanged?.Invoke(previous, next);
        }

        internal void Bind(Character owner)
        {
            Owner = owner;
        }

        internal void Unbind(Character owner)
        {
            if (Owner != owner)
            {
                return;
            }

            CancelAction();
            Owner = null;
        }

        public virtual bool TryAttack(Vector3 direction)
        {
            return CanStartAttack(direction) && ExecuteAttack(direction);
        }

        protected bool CanStartAttack(Vector3 direction)
        {
            return CanUse() && CurrentState == WeaponState.Ready && CanAttack()
                && IsFinite(direction) && direction.sqrMagnitude >= 0.0001f;
        }

        protected bool ExecuteAttack(Vector3 direction)
        {
            if (!CanUse() || !CanAttack() || !IsFinite(direction)
                || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            Vector3 origin = AttackOrigin != null ? AttackOrigin.position : transform.position;
            StartAction(AfterShotState, Mathf.Max(0.01f, UseInterval));
            PerformAttack(origin, direction);
            Attacked?.Invoke(origin, direction);
            return true;
        }

        protected bool CanUse()
        {
            return isActiveAndEnabled && Owner != null && Owner.CanAct;
        }

        protected virtual bool CanAttack() => true;
        protected abstract void PerformAttack(Vector3 origin, Vector3 direction);
        protected virtual void OnActionFinished(WeaponState previous) { }

        protected void StartAction(WeaponState state, float duration)
        {
            _actionVersion++;
            _remainingTime = duration;
            SetState(state);
        }

        protected virtual void Update()
        {
            if (CurrentState == WeaponState.Ready)
            {
                return;
            }

            if (!CanUse())
            {
                CancelAction();
                return;
            }

            _remainingTime -= Time.deltaTime;

            if (_remainingTime > 0f)
            {
                return;
            }

            int version = _actionVersion;
            OnActionFinished(CurrentState);

            // A completed warmup may start firing; a loaded shell may start another reload step.
            if (_actionVersion == version)
            {
                SetState(WeaponState.Ready);
            }
        }

        protected virtual void OnDisable()
        {
            CancelAction();
        }

        public virtual void CancelAction()
        {
            _actionVersion++;
            _remainingTime = 0f;
            SetState(WeaponState.Ready);
        }

        protected bool BelongsToOwner(Collider collider)
        {
            return Owner != null && (collider.transform == Owner.transform
                || collider.transform.IsChildOf(Owner.transform));
        }

        protected void ApplyHit(Health health, DamageType type, float damage)
        {
            if (health == null || health == Owner.CharacterHealth)
            {
                return;
            }

            float multiplier = Owner is CharacterPlayer player ? player.DamageMultiplier : 1f;
            health.Damage(new DamageSource(Mathf.Max(0f, damage) * multiplier,
                type, Owner.gameObject, health));
        }

        protected static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
                && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }
    }
}
