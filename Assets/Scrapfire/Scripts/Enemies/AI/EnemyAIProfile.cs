using UnityEngine;

namespace Scrapfire.Enemies.AI
{
    public enum EnemyDefaultBehaviour { Idle, Wander }
    public enum EnemyChaseMode { Seek, Pursue }
    public enum EnemyBrainState { Idle, Wander, Chase, Attack, Investigate, Dead }

    [CreateAssetMenu(menuName = "Scrapfire/Enemy AI Profile")]
    public sealed class EnemyAIProfile : ScriptableObject
    {
        public EnemyDefaultBehaviour DefaultBehaviour = EnemyDefaultBehaviour.Wander;
        public EnemyChaseMode ChaseMode = EnemyChaseMode.Seek;
        [Min(0.02f)] public float ThinkInterval = 0.15f;
        [Header("Sight and memory")]
        [Min(0.1f)] public float SightRadius = 12f;
        [Range(1f, 360f)] public float FieldOfView = 140f;
        public float EyeOffset = 0.65f;
        public LayerMask SightBlockingLayers = ~0;
        [Min(0f)] public float MemorySeconds = 4f;
        [Min(0f)] public float SearchSeconds = 3f;
        [Header("Movement")]
        [Min(0f)] public float WanderRadius = 6f;
        public Vector2 WanderPause = new Vector2(1f, 3f);
        [Min(0.1f)] public float WanderTravelTimeout = 10f;
        [Min(0f)] public float MaxPredictionSeconds = 0.8f;
        [Min(0f)] public float AttackExitMargin = 0.4f;
        [Min(0.1f)] public float UnreachableTimeout = 3f;
        [Header("Alert propagation")]
        [Min(0f)] public float AlertRadius = 10f;
        [Min(0.05f)] public float BroadcastInterval = 1f;
        [Min(0.1f)] public float AlertLifetime = 4f;
        [Range(0, 10)] public int MaximumAlertHops = 3;
        public bool AlertRequiresClearLine = true;
        public LayerMask AlertBlockingLayers = ~0;
    }
}
