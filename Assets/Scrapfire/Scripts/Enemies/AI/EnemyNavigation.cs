using UnityEngine;
using UnityEngine.AI;

namespace Scrapfire.Enemies.AI
{
    /// <summary>Only this adapter drives the agent; callers pass floor positions, never body centers.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class EnemyNavigation : MonoBehaviour
    {
        [Min(0.05f)] public float ProjectionRadius = 1.2f;
        [Min(0.01f)] public float MaximumProjectionHeight = 0.55f;
        public NavMeshAgent Agent { get; private set; }
        public bool Ready => Agent != null && Agent.enabled && Agent.isOnNavMesh;
        public Vector3 FootPosition => transform.position - Vector3.up * (Agent != null ? Agent.baseOffset : 1f);
        public Vector3 Velocity => Ready ? Agent.velocity : Vector3.zero;
        public bool Arrived => Ready && !Agent.pathPending && (!Agent.hasPath
            || Agent.remainingDistance <= Mathf.Max(0.15f, Agent.stoppingDistance + 0.1f));
        public string LastFailure { get; private set; }
        private NavMeshPath _path;
        private float _nextBind;
        private void Awake() => Agent = GetComponent<NavMeshAgent>();

        public bool EnsureReady()
        {
            if (Ready) return true;
            if (Time.time < _nextBind) return false;
            _nextBind = Time.time + 0.5f;
            if (TryProject(FootPosition, out var floor))
            {
                Agent.enabled = true;
                if (Agent.Warp(floor + Vector3.up * Agent.baseOffset))
                { LastFailure = null; return true; }
            }
            Agent.enabled = false;
            LastFailure = "No accessible NavMesh on this storey.";
            return false;
        }

        public bool TryProject(Vector3 desired, out Vector3 floor)
        {
            floor = desired;
            var filter = new NavMeshQueryFilter { agentTypeID = Agent.agentTypeID, areaMask = Agent.areaMask };
            if (!NavMesh.SamplePosition(desired, out var hit, ProjectionRadius, filter)
                || Mathf.Abs(hit.position.y - desired.y) > MaximumProjectionHeight) return false;
            floor = hit.position;
            return true;
        }

        public bool TryMoveTo(Vector3 destination, float stoppingDistance = 0.1f)
        {
            _path ??= new NavMeshPath();
            if (!EnsureReady() || !TryProject(destination, out var floor)
                || !Agent.CalculatePath(floor, _path) || _path.status != NavMeshPathStatus.PathComplete)
            { LastFailure = "Destination is unreachable on this storey."; Stop(); return false; }
            Agent.stoppingDistance = Mathf.Max(0f, stoppingDistance);
            Agent.isStopped = false;
            bool accepted = Agent.SetPath(_path);
            LastFailure = accepted ? null : "Path could not be assigned.";
            return accepted;
        }

        public void Stop()
        {
            if (!Ready) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        public void Suspend() { Stop(); if (Agent != null) Agent.enabled = false; }
        private void OnDisable() => Suspend();
    }
}
