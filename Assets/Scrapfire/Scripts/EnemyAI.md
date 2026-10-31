# Enemy AI prototype

## Current scene

Open `Assets/Scrapfire/Scenes/SampleScene.unity`. The existing camera settings are preserved.
`SurvivalNavigation` contains the saved, baked NavMeshSurface. A separate **Scrapfire Zombie**
agent type uses radius 0.4, height 1.8, step height 0.35 and maximum slope 50 degrees;
the TopDownEngine demo's Humanoid type is unchanged. Bake voxel size is 0.08.
The outdoor arena, ground floor, physical staircase and upper floor are connected.
The roof is Not Walkable. There are no teleport links or runtime full-scene bakes.

- `SurvivalEnemy.prefab` uses `ZombieSeek.asset`.
- `PursuitEnemy.prefab` uses `ZombiePursue.asset`.
- Wave one uses Seek enemies. Waves two and three include Pursue enemies.
- Both profiles default to **Wander**. Set Default Behaviour to **Idle** for stationary enemies.
- Enemy01 remains a scene enemy inside the building and waits for the entrance region to open.
- All eight existing SpawnPoints are preserved and require navigation on their own floor.

## State machine

| State | Behaviour | Exit |
|---|---|---|
| Idle | Stay still and scan | See a player / receive an alert |
| Wander | Choose a reachable location near the spawn origin, walk, pause | See a player / receive an alert |
| Chase | Move toward observed or remembered target position and broadcast | Attack range, expired memory, or unreachable path |
| Attack | Face target, obey Enemy attack cooldown and deal melee damage | Out of range, lost sight, or invalid target |
| Investigate | Visit last known position, then turn and search for a limited time | See a player, receive fresh information, or finish searching |
| Dead | Stop agent, perception, attacks and broadcasts | Explicit respawn / health reset only |

Attack begins within EnemyDefinition.AttackRange. AttackExitMargin (0.4) prevents toggling at
one exact distance. When just outside hit range, the enemy closes the gap without applying damage.
A target that is downed, dead or disabled is no longer pursued; another visible standing player
can be acquired. Death/disable immediately stops and disables the navigation agent.

## Responsibility boundaries

- **Enemy** owns health, scaled speed/damage, attack range/cooldown, kill/bounty integration.
- **EnemyBrain** owns each enemy's state, timers, target and last observed information.
- **EnemyAIProfile** is shared configuration only; never put runtime target/state in the asset.
- **EnemyPerception** checks distance, horizontal field of view and real physics line of sight.
- **EnemyNavigation** owns agent binding, same-storey projection, complete-path validation,
  movement and stop/suspend. It knows neither wave scheduling nor purchases.
- **EnemyAlertRelay** delivers messages spatially. Receiving never broadcasts recursively.
- **TargetMotion** records actual player displacement; it does not read input. Large teleports
  reset velocity. Existing CharacterPlayers now require this component.
- **RegionNavigationGate** translates MapRegion geometry/unlock events into carving obstacles.
  MapRegion and Balance remain unaware of navigation and AI.
- **EnemyAnimationDriver** adapts movement speed and successful attacks to a child Animator.
  Animation does not move the root actor.

## Sight and target memory

Defaults: sight radius 12 m, view angle 140 degrees, eye offset 0.65 m above the enemy root,
and a staggered thinking interval of 0.15 s. The sample masks include Default, Ground and
Obstacles. Trigger volumes, other actors and RegionMovementBarrier are ignored by sight rays.
Real walls, floors, crates and pillars obstruct sight and therefore melee attacks.

The retained target is stable while usable. A visible target updates LastKnownPosition,
LastKnownVelocity and LastSeenTime. Hidden targets never update those values from their live
Transform. Memory lasts 4 s; after it expires the enemy investigates the remembered position,
searches for 3 s, then returns to its configured default behaviour. Unreachable paths and
wander travel have finite timeouts; blocked agents do not teleport through geometry.

**Seek:** use the observed position directly.

**Pursue:** add observed horizontal velocity multiplied by
`min(horizontal distance / max(enemy speed, 0.1), MaxPredictionSeconds)`.
Prediction defaults to at most 0.8 s. A stopped player yields Seek behaviour. TargetMotion
uses actual displacement, so pressing movement into a wall does not create forward velocity.
Only current visual contact permits prediction. Projection rejects a large vertical change;
if the predicted point has no complete path, use the last observed position instead.

## Alerts / reinforcement

Chasing and attacking enemies broadcast every 1 s in a 10 m radius. Default alerts require
a clear line between sender and receiver; walls and floors block propagation. This is configurable
independently of visual detection. A receiver does not have to see the player.

An alert contains Target, observed floor Position, horizontal Velocity, ObservedAt, ExpiresAt,
IncidentId and HopCount. Idle/Wander enemies, and investigating enemies with fresher information,
can enter Chase from it. Receivers relay after a short 0.12–0.25 s delay. Relays preserve the
original timestamp and expiration; they increment hop count. Default lifetime is 4 s and maximum
propagation depth is three hops. Old information cannot overwrite newer information about the
same target. Directly seeing the player permits a new observation and resets the relay depth.

This prevents two enemies keeping each other alerted forever after the player disappears.
Reinforcement currently wakes **existing enemies**; it does not create extra enemies or bypass
WaveDirector's count / maximum alive rules.

## Dynamic locks and navigation

Static NavMesh is baked with the building open. At runtime each locked region creates carving
NavMeshObstacle boxes matching its existing local volumes. Entrance and upper-floor/stair gates
are independent; overlapping navigation remains blocked until all relevant gates open.
Unlocking removes the relevant carving obstacles without rebuilding the whole scene.
Carving changes become effective on subsequent navigation updates (normally the following frame).
The upper-floor box does not remove ground-floor navigation below it; the separate stair box
prevents entering the staircase from below. Physics barriers still stop players, while bullets
continue to use their existing barrier-exclusion rules.

SpawnPoint.RequireNavMesh checks its foot position against the configured agent type and
rejects projections onto another storey. EntitySpawner checks both region access and navigation
before allocating a spawn, and keeps its existing balanced distribution / no-random-fallback rules.

After editing static geometry, stop Play and use **Scrapfire > Rebake Survival Navigation**.
The separate **Configure Enemy AI And Bake Navigation** menu is a sample authoring/setup tool;
it also configures profiles, prefabs and later-wave enemy composition. Re-baking alone preserves
those gameplay settings and the camera. Do not enable a second motor/root-motion controller on
these enemies. Their root pivot is body-center, 1 m above the nav floor (agent Base Offset = 1).

## Imported presentation

The enemy child model and Idle/Walking clips come from TopDownEngine's Loft3D Tie character;
the melee clip comes from its Suit character. A separate ZombieAnimator controller blends
idle/walk and triggers melee. Original demo assets and engine behaviour scripts are untouched.
FBX preview cameras, lights and listeners are stripped from the copied model, so they cannot
replace the player's camera. Physics and combat still use the Scrapfire actor root.

## Verification / demonstration

Automated tests cover prediction limits, FOV/range/occlusion, actual motion, wandering vs idle,
chasing and real melee damage, death cleanup, neighbour-to-neighbour alerts, hop limit,
preserved timestamps, stale information, wall/floor alert occlusion, lost-sight search,
both dynamic locks, same-floor spawning and actual agent traversal of the real staircase.
Existing weapon and physical-stair regressions freeze AI where their subject is unrelated.

For a short demo: move near a roaming enemy, let it notice you and bring neighbours, circle a
pillar or the building to break sight, shoot enemies for money, unlock the building and then
stairs, and lead an enemy up to floor two. The camera floor fade and all player resource systems
continue to work independently.

Not included in this AI pass: destructible doors, group formations, extra spawned reinforcement,
boss behaviours, or multiplayer transport / server authority. For networking,
run EnemyBrain and agents on the host/server; the local alert registry is not a network transport.

## Standalone build check

`Builds/ScrapfireAIPrototype.app` is a macOS development build containing only SampleScene as
its scene entry (the project's existing TopDownEngine demo build-list is preserved).
The project's own PC_RPAsset now disables GPU Resident Drawer: its previous configuration
stripped required DOTS instancing variants in Player builds and produced missing objects.
The ordinary URP path is sufficient for this small prototype and preserves per-camera material
fades. Original TopDownEngine pipeline assets are unchanged. The full gameplay regression has
80 passing tests; the three building rendering tests also passed after this rendering change.


## Gunshots and bullet-hit awareness

WeaponData.GunshotRadius controls gameplay audibility independently for each weapon.
Current values are pistol 12 m, rifle 22 m, shotgun 28 m and sniper 35 m. Zero is silent.
RangedWeapon emits one WorldNoise signal per actual discharge, before resolving pellets;
empty triggers, reloads, warmup requests and cancelled warmups emit no gunshot.
The event contains the muzzle location, a floor navigation anchor and radius, with no player identity.

EnemyHearing subscribes while enabled. A sound within its 3D radius promotes Idle/Wander
or an existing investigation into Investigate: walk toward the recorded sound location, search,
then return to the default behaviour. Hearing is radius-based and can pass through walls/floors;
movement still requires a valid path and cannot bypass walls or locked regions.
A sound never updates itself as the shooter moves, and it cannot interrupt Chase/Attack.
It does not generate the identified zombie alert broadcast. Normal visual detection can confirm
a player while investigating and then enter Chase.

EnemyBrain also listens to successfully applied Health.Damaged events. A nonlethal Projectile
hit attributed to a standing CharacterPlayer immediately switches to Chase and records that
attacker's current position once, including when outside sight radius/FOV. Further hidden
movement is not read; visual confirmation is still required for continuous tracking and melee.
The attacker can supersede an existing target, and the usual bounded alert broadcasts can then
bring neighbours into combat. Immune/rejected hits, environmental damage, dead brains and
lethal hits do not create a new chase.

EnemyHearing is required on newly added brains and has also been added to the two saved enemy
prefabs and existing scene enemy. Weapons depend only on WorldNoise, never on enemy AI types.
Additional tests cover radius exclusion, anonymous searching and actual movement, search timeout,
visual promotion, immediate attacker attribution, hidden-position memory, combat priority,
new attackers, lethal/immune hits and a real six-pellet shotgun discharge with one noise event.
