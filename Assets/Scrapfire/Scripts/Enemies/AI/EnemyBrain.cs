using System;
using UnityEngine;
using Scrapfire.Characters.Core;
using Scrapfire.Levels;
using Scrapfire.Session;

namespace Scrapfire.Enemies.AI
{
    [DisallowMultipleComponent, RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyNavigation), typeof(EnemyPerception), typeof(EnemyAlertRelay))]
    [RequireComponent(typeof(EnemyHearing))]
    public sealed class EnemyBrain : MonoBehaviour
    {
        public EnemyAIProfile Profile;
        public SessionPlayers Players;
        public EnemyBrainState State { get; private set; }
        public CharacterPlayer Target { get; private set; }
        public Vector3 LastKnownPosition { get; private set; }
        public Vector3 LastKnownVelocity { get; private set; }
        public float LastSeenTime { get; private set; } = float.NegativeInfinity;
        public bool HasVisualContact { get; private set; }
        public int AlertsDelivered { get; private set; }
        public EnemyNavigation Navigation { get; private set; }
        public event Action<EnemyBrainState, EnemyBrainState> StateChanged;
        private Enemy _enemy;
        private Health _health;
        private EnemyPerception _perception;
        private EnemyAlertRelay _relay;
        private Vector3 _home;
        private bool _initialized, _wandering;
        private float _nextThink, _nextBroadcast, _waitUntil, _travelDeadline, _searchUntil, _unreachableSince;
        private int _incident, _hops;
        private float _messageExpiry;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>(); _health = GetComponent<Health>();
            Navigation = GetComponent<EnemyNavigation>(); _perception = GetComponent<EnemyPerception>();
            _relay = GetComponent<EnemyAlertRelay>();
        }
        private void OnEnable()
        {
            _health.StateChanged += OnHealthChanged;
            _health.Damaged += OnDamaged;
            _initialized = false;
            _nextThink = Time.time + UnityEngine.Random.Range(0f, 0.1f);
        }
        private void OnDisable()
        {
            _health.StateChanged -= OnHealthChanged;
            _health.Damaged -= OnDamaged;
            Navigation.Suspend();
            HasVisualContact = false;
        }
        private void OnHealthChanged(HealthState previous, HealthState current)
        {
            if (current != HealthState.Alive) { ChangeState(EnemyBrainState.Dead); Navigation.Suspend(); }
            else _initialized = false;
        }

        private void OnDamaged(DamageSource damage)
        {
            if (!isActiveAndEnabled || Profile == null || !_enemy.IsAlive || _enemy.Definition == null
                || damage.Type != DamageType.Projectile || damage.CausedBy == null) return;
            var attacker = damage.CausedBy.GetComponentInParent<CharacterPlayer>();
            if (attacker == null || !attacker.CanAct) return;
            if (!_initialized) Initialize();
            // A hit identifies its attacker once; subsequent position updates still require real sight.
            Observe(attacker);
            HasVisualContact = _perception.CanSee(attacker, Profile);
            ChangeState(EnemyBrainState.Chase);
            _nextThink = Time.time;
            _nextBroadcast = Time.time;
            if (Navigation.EnsureReady())
            {
                Navigation.Agent.speed = _enemy.MovementSpeed;
                Navigation.TryMoveTo(LastKnownPosition, Mathf.Max(0.1f, _enemy.Definition.AttackRange * 0.65f));
            }
        }

        public bool TryInvestigateNoise(Vector3 position)
        {
            if (!isActiveAndEnabled || Profile == null || !_enemy.IsAlive || _enemy.Definition == null) return false;
            if (!_initialized) Initialize();
            // Confirmed combat takes priority over an anonymous sound.
            if (State == EnemyBrainState.Chase || State == EnemyBrainState.Attack || State == EnemyBrainState.Dead) return false;
            Target = null; HasVisualContact = false;
            LastKnownPosition = position; LastKnownVelocity = Vector3.zero; LastSeenTime = float.NegativeInfinity;
            _incident = 0; _hops = 0; _messageExpiry = float.NegativeInfinity;
            BeginInvestigation();
            _nextThink = Time.time;
            return true;
        }

        private void Initialize()
        {
            _initialized = true;
            if (Players == null) Players = FindFirstObjectByType<SessionPlayers>();
            _home = Navigation.FootPosition;
            Target = null; LastSeenTime = float.NegativeInfinity; HasVisualContact = false;
            _incident = 0; _hops = 0; AlertsDelivered = 0;
            ReturnToDefault();
        }

        private void Update()
        {
            if (Profile == null || _enemy.Definition == null || Time.time < _nextThink) return;
            _nextThink = Time.time + Mathf.Max(0.02f, Profile.ThinkInterval);
            if (!_initialized) Initialize();
            if (!_enemy.IsAlive) { ChangeState(EnemyBrainState.Dead); Navigation.Suspend(); return; }
            if (!Navigation.EnsureReady())
            {
                if (State == EnemyBrainState.Investigate && Time.time >= _travelDeadline)
                { Target = null; ReturnToDefault(); }
                return;
            }
            Navigation.Agent.speed = _enemy.MovementSpeed;
            if (Target != null && !Target.CanAct) { Target = null; ReturnToDefault(); }
            HasVisualContact = Target != null && _perception.CanSee(Target, Profile);
            if (HasVisualContact) Observe(Target);
            else if (Target == null)
            {
                var visible = _perception.FindVisiblePlayer(Players, Profile);
                if (visible != null) { Observe(visible); HasVisualContact = true; }
            }
            if (HasVisualContact && State != EnemyBrainState.Chase && State != EnemyBrainState.Attack)
                ChangeState(EnemyBrainState.Chase);
            switch (State)
            {
                case EnemyBrainState.Idle: Navigation.Stop(); break;
                case EnemyBrainState.Wander: Wander(); break;
                case EnemyBrainState.Chase: Chase(); break;
                case EnemyBrainState.Attack: Attack(); break;
                case EnemyBrainState.Investigate: Investigate(); break;
            }
            BroadcastIfDue();
        }

        private void Observe(CharacterPlayer player)
        {
            if (Target != player || _incident == 0) _incident = EnemyAlertRelay.NewIncident();
            Target = player;
            LastKnownPosition = BuildingVisibility.FootPosition(player.transform);
            var motion = player.GetComponent<TargetMotion>();
            LastKnownVelocity = motion != null ? motion.Velocity : Vector3.zero;
            LastSeenTime = Time.time;
            _messageExpiry = Time.time + Mathf.Max(0.1f, Profile.AlertLifetime);
            _hops = 0;
        }

        public bool TryReceiveAlert(EnemyAlertMessage message)
        {
            if (!isActiveAndEnabled || Profile == null || !_enemy.IsAlive || message.Target == null || !message.Target.CanAct
                || Time.time >= message.ExpiresAt || message.ObservedAt > Time.time + 0.01f
                || Time.time - message.ObservedAt > Profile.AlertLifetime || message.HopCount >= Profile.MaximumAlertHops) return false;
            if (!_initialized) Initialize();
            if (State != EnemyBrainState.Idle && State != EnemyBrainState.Wander && State != EnemyBrainState.Investigate) return false;
            if (Target == message.Target && message.ObservedAt <= LastSeenTime) return false;
            Target = message.Target; LastKnownPosition = message.Position; LastKnownVelocity = message.Velocity;
            LastSeenTime = message.ObservedAt; _messageExpiry = message.ExpiresAt;
            _incident = message.IncidentId; _hops = message.HopCount + 1;
            HasVisualContact = false;
            ChangeState(EnemyBrainState.Chase);
            _nextBroadcast = Time.time + UnityEngine.Random.Range(0.12f, 0.25f);
            return true;
        }

        private void BroadcastIfDue()
        {
            if ((State != EnemyBrainState.Chase && State != EnemyBrainState.Attack) || Target == null
                || !Target.CanAct || Time.time < _nextBroadcast || Time.time >= _messageExpiry) return;
            _nextBroadcast = Time.time + Mathf.Max(0.05f, Profile.BroadcastInterval);
            AlertsDelivered += _relay.Broadcast(new EnemyAlertMessage(Target, LastKnownPosition, LastKnownVelocity,
                LastSeenTime, _messageExpiry, _incident, _hops), Profile, _perception);
        }

        private void Wander()
        {
            if (_wandering && !Navigation.Arrived && Time.time < _travelDeadline) return;
            if (_wandering)
            {
                _wandering = false; Navigation.Stop();
                _waitUntil = Time.time + UnityEngine.Random.Range(Mathf.Max(0f, Profile.WanderPause.x), Mathf.Max(Profile.WanderPause.x, Profile.WanderPause.y));
            }
            if (Time.time < _waitUntil) return;
            for (int i = 0; i < 8; i++)
            {
                Vector2 offset = UnityEngine.Random.insideUnitCircle * Profile.WanderRadius;
                Vector3 goal = _home + new Vector3(offset.x, 0f, offset.y);
                if ((goal - Navigation.FootPosition).sqrMagnitude < 0.5f || !Navigation.TryMoveTo(goal)) continue;
                _wandering = true; _travelDeadline = Time.time + Mathf.Max(0.1f, Profile.WanderTravelTimeout); return;
            }
            _waitUntil = Time.time + 1f;
        }

        public static Vector3 ChaseDestination(EnemyChaseMode mode, Vector3 observer, Vector3 target,
            Vector3 velocity, float speed, float maximumPrediction)
        {
            if (mode == EnemyChaseMode.Seek) return target;
            velocity.y = 0f;
            Vector3 distance = target - observer; distance.y = 0f;
            float prediction = Mathf.Clamp(distance.magnitude / Mathf.Max(0.1f, speed), 0f, Mathf.Max(0f, maximumPrediction));
            return target + Vector3.ClampMagnitude(velocity, 20f) * prediction;
        }

        private bool MemoryExpired => Target == null || Time.time - LastSeenTime > Mathf.Max(0f, Profile.MemorySeconds);
        private void Chase()
        {
            if (MemoryExpired) { BeginInvestigation(); return; }
            if (HasVisualContact && Vector3.Distance(transform.position, Target.transform.position) <= _enemy.Definition.AttackRange)
            { ChangeState(EnemyBrainState.Attack); Attack(); return; }
            Vector3 destination = ChaseDestination(Profile.ChaseMode, Navigation.FootPosition,
                LastKnownPosition, HasVisualContact ? LastKnownVelocity : Vector3.zero, _enemy.MovementSpeed, Profile.MaxPredictionSeconds);
            bool moving = Navigation.TryMoveTo(destination, Mathf.Max(0.1f, _enemy.Definition.AttackRange * 0.65f));
            if (!moving && destination != LastKnownPosition) moving = Navigation.TryMoveTo(LastKnownPosition, 0.5f);
            if (moving) _unreachableSince = -1f;
            else if (_unreachableSince < 0f) _unreachableSince = Time.time;
            else if (Time.time - _unreachableSince >= Profile.UnreachableTimeout) BeginInvestigation();
        }

        private void Attack()
        {
            if (Target == null || !HasVisualContact) { ChangeState(EnemyBrainState.Chase); return; }
            float distance = Vector3.Distance(transform.position, Target.transform.position);
            if (distance > _enemy.Definition.AttackRange + Profile.AttackExitMargin)
            { ChangeState(EnemyBrainState.Chase); return; }
            if (distance > _enemy.Definition.AttackRange)
            { Navigation.TryMoveTo(LastKnownPosition, _enemy.Definition.AttackRange * 0.65f); return; }
            Navigation.Stop();
            Vector3 facing = Target.transform.position - transform.position; facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(facing);
            // Perception checked the real wall/floor line before reaching this state.
            _enemy.TryAttack(Target.CharacterHealth);
        }

        private void BeginInvestigation()
        {
            ChangeState(EnemyBrainState.Investigate);
            _searchUntil = -1f;
            _travelDeadline = Time.time + Mathf.Max(Profile.UnreachableTimeout, Profile.WanderTravelTimeout);
            if (!Navigation.TryMoveTo(LastKnownPosition)) _searchUntil = Time.time + Mathf.Max(0f, Profile.SearchSeconds);
        }
        private void Investigate()
        {
            if (_searchUntil < 0f && (Navigation.Arrived || Time.time >= _travelDeadline))
            { Navigation.Stop(); _searchUntil = Time.time + Mathf.Max(0f, Profile.SearchSeconds); }
            if (_searchUntil < 0f) return;
            transform.Rotate(0f, 45f * Mathf.Max(0.02f, Profile.ThinkInterval), 0f);
            if (Time.time >= _searchUntil) { Target = null; ReturnToDefault(); }
        }

        private void ReturnToDefault()
        {
            Navigation.Stop(); _wandering = false; _waitUntil = Time.time;
            _unreachableSince = -1f;
            ChangeState(Profile.DefaultBehaviour == EnemyDefaultBehaviour.Idle ? EnemyBrainState.Idle : EnemyBrainState.Wander);
        }
        private void ChangeState(EnemyBrainState next)
        {
            if (State == next) return;
            var previous = State; State = next;
            if (next == EnemyBrainState.Chase) _unreachableSince = -1f;
            StateChanged?.Invoke(previous, next);
        }

        private void OnDrawGizmosSelected()
        {
            if (Profile == null) return;
            Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.5f); Gizmos.DrawWireSphere(transform.position, Profile.SightRadius);
            Gizmos.color = new Color(0.8f, 0.2f, 1f, 0.35f); Gizmos.DrawWireSphere(transform.position, Profile.AlertRadius);
            if (Application.isPlaying && Target != null) { Gizmos.color = Color.red; Gizmos.DrawLine(transform.position, LastKnownPosition); }
        }
    }
}
