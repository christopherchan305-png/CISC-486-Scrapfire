using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Levels;
using Scrapfire.Session;

namespace Scrapfire.Enemies.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyPerception : MonoBehaviour
    {
        private readonly RaycastHit[] _hits = new RaycastHit[64];

        public bool HasClearLine(Vector3 from, Vector3 to, LayerMask blockingLayers)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return true;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, _hits, delta.magnitude,
                blockingLayers, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return false; // Crowded ray: fail closed, never see through a wall.
            for (int i = 0; i < count; i++)
            {
                var hit = _hits[i].collider;
                if (hit.transform.IsChildOf(transform) || hit.GetComponentInParent<Character>() != null
                    || hit.GetComponentInParent<Enemy>() != null || hit.GetComponentInParent<RegionMovementBarrier>() != null) continue;
                return false;
            }
            return true;
        }

        public bool CanSee(CharacterPlayer player, EnemyAIProfile profile)
        {
            if (player == null || !player.CanAct || profile == null) return false;
            Vector3 delta = player.transform.position - transform.position;
            if (delta.sqrMagnitude > profile.SightRadius * profile.SightRadius) return false;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, delta) > profile.FieldOfView * 0.5f) return false;
            return HasClearLine(transform.position + Vector3.up * profile.EyeOffset,
                player.transform.position, profile.SightBlockingLayers);
        }

        public CharacterPlayer FindVisiblePlayer(SessionPlayers roster, EnemyAIProfile profile)
        {
            if (roster == null) return null;
            CharacterPlayer nearest = null;
            float distance = float.PositiveInfinity;
            foreach (var player in roster.Players)
            {
                if (!CanSee(player, profile)) continue;
                float candidate = (player.transform.position - transform.position).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate; nearest = player;
            }
            return nearest;
        }
    }
}
