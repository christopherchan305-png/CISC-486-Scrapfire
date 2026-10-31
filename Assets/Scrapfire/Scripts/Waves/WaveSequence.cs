using System;
using UnityEngine;
using Scrapfire.Enemies;

namespace Scrapfire.Waves
{
    [Serializable]
    public sealed class WaveSpawnGroup
    {
        public Enemy Prefab;
        [Min(1)] public int Count = 5;
        [Min(0.01f)] public float Interval = 1f;
    }

    [Serializable]
    public sealed class WaveDefinition
    {
        public string Name = "Wave";
        [Min(0f)] public float PreparationSeconds = 5f;
        [Min(1)] public int MaximumAlive = 10;
        [Min(0.01f)] public float HealthMultiplier = 1f;
        [Min(0.01f)] public float DamageMultiplier = 1f;
        public WaveSpawnGroup[] Groups = Array.Empty<WaveSpawnGroup>();
    }

    [CreateAssetMenu(menuName = "Scrapfire/Wave Sequence")]
    public sealed class WaveSequence : ScriptableObject
    {
        public WaveDefinition[] Waves = Array.Empty<WaveDefinition>();
        [Tooltip("If enabled, repeat the last configured wave with this additional multiplier each repeat.")]
        public bool RepeatLastWave;
        [Min(1f)] public float RepeatMultiplier = 1.15f;
    }
}
