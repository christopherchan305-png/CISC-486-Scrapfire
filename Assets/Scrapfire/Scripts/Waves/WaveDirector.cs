using System;
using UnityEngine;
using Scrapfire.Session;
using Scrapfire.Spawning;

namespace Scrapfire.Waves
{
    public enum WaveState { Idle, Preparing, Spawning, Clearing, Completed, Failed }

    /// <summary>Schedules configured waves; the spawner owns enemy instances, the roster owns players.</summary>
    public sealed class WaveDirector : MonoBehaviour
    {
        public WaveSequence Sequence;
        public DifficultySettings DifficultySettings;
        public GameDifficulty Difficulty = GameDifficulty.Easy;
        public SessionPlayers Players;
        public EntitySpawner Spawner;
        public bool AutoStart = true;
        public WaveState State { get; private set; }
        public int WaveNumber { get; private set; }
        public int PendingCount { get; private set; }
        public float AppliedMultiplier { get; private set; }
        public string StatusMessage { get; private set; }
        public event Action<WaveState> StateChanged;
        public event Action<int> WaveStarted;
        private WaveDefinition _wave;
        private int[] _remaining;
        private int _group;
        private float _nextAction;

        private void Start() { if (AutoStart) BeginSession(); }

        public bool BeginSession()
        {
            if (State != WaveState.Idle && State != WaveState.Completed && State != WaveState.Failed) return false;
            if (Sequence == null || DifficultySettings == null || Spawner == null || Players == null
                || Sequence.Waves == null || Sequence.Waves.Length == 0 || Players.ActiveCount == 0)
            {
                StatusMessage = "Assign a wave sequence, difficulty, spawner and session players before starting.";
                return false;
            }
            Spawner.DespawnAll();
            WaveNumber = 0;
            PrepareNextWave();
            return State == WaveState.Preparing;
        }

        private void SetState(WaveState state)
        {
            State = state;
            StateChanged?.Invoke(state);
        }

        private void PrepareNextWave()
        {
            if (WaveNumber >= Sequence.Waves.Length && !Sequence.RepeatLastWave)
            { SetState(WaveState.Completed); return; }
            _wave = Sequence.Waves[Mathf.Min(WaveNumber, Sequence.Waves.Length - 1)];
            WaveNumber++;
            if (_wave == null || _wave.Groups == null || _wave.Groups.Length == 0 || _wave.MaximumAlive < 1)
            { Fail("Wave configuration has no groups or an invalid alive limit."); return; }
            _nextAction = Time.time + Mathf.Max(0f, _wave.PreparationSeconds);
            StatusMessage = null;
            SetState(WaveState.Preparing);
        }

        private void StartWave()
        {
            try
            {
                int repeat = Mathf.Max(0, WaveNumber - Sequence.Waves.Length);
                AppliedMultiplier = DifficultySettings.GetMultiplier(Players.ActiveCount, Difficulty)
                    * Mathf.Pow(Sequence.RepeatMultiplier, repeat);
                _remaining = new int[_wave.Groups.Length];
                PendingCount = 0;
                for (int i = 0; i < _remaining.Length; i++)
                {
                    var group = _wave.Groups[i];
                    if (group == null || group.Prefab == null || group.Prefab.Definition == null
                        || group.Count < 1 || group.Interval <= 0f)
                        throw new InvalidOperationException("Each wave group needs an enemy prefab, positive count and interval.");
                    double count = Math.Ceiling(group.Count * (double)AppliedMultiplier);
                    if (double.IsNaN(count) || count > int.MaxValue - PendingCount)
                        throw new InvalidOperationException("Wave count exceeds supported limits.");
                    _remaining[i] = (int)count;
                    PendingCount += _remaining[i];
                }
                _group = 0;
                Spawner.BeginWave();
                _nextAction = Time.time;
                SetState(WaveState.Spawning);
                WaveStarted?.Invoke(WaveNumber);
            }
            catch (Exception exception) { Fail(exception.Message); }
        }

        private void Update()
        {
            if (State == WaveState.Idle || State == WaveState.Completed || State == WaveState.Failed) return;
            if (!Players.HasStandingPlayer) { Fail("All session players are downed, dead or disconnected."); return; }
            if (State == WaveState.Preparing)
            {
                if (Time.time >= _nextAction) StartWave();
                return;
            }
            if (State == WaveState.Spawning && Time.time >= _nextAction && Spawner.AliveCount < _wave.MaximumAlive)
            {
                while (_group < _remaining.Length && _remaining[_group] == 0) _group++;
                if (_group < _remaining.Length)
                {
                    var group = _wave.Groups[_group];
                    if (Spawner.TrySpawn(group.Prefab, _wave.HealthMultiplier * AppliedMultiplier,
                        _wave.DamageMultiplier * AppliedMultiplier, out _))
                    {
                        _remaining[_group]--;
                        PendingCount--;
                        StatusMessage = null;
                    }
                    else StatusMessage = Spawner.LastFailure;
                    _nextAction = Time.time + Mathf.Max(0.01f, group.Interval / AppliedMultiplier);
                }
                if (PendingCount == 0) SetState(WaveState.Clearing);
            }
            if (State == WaveState.Clearing && Spawner.AliveCount == 0) PrepareNextWave();
        }

        public void Fail(string reason)
        {
            StatusMessage = reason;
            SetState(WaveState.Failed);
        }
    }
}
