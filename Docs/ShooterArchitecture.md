# Shooter Game Architecture

## Goal

This document defines the initial architecture for the shooter MVP built on XiheFramework.

The MVP gameplay is:

- The player controls one aircraft.
- The aircraft has an extensible targeting vision area.
- Enemies inside the current vision rules continuously gain lock value.
- When an enemy reaches full lock value, the player aircraft launches a missile at it.
- Enemies spawn around the screen, move directly toward the player, and self-destruct near the player to deal damage.

The implementation should use a lightweight MVC vocabulary, but the deeper architectural goal is simulation/presentation separation. Gameplay state should be data-driven enough to keep Unity entities as presentation objects, without over-engineering the jam version.

This is a single-player jam build first. Reusable gameplay features are implemented as custom `GameModuleBase` modules, and game flow is controlled by the FSM module. The architecture should leave clean extension points for vision types and enemy behavior/types.

## XiheFramework Mapping

The architecture uses existing XiheFramework systems as follows:

| XiheFramework System | Role In Shooter |
| --- | --- |
| `Game.Fsm` | Owns the overall game loop states. |
| `Game.Blackboard` | Stores the shooter runtime model. |
| `GameModuleBase` | Base type for reusable gameplay feature modules. |
| `Game.Entity` | Instantiates and destroys visual entities. |
| `GameEntityBase` | Base type for gameplay views. |
| `Game.Event` | Decoupled notifications for UI, audio, VFX, and state transitions. |
| `Game.Resource` | Loads Addressable prefabs/assets. |
| `Game.UI` | Opens UI entities such as HUD and game over overlays. |

## Architecture Definition

This project does not use strict application-style MVC. For this shooter, MVC is only a naming aid:

```text
Model      = simulation state
View       = Unity presentation
Controller = state flow + gameplay systems
```

The stronger rule is:

```text
Input + Gameplay Rules + Runtime Model -> New Runtime Model -> View Sync
```

Gameplay decisions should be made from model data, tick timing, imported table info data, and targeting results. Views should not be required to calculate gameplay outcomes.

## Lightweight MVC Mapping

### Model

The Model layer is pure simulation/runtime data. It should not inherit `MonoBehaviour`, should not instantiate prefabs, and should not directly modify Unity transforms.

Model data is owned by `ShooterBlackboard`, created through `Game.Blackboard`.

Core model data should stay simple and inspectable. Full replay/multiplayer determinism is not part of the jam scope.

Examples:

- `ShooterBlackboard`
- `RunModel`
- `PlayerModel`
- `EnemyModel`
- `MissileModel`
- `TargetingRuntimeModel`
- `BuildModel`
- `ScoreModel`

### View

The View layer is implemented with `GameEntityBase` and UI entity types.

Views only present model state and play feedback. They should not own gameplay rules such as targeting, damage, missile launch decisions, or enemy spawning.

Examples:

- `PlayerPlaneEntity`
- `EnemyPlaneEntity`
- `MissileEntity`
- `VisionMaskEntity`
- `ExplosionEntity`
- `ShooterHudEntity`

### Controller

The Controller/System layer is split into two levels:

- FSM states control the game flow.
- Reusable custom `GameModuleBase` modules implement gameplay systems.

Examples:

- `ShooterBootState`
- `ShooterPrepareRunState`
- `ShooterPlayingState`
- `ShooterGameOverState`
- `PlayerControlModule`
- `EnemySpawnModule`
- `TargetingModule`
- `LockOnModule`
- `MissileModule`
- `DamageModule`
- `ShooterViewSyncModule`

## High-Level Runtime Flow

```text
StartMainLoop
  -> Create Shooter.Main FSM
  -> Register shooter states
  -> Start FSM

ShooterBootState
  -> Create ShooterBlackboard
  -> Ensure imported CSV info data is available
  -> Change to PrepareRun

ShooterPrepareRunState
  -> Reset run model
  -> Create player view
  -> Create HUD view
  -> Initialize runtime data
  -> Change to Playing

ShooterPlayingState
  -> Tick gameplay modules in fixed order
  -> Change to GameOver when player dies

ShooterGameOverState
  -> Stop gameplay ticking
  -> Show result UI
  -> Wait for restart / exit intent

ShooterCleanupState
  -> Destroy run entities
  -> Clear run data
  -> Change to PrepareRun or exit
```

## Directory Layout

Recommended project layout:

```text
Assets/Game/Scripts/Shooter/
  Bootstrap/
    StartMainLoop.cs

  State/
    ShooterFsmNames.cs
    ShooterBootState.cs
    ShooterPrepareRunState.cs
    ShooterPlayingState.cs
    ShooterPausedState.cs
    ShooterGameOverState.cs
    ShooterCleanupState.cs

  Model/
    ShooterBlackboard.cs
    RunModel.cs
    PlayerModel.cs
    EnemyModel.cs
    MissileModel.cs
    TargetingRuntimeModel.cs
    BuildModel.cs
    ScoreModel.cs

  Module/
    IShooterGameplayModule.cs
    PlayerControlModule.cs
    EnemySpawnModule.cs
    EnemyMovementModule.cs
    TargetingModule.cs
    LockOnModule.cs
    MissileModule.cs
    DamageModule.cs
    ShooterViewSyncModule.cs

  Targeting/
    ITargetingBackend.cs
    GpuMaskTargetingBackend.cs
    TargetingQueryInput.cs
    TargetingQueryResult.cs
    TargetingProfile.cs
    VisionShapeInfoAdapter.cs
    VisionShapeType.cs

  Enemy/
    EnemyInfoAdapter.cs
    EnemyType.cs
    IEnemyBehavior.cs
    EnemyBehaviorRegistry.cs

  View/
    PlayerPlaneEntity.cs
    EnemyPlaneEntity.cs
    MissileEntity.cs
    VisionMaskEntity.cs
    ExplosionEntity.cs
    ShooterHudEntity.cs

  Events/
    ShooterEvents.cs
```

## StartMainLoop

`StartMainLoop` is the scene entry script. It should stay thin.

Responsibilities:

- Create the main shooter FSM.
- Register all shooter states.
- Start the FSM.

It should not contain gameplay rules.

Example shape:

```csharp
public sealed class StartMainLoop : MonoBehaviour
{
    private void Start()
    {
        var fsm = Game.Fsm.CreateStateMachine(ShooterFsmNames.Main);

        fsm.AddState(new ShooterBootState(fsm));
        fsm.AddState(new ShooterPrepareRunState(fsm));
        fsm.AddState(new ShooterPlayingState(fsm));
        fsm.AddState(new ShooterGameOverState(fsm));
        fsm.AddState(new ShooterCleanupState(fsm));

        fsm.SetInitialState(ShooterFsmNames.Boot);
        fsm.OnStart();
    }
}
```

## ShooterBlackboard

`ShooterBlackboard` should live in:

```text
Assets/Game/Scripts/Shooter/Model/ShooterBlackboard.cs
```

It owns all runtime model data for the shooter mode.

Example shape:

```csharp
public sealed class ShooterBlackboard : IBlackboard
{
    public RunModel Run { get; } = new();
    public PlayerModel Player { get; } = new();
    public List<EnemyModel> Enemies { get; } = new();
    public List<MissileModel> Missiles { get; } = new();
    public TargetingRuntimeModel Targeting { get; } = new();
    public BuildModel Build { get; } = new();
    public ScoreModel Score { get; } = new();

    public void ResetRun()
    {
        Run.Reset();
        Player.Reset();
        Enemies.Clear();
        Missiles.Clear();
        Targeting.Reset();
        Score.Reset();
    }

    public void OnCreated() { }

    public void OnRelease()
    {
        ResetRun();
    }
}
```

Ownership rule:

```text
State creates, resets, and releases the blackboard.
Gameplay modules read and mutate blackboard data.
Views present blackboard data.
```

## Table Info Data

Enemy stats, player upgrades, build definitions, vision shape parameters, and similar design data should come from CSV tables imported at game startup.

The import pipeline converts CSV rows into `xxxInfo` classes and keeps them in memory. Runtime gameplay code should reference those imported info objects instead of inventing static config classes.

Examples:

```text
EnemyInfo
PlayerUpgradeInfo
VisionShapeInfo
BuildInfo
MissileInfo
```

Implementation rule:

```text
If a gameplay feature needs design data that is not already represented by an imported xxxInfo class or known table field, ask before adding fields or creating fallback static config.
```

Allowed:

- Runtime model references an info id or cached info reference.
- Modules read imported `xxxInfo` data.
- Small adapter classes convert imported info rows into runtime-friendly structures.

Avoid:

- Creating `ShooterGameConfig` or static config classes for design values.
- Hard-coding enemy stats, upgrade values, vision sizes, or lock parameters in modules.
- Guessing missing table fields during implementation.

## Gameplay Modules

Gameplay modules are reusable feature/system modules. They may inherit `GameModuleBase`, but the shooter loop should not rely on their `OnUpdate()` methods for core gameplay order.

Instead, each gameplay module should expose an explicit tick method:

```csharp
public interface IShooterGameplayModule
{
    void Tick(ShooterBlackboard blackboard, float deltaTime);
}
```

`ShooterPlayingState` calls gameplay modules in a stable explicit order.

Recommended order:

```text
1. PlayerControlModule
2. EnemySpawnModule
3. EnemyMovementModule
4. TargetingModule
5. LockOnModule
6. MissileModule
7. DamageModule
8. ShooterViewSyncModule
```

This avoids relying on XiheFramework module update ordering. The current framework tracks module priority, but `GameManager.Update()` iterates the alive module dictionary, so priority should not be treated as the core gameplay ordering mechanism.

## Gameplay Module Responsibilities

### PlayerControlModule

- Reads player input.
- Updates `PlayerModel.Position`.
- Updates `PlayerModel.Forward`.
- Applies movement speed and bounds rules.

### EnemySpawnModule

- Advances spawn timers.
- Chooses spawn points around the screen.
- Creates `EnemyModel` entries.
- Instantiates `EnemyPlaneEntity` through `Game.Entity`.

### EnemyMovementModule

- Moves enemies directly toward the player.
- Updates enemy position and forward direction.

### TargetingModule

- Evaluates current targeting profile.
- Uses the active targeting backend to determine which enemies are inside the current vision mask.
- Writes visible target data to `TargetingRuntimeModel`.

### LockOnModule

- Reads visible targets from `TargetingRuntimeModel`.
- Increases enemy lock values.
- Requests missile launch when an enemy reaches full lock.

### MissileModule

- Creates missiles.
- Updates missile movement.
- Handles missile target tracking.
- Reports missile hit events.

### DamageModule

- Applies missile damage to enemies.
- Checks enemy self-destruction range near the player.
- Applies damage to player.
- Detects player death.

### ShooterViewSyncModule

- Synchronizes model data to view entities.
- Updates transforms, lock bars, HUD values, and vision visuals.
- Does not change gameplay state.

## Extensible Targeting And Vision

Vision must be extensible because future builds may change how targets are detected. Do not hard-code cone checks inside `LockOnModule`.

For the jam build, use a simplified GPU mask approach:

```text
TargetingProfile
  -> VisionShapeInfo / runtime shape entries
  -> GpuMaskTargetingBackend renders VisionMask
  -> Enemy positions are sampled against VisionMask
  -> TargetingRuntimeModel stores visible enemy ids
```

`TargetingModule` owns target detection, and `LockOnModule` only consumes detection results.

### Simplified Targeting Interfaces

```csharp
public interface ITargetingBackend
{
    void RebuildMask(TargetingProfile profile, PlayerModel player);
    void QueryTargets(TargetingQueryInput input, TargetingQueryResult result);
}
```

```csharp
public sealed class TargetingProfile
{
    public List<VisionShapeRuntimeData> Shapes = new();
    public float LockRateMultiplier = 1f;
    public int MaxTargets = 0; // 0 means unlimited.
}
```

```csharp
public enum VisionShapeType
{
    Cone,
    Circle,
    Beam,
    Ring,
    FullScreen
}
```

```csharp
public struct VisionShapeRuntimeData
{
    public VisionShapeType Type;
    public Vector2 LocalOffset;
    public float Range;
    public float Angle;
    public float Width;
    public float InnerRadius;
}
```

`TargetingProfile` is runtime data assembled from imported table info such as `BuildInfo`, `PlayerUpgradeInfo`, and `VisionShapeInfo`. The first implementation can support only `Cone`, while the data structure already leaves room for new shape types.

### GPU Mask Backend

`GpuMaskTargetingBackend` should be the default jam implementation.

Recommended flow:

```text
1. Clear a screen-sized or near-screen-sized RenderTexture mask.
2. Render current vision shapes into the mask.
3. Upload enemy positions to the targeting backend.
4. Sample the mask for each enemy.
5. Write visible enemy ids to `TargetingQueryResult`.
```

Do not read back the full mask texture every frame. If visibility results are needed on CPU, read back only compact enemy visibility results. If the GPU query path is too expensive for the jam schedule, add a temporary `SimpleGeometryTargetingBackend` for MVP, but keep `ITargetingBackend` as the public boundary.

### Targeting Extension Rule

Adding a new vision build should usually require:

- Add a new `VisionShapeType` if needed.
- Teach `GpuMaskTargetingBackend` how to render that shape.
- Add or map the imported `VisionShapeInfo` data into `TargetingProfile`.

It should not require changes to:

- `LockOnModule`
- `MissileModule`
- `DamageModule`
- `EnemyMovementModule`

## Enemy Type Extensibility

Enemy logic should also leave a simple extension point. The first MVP only needs a straight-charging self-destruct enemy, but future enemies may move, explode, or react differently.

Use imported table info for enemy stats. The field list below is illustrative only; the real class should match the CSV schema:

```csharp
public enum EnemyType
{
    Charger,
    Tank,
    Dasher,
    Splitter
}
```

```csharp
public sealed class EnemyInfo
{
    public int Id;
    public EnemyType Type;
    public string EntityAddress;
    public float MaxHp;
    public float MoveSpeed;
    public float ExplodeRange;
    public float ExplodeDamage;
    public float LockMax;
}
```

`EnemyInfo` represents imported CSV data, not a hand-written static config class. The exact fields should follow the table schema. If the implementation needs a field that does not exist yet, ask before adding or assuming it.

Use behavior objects for enemy-specific logic:

```csharp
public interface IEnemyBehavior
{
    void Tick(EnemyModel enemy, ShooterBlackboard blackboard, float deltaTime);
}
```

MVP behavior:

```text
ChargerEnemyBehavior
  -> Move directly toward player
  -> Self-destruct when close enough
```

`EnemyMovementModule` can look up behavior by `EnemyType`:

```text
EnemyModel.Type -> EnemyBehaviorRegistry -> IEnemyBehavior.Tick()
```

This keeps enemy type expansion out of the core loop.

## Future Notes

Replay and multiplayer are not part of this jam scope. The current design only keeps a few habits that make future work less painful:

- Keep model data separate from view entities.
- Keep gameplay module tick order explicit.
- Keep targeting behind `ITargetingBackend`.
- Keep enemy-specific logic behind `IEnemyBehavior`.

## Events

Events should be used for cross-layer notifications, not for every internal data mutation.

Recommended event names:

```text
Shooter.EnemySpawned
Shooter.EnemyLockChanged
Shooter.MissileLaunched
Shooter.MissileHit
Shooter.EnemyExploded
Shooter.PlayerDamaged
Shooter.PlayerDead
Shooter.RunStarted
Shooter.RunEnded
```

Good event consumers:

- UI
- Audio
- VFX
- Camera feedback
- State transitions

Avoid using events where direct model access is simpler and local to the gameplay tick.

## Data Ownership Rules

Use these rules to keep simulation and presentation boundaries clean:

- `ShooterBlackboard` owns runtime data.
- FSM states own lifecycle and flow.
- Gameplay modules own feature/system logic.
- Views own presentation only.
- Build data owns targeting profile selection.
- `TargetingModule` owns visibility detection.
- `LockOnModule` owns lock value progression.
- `MissileModule` owns missile lifecycle.
- `DamageModule` owns health and death decisions.

Avoid:

- Putting core model data inside feature modules.
- Putting long-lived model data inside states.
- Letting entities decide gameplay rules.
- Letting lock-on logic know specific vision shapes.
- Letting UI read scene objects directly when model data is available.
- Using view transforms as authoritative simulation state.
- Scattering random calls across unrelated modules.
- Allowing multiple modules to update the same model field without clear ownership.

## First MVP Scope

The first implementation should include:

- `StartMainLoop`
- Main shooter FSM
- `ShooterBlackboard`
- CSV-imported info usage for design data
- Player model and view
- Enemy model and view
- Missile model and view
- GPU mask targeting backend
- Cone vision shape
- Targeting profile data
- Player movement
- Enemy spawn around screen
- Enemy straight-line movement toward player
- Lock-on accumulation
- Missile launch on full lock
- Missile hit and enemy death
- Enemy self-destruction near player
- Player HP damage
- Basic HUD

Post-MVP extensions:

- Build selection
- Multiple targeting profiles
- Additional vision shapes
- Enemy variants
- Upgrade rewards
- Pause state
- Result screen details
- Object pooling
- GPU enemy visibility sampling

## Design Summary

The final architecture is:

```text
StartMainLoop
  -> Shooter FSM
    -> States control lifecycle and game flow
      -> PlayingState ticks reusable gameplay modules
        -> Modules mutate ShooterBlackboard model data
          -> ViewSync updates GameEntityBase views
```

In MVC terms:

```text
Model      = ShooterBlackboard + pure model classes
View       = GameEntityBase / UIEntity classes
Controller = FSM states + reusable GameModuleBase gameplay systems
```

More precisely:

```text
Simulation   = gameplay modules + ShooterBlackboard
Presentation = GameEntityBase / UIEntity + ViewSync
Flow         = Shooter FSM states
```

This keeps game state centralized, gameplay systems reusable, view code thin, and future targeting/enemy builds easy to extend without overbuilding the jam version.
