using UnityEngine;
using UnityEngine.InputSystem;
using Scrapfire.Characters.Core;
using Scrapfire.Characters.Player;

namespace Scrapfire.Interaction
{
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterPlayer))]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        [Min(0.1f)] public float Reach = 2f;
        public LayerMask Layers = ~0;
        public bool ReadLocalInput = true;
        public IPlayerInteractable Target { get; private set; }
        public string Prompt => Target?.GetPrompt(_player) ?? string.Empty;
        private CharacterPlayer _player;
        private PlayerInventoryControls _panels;
        private void Awake()
        {
            _player = GetComponent<CharacterPlayer>();
            _panels = GetComponent<PlayerInventoryControls>();
        }

        private bool CanAct => _player.CanAct && (_panels == null || !_panels.BlocksGameplay);

        private void Update()
        {
            RefreshTarget();
            if (ReadLocalInput && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
                TryInteract();
        }

        public void RefreshTarget()
        {
            Target = null;
            if (!CanAct) return;
            float nearest = float.PositiveInfinity;
            foreach (var collider in Physics.OverlapSphere(transform.position, Reach, Layers, QueryTriggerInteraction.Collide))
            {
                var target = collider.GetComponentInParent<IPlayerInteractable>();
                if (target == null || !target.CanInteract(_player)) continue;
                float distance = (collider.ClosestPoint(transform.position) - transform.position).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                Target = target;
            }
        }

        public bool TryInteract()
        {
            RefreshTarget();
            return CanAct && Target != null && Target.TryInteract(_player);
        }
    }
}
