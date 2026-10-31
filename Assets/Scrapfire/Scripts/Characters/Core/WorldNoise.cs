using System;
using UnityEngine;

namespace Scrapfire.Characters.Core
{
    /// <summary>A location-only stimulus: hearing a sound never reveals an actor's identity.</summary>
    public readonly struct NoiseSignal
    {
        public readonly Vector3 Position;
        public readonly Vector3 NavigationPosition;
        public readonly float Radius;
        public NoiseSignal(Vector3 position, Vector3 navigationPosition, float radius)
        { Position = position; NavigationPosition = navigationPosition; Radius = radius; }
    }

    /// <summary>World-space gameplay noise, independent of AudioSource volume and enemy AI.</summary>
    public static class WorldNoise
    {
        public static event Action<NoiseSignal> Emitted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => Emitted = null;

        public static bool Emit(Vector3 position, Vector3 navigationPosition, float radius)
        {
            if (radius <= 0f || float.IsNaN(radius) || float.IsInfinity(radius)
                || !Finite(position) || !Finite(navigationPosition)) return false;
            Emitted?.Invoke(new NoiseSignal(position, navigationPosition, radius));
            return true;
        }
        private static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
    }
}
