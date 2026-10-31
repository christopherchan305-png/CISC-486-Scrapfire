using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Characters.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterPlayer))]
    public sealed class PlayerAim : MonoBehaviour
    {
        [Range(0f, 1f)] public float MoveSpeedMultiplier = 0.4f;
        public bool IsAiming { get; private set; }
        private CharacterPlayer _player;

        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
            _player.CharacterHealth.StateChanged += OnHealthChanged;
        }

        public void SetAiming(bool aiming)
        {
            IsAiming = aiming && isActiveAndEnabled && _player.CanAct;
        }

        private void OnHealthChanged(HealthState previous, HealthState current)
        {
            if (current != HealthState.Alive)
            {
                IsAiming = false;
            }
        }

        private void OnDisable()
        {
            IsAiming = false;
        }

        private void OnDestroy()
        {
            if (_player != null && _player.CharacterHealth != null)
            {
                _player.CharacterHealth.StateChanged -= OnHealthChanged;
            }
        }
    }
}
