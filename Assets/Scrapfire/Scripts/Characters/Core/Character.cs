using UnityEngine;
using CharacterWeapon = Scrapfire.Characters.Weapon.Weapon;

namespace Scrapfire.Characters.Core
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [AddComponentMenu("Scrapfire/Character/Core/Character")]
    public class Character : MonoBehaviour
    {
        public GameObject CharacterModel;
        public Health CharacterHealth;
        [SerializeField] private CharacterWeapon _weapon;

        public CharacterWeapon CurrentWeapon => _weapon;
        public HealthState HealthState => CharacterHealth.CurrentHealthState;
        public bool CanAct => isActiveAndEnabled && CharacterHealth != null
            && CharacterHealth.CurrentHealthState == HealthState.Alive;

        protected virtual void Awake()
        {
            // Health belongs to this character, never to an accidentally bound other actor.
            if (CharacterHealth == null || CharacterHealth.gameObject != gameObject)
                CharacterHealth = GetComponent<Health>();
            CharacterHealth.StateChanged += OnHealthStateChanged;
            if (_weapon == null)
                _weapon = GetComponentInChildren<CharacterWeapon>();
            EquipWeapon(_weapon);
        }

        protected virtual void OnHealthStateChanged(HealthState previous, HealthState current)
        {
            if (current != HealthState.Alive && _weapon != null)
                _weapon.CancelAction();
        }

        protected virtual void OnDisable()
        {
            if (_weapon != null) _weapon.CancelAction();
        }

        protected virtual void OnDestroy()
        {
            if (CharacterHealth != null) CharacterHealth.StateChanged -= OnHealthStateChanged;
            if (_weapon != null) _weapon.Unbind(this);
        }

        public bool EquipWeapon(CharacterWeapon weapon)
        {
            if (weapon != null && weapon.Owner != null && weapon.Owner != this)
                return false;
            if (_weapon != null && _weapon != weapon)
                _weapon.Unbind(this);
            _weapon = weapon;
            if (_weapon != null)
                _weapon.Bind(this);
            return true;
        }

        public virtual bool TryAttack(Vector3 direction)
        {
            return CanAct && _weapon != null && _weapon.TryAttack(direction);
        }
    }
}
