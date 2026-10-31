using UnityEngine;

namespace Scrapfire.Enemies
{
    [CreateAssetMenu(menuName = "Scrapfire/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Min(0.01f)] public float Health = 100f;
        [Min(0f)] public float MovementSpeed = 3f;
        [Min(0f)] public float AttackDamage = 10f;
        [Min(0.01f)] public float AttackInterval = 1f;
        [Min(0f)] public float AttackRange = 1.8f;
        [Tooltip("Obstacle durability removed per second. Zero means unable to break obstacles.")]
        [Min(0f)] public float DestructionSpeed;
    }
}
