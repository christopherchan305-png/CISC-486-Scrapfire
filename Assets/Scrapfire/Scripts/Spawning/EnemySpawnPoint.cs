using UnityEngine;
using Scrapfire.Levels;

namespace Scrapfire.Spawning
{
    public sealed class EnemySpawnPoint : MonoBehaviour
    {
        [Tooltip("Optional additional gate. EntitySpawner always checks all containing regions by position, even when this is empty.")]
        public MapRegion RequiredRegion;
        [Tooltip("Require navigation on the same floor before spawning an agent here.")]
        public bool RequireNavMesh;
        public float NavigationFootOffset = 1f;
        public int NavigationAgentType;
        public bool Available => isActiveAndEnabled && (RequiredRegion == null || RequiredRegion.IsUnlocked);
        public bool HasNavigation()
        {
            if (!RequireNavMesh) return true;
            Vector3 foot = transform.position - Vector3.up * NavigationFootOffset;
            var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = NavigationAgentType, areaMask = UnityEngine.AI.NavMesh.AllAreas };
            return UnityEngine.AI.NavMesh.SamplePosition(foot, out var hit, 0.6f, filter)
                && Mathf.Abs(hit.position.y - foot.y) <= 0.4f;
        }
        private void OnDrawGizmos()
        {
            // A small filled marker stays recognizable from any Scene view angle.
            Gizmos.color = new Color(0.1f, 0.45f, 1f, 1f);
            Gizmos.DrawSphere(transform.position, 0.22f);
            Gizmos.DrawWireSphere(transform.position, 0.3f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.1f, 0.45f, 1f, 1f);
            Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
        }
    }
}
