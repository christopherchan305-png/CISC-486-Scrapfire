using UnityEngine;
using UnityEngine.InputSystem;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Weapon;

namespace Scrapfire.Characters.Player
{
    /// <summary>Local prototype input adapter, kept separate from Character and combat rules.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterPlayer), typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInventoryControls), typeof(PlayerAim))]
    [AddComponentMenu("Scrapfire/Player/Player Input Driver")]
    public class PlayerInputDriver : MonoBehaviour
    {
        public float MoveSpeed
        {
            get => GetComponent<CharacterAttributes>().MovementSpeed;
            set => GetComponent<CharacterAttributes>().MovementSpeed = Mathf.Max(0f, value);
        }
        public float SprintMultiplier
        {
            get => GetComponent<CharacterAttributes>().SprintMultiplier;
            set => GetComponent<CharacterAttributes>().SprintMultiplier = Mathf.Max(1f, value);
        }
        private CharacterPlayer _player;
        private CharacterController _motor;
        private PlayerInventoryControls _inventoryControls;
        private PlayerAim _aim;
        private float _verticalSpeed;
        public bool IsMoving { get; private set; }
        public bool IsSprinting { get; private set; }


        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
            _motor = GetComponent<CharacterController>();
            _inventoryControls = GetComponent<PlayerInventoryControls>();
            _aim = GetComponent<PlayerAim>();
        }

        private void Update()
        {
            if (!_player.CanAct || (_inventoryControls != null && _inventoryControls.BlocksGameplay))
            {
                IsMoving = false;
                IsSprinting = false;
                _aim.SetAiming(false);
                _player.CurrentWeapon?.CancelAction();
                return;
            }
            Mouse mouse = Mouse.current;
            _aim.SetAiming(mouse != null && mouse.rightButton.isPressed);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 input = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
                Move(input, keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
                if (keyboard.rKey.wasPressedThisFrame && _player.CurrentWeapon is RangedWeapon ranged)
                    ranged.TryReload();
            }

            Camera camera = _player.PlayerCamera;
            if (mouse == null || camera == null)
            {
                if (_player.CurrentWeapon is RangedWeapon unavailable)
                {
                    unavailable.ReleaseTrigger();
                }

                return;
            }

            if (_player.CurrentWeapon is RangedWeapon current)
            {
                current.SetUseContext(new WeaponUseContext(IsMoving, IsSprinting, _aim.IsAiming));

                if (!mouse.leftButton.isPressed)
                {
                    current.ReleaseTrigger();
                }
            }
            Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());
            // Aim on the horizontal plane through the muzzle, avoiding downward shots into the floor.
            Transform muzzle = _player.CurrentWeapon != null ? _player.CurrentWeapon.AttackOrigin : null;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position;
            var plane = new Plane(Vector3.up, origin);
            if (!plane.Raycast(ray, out float distance)) return;
            Vector3 direction = ray.GetPoint(distance) - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(direction);
            if (_player.CurrentWeapon is RangedWeapon firearm)
            {
                firearm.UseTrigger(mouse.leftButton.isPressed, mouse.leftButton.wasPressedThisFrame, direction);
            }
            else if (mouse.leftButton.wasPressedThisFrame)
            {
                _player.TryAttack(direction);
            }
        }

        public float GetMoveSpeed(bool sprint)
        {
            float equipmentMultiplier = _player != null ? _player.MoveSpeedMultiplier : 1f;
            float movementMultiplier = _aim != null && _aim.IsAiming
                ? _aim.MoveSpeedMultiplier
                : sprint ? Mathf.Max(1f, SprintMultiplier) : 1f;
            return MoveSpeed * equipmentMultiplier * movementMultiplier;
        }

        public void Move(Vector2 input, bool sprint = false)
        {
            if (_player == null || !_player.CanAct || !_motor.enabled
                || (_inventoryControls != null && _inventoryControls.BlocksGameplay)) return;
            input = Vector2.ClampMagnitude(input, 1f);
            _verticalSpeed = _motor.isGrounded ? -2f : _verticalSpeed + Physics.gravity.y * Time.deltaTime;
            float speed = GetMoveSpeed(sprint);
            Vector3 velocity = new Vector3(input.x * speed, _verticalSpeed, input.y * speed);
            Vector3 previousPosition = transform.position;
            _motor.Move(velocity * Time.deltaTime);
            Vector3 displacement = transform.position - previousPosition;
            displacement.y = 0f;
            IsMoving = displacement.sqrMagnitude > 0.000001f;
            IsSprinting = IsMoving && sprint && !_aim.IsAiming;

            if (_player.CurrentWeapon is RangedWeapon firearm)
            {
                firearm.SetUseContext(new WeaponUseContext(IsMoving, IsSprinting, _aim.IsAiming));
            }
        }
    }
}
