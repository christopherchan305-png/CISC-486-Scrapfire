using UnityEngine;

namespace Scrapfire.Enemies.AI
{
    /// <summary>Presentation only: the agent moves the actor, imported clips animate its child model.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyBrain))]
    public sealed class EnemyAnimationDriver : MonoBehaviour
    {
        public Animator Animator;
        private Enemy _enemy;
        private EnemyNavigation _navigation;
        private static readonly int Speed = UnityEngine.Animator.StringToHash("Speed");
        private static readonly int Attack = UnityEngine.Animator.StringToHash("Attack");
        private void Awake() { _enemy = GetComponent<Enemy>(); _navigation = GetComponent<EnemyNavigation>(); }
        private void OnEnable() => _enemy.Attacked += OnAttack;
        private void OnDisable() => _enemy.Attacked -= OnAttack;
        private void OnAttack() { if (Animator != null) Animator.SetTrigger(Attack); }
        private void Update()
        {
            if (Animator != null) Animator.SetFloat(Speed, _navigation.Velocity.magnitude, 0.12f, Time.deltaTime);
        }
    }
}
