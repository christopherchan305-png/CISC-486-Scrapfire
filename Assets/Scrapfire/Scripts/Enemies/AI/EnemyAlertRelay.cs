using System.Collections.Generic;
using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Enemies.AI
{
    public readonly struct EnemyAlertMessage
    {
        public readonly CharacterPlayer Target;
        public readonly Vector3 Position, Velocity;
        public readonly float ObservedAt, ExpiresAt;
        public readonly int IncidentId, HopCount;
        public EnemyAlertMessage(CharacterPlayer target, Vector3 position, Vector3 velocity,
            float observedAt, float expiresAt, int incidentId, int hopCount)
        {
            Target = target; Position = position; Velocity = velocity;
            ObservedAt = observedAt; ExpiresAt = expiresAt; IncidentId = incidentId; HopCount = hopCount;
        }
    }

    /// <summary>Local spatial delivery only. Receiving never broadcasts recursively.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAlertRelay : MonoBehaviour
    {
        private static readonly HashSet<EnemyAlertRelay> Receivers = new HashSet<EnemyAlertRelay>();
        private static int _nextIncident;
        public static int NewIncident() => ++_nextIncident;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Receivers.Clear(); _nextIncident = 0; }
        private void OnEnable() => Receivers.Add(this);
        private void OnDisable() => Receivers.Remove(this);

        public int Broadcast(EnemyAlertMessage message, EnemyAIProfile profile, EnemyPerception perception)
        {
            if (message.Target == null || Time.time >= message.ExpiresAt || message.HopCount >= profile.MaximumAlertHops) return 0;
            int received = 0;
            foreach (var receiver in Receivers)
            {
                if (receiver == null || receiver == this || !receiver.isActiveAndEnabled
                    || (receiver.transform.position - transform.position).sqrMagnitude > profile.AlertRadius * profile.AlertRadius) continue;
                if (profile.AlertRequiresClearLine && !perception.HasClearLine(
                    transform.position, receiver.transform.position, profile.AlertBlockingLayers)) continue;
                var brain = receiver.GetComponent<EnemyBrain>();
                if (brain != null && brain.TryReceiveAlert(message)) received++;
            }
            return received;
        }
    }
}
