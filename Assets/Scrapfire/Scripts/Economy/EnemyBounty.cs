using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Economy
{
    /// <summary>Optional combat/economy adapter. Core Health and Enemy do not know about money.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class EnemyBounty : MonoBehaviour
    {
        [Min(0)] public int Reward = 100;
        private Health _health;
        private bool _paid;
        private void Awake()
        {
            _health = GetComponent<Health>();
            _health.Damaged += OnDamaged;
        }
        private void OnEnable() => _paid = false;
        // Keep listening until Destroy: a CharacterAi may disable itself during lethal Damage.
        private void OnDestroy() { if (_health != null) _health.Damaged -= OnDamaged; }
        private void OnDamaged(DamageSource source)
        {
            if (_paid || _health.CurrentHealthState != HealthState.Dead) return;
            _paid = true;
            var player = source.CausedBy != null ? source.CausedBy.GetComponentInParent<CharacterPlayer>() : null;
            if (player != null) player.Balance.Credit(Reward);
        }
    }
}
