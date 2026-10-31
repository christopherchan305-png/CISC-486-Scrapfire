using System;
using UnityEngine;

namespace Scrapfire.Waves
{
    public enum GameDifficulty { Easy, Hard, Nightmare }

    [Serializable]
    public struct DifficultyPreset
    {
        public GameDifficulty Difficulty;
        [Min(0.01f)] public float Multiplier;
        public DifficultyPreset(GameDifficulty difficulty, float multiplier)
        { Difficulty = difficulty; Multiplier = multiplier; }
    }

    [CreateAssetMenu(menuName = "Scrapfire/Difficulty Settings")]
    public sealed class DifficultySettings : ScriptableObject
    {
        public AnimationCurve PlayerCountMultiplier = AnimationCurve.Linear(1f, 1f, 4f, 2.5f);
        public DifficultyPreset[] Presets = {
            new DifficultyPreset(GameDifficulty.Easy, 1f),
            new DifficultyPreset(GameDifficulty.Hard, 1.5f),
            new DifficultyPreset(GameDifficulty.Nightmare, 2f)
        };

        public float GetMultiplier(int players, GameDifficulty difficulty)
        {
            float playerMultiplier = PlayerCountMultiplier.Evaluate(Mathf.Max(1, players));
            foreach (var preset in Presets)
                if (preset.Difficulty == difficulty)
                {
                    float combined = playerMultiplier * preset.Multiplier;
                    if (combined > 0f && !float.IsNaN(combined) && !float.IsInfinity(combined)) return combined;
                    throw new InvalidOperationException("Difficulty multipliers must be positive and finite.");
                }
            throw new InvalidOperationException("Missing difficulty preset: " + difficulty);
        }
    }
}
