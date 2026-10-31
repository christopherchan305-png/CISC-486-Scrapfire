using System.Collections.Generic;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Weapon
{
    [AddComponentMenu("Scrapfire/Character/Weapon/Melee Weapon")]
    public class MeleeWeapon : Weapon
    {
        [Min(0f)] public float Damage = 20f;
        [Min(0.01f)] public float AttackInterval = 0.25f;
        [Min(0.01f)] public float Range = 10f;
        protected override float UseInterval => AttackInterval;

        [Range(1f, 360f)] public float AttackAngle = 100f;
        // Set to the environment layer(s) to prevent melee attacks through walls.
        public LayerMask ObstructionLayers;
        private readonly HashSet<Health> _hitTargets = new HashSet<Health>();

        protected override void PerformAttack(Vector3 origin, Vector3 direction)
        {
            _hitTargets.Clear();
            Collider[] hits = Physics.OverlapSphere(origin, Mathf.Max(0.01f, Range), HitLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (BelongsToOwner(hit)) continue;
                Health health = hit.GetComponentInParent<Health>();
                if (health == null || health == Owner.CharacterHealth || _hitTargets.Contains(health)) continue;
                Vector3 offset = hit.ClosestPoint(origin) - origin;
                if (offset.sqrMagnitude > 0.0001f)
                {
                    if (Vector3.Angle(direction, offset) > AttackAngle * 0.5f) continue;
                    if (Physics.Raycast(origin, offset.normalized, offset.magnitude, ObstructionLayers, QueryTriggerInteraction.Ignore)) continue;
                }
                // An actor with several colliders still receives damage only once per swing.
                _hitTargets.Add(health);
                ApplyHit(health, DamageType.Melee, Damage);
            }
        }
    }
}
