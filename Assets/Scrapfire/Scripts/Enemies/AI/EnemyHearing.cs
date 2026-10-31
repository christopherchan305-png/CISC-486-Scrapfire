using UnityEngine;
using Scrapfire.Characters.Core;

namespace Scrapfire.Enemies.AI
{
    [DisallowMultipleComponent]
    public sealed class EnemyHearing : MonoBehaviour
    {
        private void OnEnable() => WorldNoise.Emitted += OnNoise;
        private void OnDisable() => WorldNoise.Emitted -= OnNoise;
        private void OnNoise(NoiseSignal signal)
        {
            if ((signal.Position - transform.position).sqrMagnitude > signal.Radius * signal.Radius) return;
            var brain = GetComponent<EnemyBrain>();
            if (brain != null) brain.TryInvestigateNoise(signal.NavigationPosition);
        }
    }
}
