# 模块 A：核心战斗与视界系统 —— 技术实现规格文档

> **文档定位**：本文档是 `ModuleA_核心战斗与视界系统.md` 的工程落地规格。
> AI 应按 **Phase 顺序** 分批生成代码，每个 Phase 完成后必须通过 **验收清单** 后再进入下一阶段。

---

## 一、整体架构决策

### 1.1 架构风格

| 维度 | 决策 | 理由 |
|------|------|------|
| 模块通信 | `Game.Event`（XiheFramework EventModule） | 零耦合，Invoke 延一帧派发，天然防重入 |
| 几何判定 | 策略模式 `IVisionShape` | 支持扩展异形视界，不改核心逻辑 |
| 性能敏感路径 | 避免 LINQ、不在 Update 中 new | 每帧遍历所有敌人×所有视界，GC 敏感 |
| 导弹管理 | 对象池（`MissilePool`） | 高频发射，避免 Instantiate/Destroy 峰值 |
| 数据驱动 | 视界参数走 ScriptableObject | 方便策划配置，不改代码 |
| 敌人接入 | `ILockableTarget` 接口 | 模块 A 不依赖具体 Enemy 实现 |

### 1.2 目录结构

```
Assets/Scripts/
└── Combat/
    ├── Contracts/          # Phase 0 ── 接口与事件常量（零依赖层）
    │   ├── ILockableTarget.cs
    │   └── CombatEvents.cs
    ├── Player/             # Phase 1 ── 玩家控制
    │   └── PlayerController.cs
    ├── Vision/             # Phase 2 ── 视界几何系统
    │   ├── IVisionShape.cs
    │   ├── VisionLayerData.cs       (ScriptableObject)
    │   ├── VisionLayer.cs           (MonoBehaviour)
    │   ├── VisionRegistry.cs        (运行时注册表，单例服务)
    │   └── Shapes/
    │       ├── CircleVisionShape.cs
    │       ├── SectorVisionShape.cs
    │       ├── RectVisionShape.cs
    │       └── RingVisionShape.cs
    ├── LockOn/             # Phase 3 ── 锁定进度系统
    │   ├── LockOnProgressEntry.cs   (struct)
    │   └── LockOnProcessor.cs       (MonoBehaviour，核心每帧计算)
    ├── Missile/            # Phase 4 ── 跟踪导弹
    │   ├── TrackingMissile.cs
    │   └── MissilePool.cs
    └── Enemy/              # Phase 5 ── 敌人基础组件（实现 ILockableTarget）
        ├── EnemyBase.cs
        └── EnemyHealth.cs
```

### 1.3 事件总线契约（所有跨模块通信）

```
CombatEvents.OnMissileFired          载荷: MissileFiredPayload
CombatEvents.OnEnemyFullyLocked      载荷: EnemyLockedPayload   (内部，LockOnProcessor → MissilePool)
CombatEvents.OnEnemyDied             载荷: EnemyDiedPayload      (EnemyHealth 发出)
CombatEvents.OnLockProgressChanged   载荷: LockProgressPayload   (给 HUD 订阅)
```

### 1.4 执行时序（每帧）

```
PlayerController.Update()
  └─ 移动 + 转向插值

LockOnProcessor.Update()
  ├─ 1. 遍历所有存活 ILockableTarget
  ├─ 2. 查询 VisionRegistry 获取所有激活 VisionLayer
  ├─ 3. 对每个敌人，累加其命中的视界的 lockSpeed → 计算 deltaProgress
  ├─ 4. 更新 LockOnProgressEntry（进度 ± 增量）
  ├─ 5. 若 progress ≥ concealmentValue → 触发发射 → 重置进度
  └─ 6. 发布 OnLockProgressChanged（HUD 订阅）

TrackingMissile.Update()
  ├─ 阶段 A（抛物线）：持续 launchDuration 秒，偏移 Y 轴曲线
  └─ 阶段 B（追踪）：朝目标旋转，maxTurnSpeed 限制，Rigidbody.velocity
```

---

## 二、Phase 0：基础契约层

> **目标**：建立零依赖的接口与常量层，所有后续 Phase 依赖此层。

### 文件清单

| 文件 | 类型 | 职责 |
|------|------|------|
| `Contracts/ILockableTarget.cs` | interface | 敌人向锁定系统暴露的最小接口 |
| `Contracts/CombatEvents.cs` | static class | 集中定义所有事件名字符串常量 |
| `Contracts/Payloads.cs` | 若干 struct | 所有事件载荷的 DTO |

### 详细规格

**`ILockableTarget`**

```csharp
// 需要暴露的属性与方法：
float ConcealmentValue { get; }     // 隐蔽值（锁定阈值）
bool  IsAlive          { get; }     // 是否存活（导弹飞行中检查）
Transform BodyTransform { get; }    // 世界坐标（几何判定用）
void  OnMissileHit(float damage);   // 受到导弹伤害
```

**`CombatEvents` 常量规范**
- 命名格式：`"Combat.{动作}"` 例如 `"Combat.MissileFired"`
- 每条常量需 XML 注释说明载荷类型

**Payloads（全部 struct，避免 GC）**

```
MissileFiredPayload    { ILockableTarget Target; Vector3 FirePosition }
EnemyLockedPayload     { ILockableTarget Target }
EnemyDiedPayload       { ILockableTarget Target }
LockProgressPayload    { ILockableTarget Target; float Progress; float MaxProgress }
```

### Phase 0 验收清单

- [ ] `ILockableTarget` 编译通过，无 UnityEngine 依赖
- [ ] `CombatEvents` 所有常量有 XML 文档注释
- [ ] Payload struct 均为 `readonly struct`（防止意外修改）
- [ ] `Assets/Scripts/Combat/Contracts/` 目录结构正确

---

## 三、Phase 1：玩家控制器

> **目标**：实现 WASD 全向移动 + 鼠标控制机头朝向（有转速上限）。

### 文件清单

| 文件 | 组件 |
|------|------|
| `Player/PlayerController.cs` | MonoBehaviour，挂载在玩家战机 GameObject |

### 详细规格

**Inspector 暴露参数**（全部 `[SerializeField]`）

| 字段 | 类型 | 说明 | 参考默认值 |
|------|------|------|-----------|
| `moveSpeed` | float | 移动速度（单位/秒） | 8f |
| `maxTurnSpeed` | float | 最大转向速度（°/秒） | 360f |
| `mainCamera` | Camera | 用于鼠标射线，若为 null 则 `Camera.main` | null |

**移动逻辑**
- 读取 `Input.GetAxisRaw("Horizontal/Vertical")`，归一化后乘以 `moveSpeed * Time.deltaTime`
- 使用 `Rigidbody2D.MovePosition`（若为 2D 物理）或直接修改 `transform.position`
- 注意：此游戏为俯视角 XY 平面，Z 轴始终为 0

**旋转逻辑**
- 将鼠标屏幕坐标转为世界坐标（`Camera.ScreenToWorldPoint`）
- 计算目标朝向角度
- 使用 `Mathf.MoveTowardsAngle(currentAngle, targetAngle, maxTurnSpeed * Time.deltaTime)` 插值
- 应用到 `transform.rotation`

**组件依赖**
- `[RequireComponent(typeof(Rigidbody2D))]`

### Phase 1 验收清单

- [ ] WASD 移动方向正确，移动速度与 `moveSpeed` 一致
- [ ] 鼠标偏向某方向时，机头缓慢转向目标角度（不会瞬间完成）
- [ ] `maxTurnSpeed = 0` 时机头完全无法旋转
- [ ] 移动时不产生额外旋转（平移与旋转独立）
- [ ] 无 GC Alloc（Profile 检验 `PlayerController.Update`）

---

## 四、Phase 2：视界几何系统

> **目标**：实现可扩展的多形状视界，支持 IsPointInside 判定，并提供运行时注册表。

### 文件清单

| 文件 | 类型 |
|------|------|
| `Vision/IVisionShape.cs` | interface |
| `Vision/VisionLayerData.cs` | ScriptableObject（视界配置） |
| `Vision/VisionLayer.cs` | MonoBehaviour（挂在战机子物体上） |
| `Vision/VisionRegistry.cs` | 单例 MonoBehaviour（场景级服务） |
| `Vision/Shapes/CircleVisionShape.cs` | class : IVisionShape |
| `Vision/Shapes/SectorVisionShape.cs` | class : IVisionShape |
| `Vision/Shapes/RectVisionShape.cs` | class : IVisionShape |
| `Vision/Shapes/RingVisionShape.cs` | class : IVisionShape |

### 详细规格

**`IVisionShape`**

```csharp
// 核心 API
bool IsPointInside(Vector2 worldPoint, Vector2 origin, float forwardAngleDeg);
void DrawGizmos(Vector2 origin, float forwardAngleDeg);   // 编辑器可视化
```

- `origin`：视界中心（通常为战机位置）
- `forwardAngleDeg`：战机当前朝向角度（扇形/矩形判定需要）

**`VisionLayerData`（ScriptableObject）**

| 字段 | 类型 | 说明 |
|------|------|------|
| `shapeType` | enum VisionShapeType | Circle/Sector/Rect/Ring |
| `lockOnSpeed` | float | 每秒锁定进度增量 |
| `duration` | float | ≤ 0 表示常驻，> 0 表示有时限（秒）|
| `circleRadius` | float | 圆形参数 |
| `sectorAngle` | float | 扇形张角（°） |
| `sectorRadius` | float | 扇形半径 |
| `rectWidth` | float | 矩形宽度 |
| `rectLength` | float | 矩形长度（前向） |
| `ringInnerRadius` | float | 环形内半径 |
| `ringOuterRadius` | float | 环形外半径 |

> ScriptableObject 字段用 `[Header]` 分组展示，不在同一形状类型生效的字段用 `[Tooltip]` 注明"仅当 shapeType = XX 时生效"。

**各形状判定算法**

| 形状 | 判定要点 |
|------|----------|
| Circle | `distance ≤ radius` |
| Sector | `distance ≤ radius` 且 `Mathf.Abs(Mathf.DeltaAngle(pointAngle, forwardAngle)) ≤ halfAngle` |
| Rect | 将目标点转到战机本地空间，判定 `[-w/2, w/2] × [0, length]`（矩形在前方）|
| Ring | `innerRadius ≤ distance ≤ outerRadius` |

**`VisionRegistry`**（场景单例）

```csharp
// 对外 API
void Register(VisionLayer layer);
void Unregister(VisionLayer layer);
// 返回命中指定点的所有激活视界，并聚合锁定速度总和
// 使用预分配 List<VisionLayer> 缓存，避免 GC
float GetTotalLockSpeedAt(Vector2 worldPoint);
// 返回所有激活视界（供调试）
IReadOnlyList<VisionLayer> GetAllLayers();
```

**`VisionLayer`（MonoBehaviour）**

- 在 `OnEnable` 时向 `VisionRegistry` 注册，`OnDisable` 时注销
- 若 `data.duration > 0`，自计时，到期后 `gameObject.SetActive(false)`
- 跟随 `ownerTransform`（默认为父对象，可配置）

### Phase 2 验收清单

- [ ] 编辑器中 VisionLayer Gizmos 正确绘制四种形状
- [ ] 对每种形状，手动测试边界点（内、外、边界）IsPointInside 返回值正确
- [ ] VisionRegistry 在运行时能正确追踪 Register/Unregister（OnEnable/Disable）
- [ ] `GetTotalLockSpeedAt` 对单层返回该层 lockSpeed，对两层重叠点返回两者之和
- [ ] 有时限的 VisionLayer 到期后自动禁用
- [ ] 全程无 GC Alloc（GetTotalLockSpeedAt 内部不产生集合分配）

---

## 五、Phase 3：锁定进度系统

> **目标**：每帧为所有存活敌人计算锁定进度，达阈值触发发射事件，脱离视界后自动衰减。

### 文件清单

| 文件 | 类型 |
|------|------|
| `LockOn/LockOnProgressEntry.cs` | struct（per-enemy 数据） |
| `LockOn/LockOnProcessor.cs` | MonoBehaviour |

### 详细规格

**`LockOnProgressEntry`（struct）**

```csharp
struct LockOnProgressEntry {
    public ILockableTarget Target;
    public float           CurrentProgress;
    public bool            IsMissilePending;   // 已发射但敌人未死亡，抑制 HUD 显示
}
```

**`LockOnProcessor`**

Inspector 参数：

| 字段 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `decayRatePerSecond` | float | 脱离视界后每秒衰减的**百分比**（0~1） | 0.2f（20%/s）|
| `lockProgressBroadcastInterval` | float | HUD 进度事件的发送间隔（秒），避免每帧都发 | 0.05f |

核心逻辑伪代码：

```
Update():
  // 1. 清理已死亡敌人的 Entry
  foreach entry in _progressMap where !entry.Target.IsAlive → remove

  // 2. 向 ILockOnTargetRegistry 查询所有存活敌人（Phase 5 提供，或直接用静态列表）
  foreach target in EnemyRegistry.Alive:
    if !_progressMap.ContainsKey(target) → _progressMap[target] = new Entry(target)
    
    // 3. 判定该敌人命中哪些视界
    float totalSpeed = VisionRegistry.Instance.GetTotalLockSpeedAt(target.BodyTransform.position)
    
    // 4. 更新进度
    ref entry = _progressMap[target]
    if totalSpeed > 0:
      entry.CurrentProgress += totalSpeed * Time.deltaTime
      entry.CurrentProgress = Mathf.Min(entry.CurrentProgress, entry.Target.ConcealmentValue)
    else:
      // 衰减：每秒减少 decayRatePerSecond 比例
      entry.CurrentProgress -= entry.CurrentProgress * decayRatePerSecond * Time.deltaTime
      entry.CurrentProgress = Mathf.Max(entry.CurrentProgress, 0f)
    
    // 5. 触发阈值
    if !entry.IsMissilePending && entry.CurrentProgress >= target.ConcealmentValue:
      Game.Event.InvokeNow(CombatEvents.OnEnemyFullyLocked, new EnemyLockedPayload { Target = target })
      entry.CurrentProgress = 0f
      // Step 4：判断这枚导弹是否足以击杀（target.CurrentHealth <= 100）
      entry.IsMissilePending = (target.CurrentHealth <= MissileDamage)
  
  // 6. 定期广播进度给 HUD
  _broadcastTimer += Time.deltaTime
  if _broadcastTimer >= lockProgressBroadcastInterval:
    _broadcastTimer = 0
    foreach entry → Invoke(CombatEvents.OnLockProgressChanged, payload)
```

> **关键**：`_progressMap` 使用 `Dictionary<ILockableTarget, LockOnProgressEntry>`，key 为接口引用，无需字符串键。

**订阅敌人死亡事件**（在 OnEnable/OnDisable 中订阅/注销 `CombatEvents.OnEnemyDied`）：
当收到死亡事件时，将对应 Entry 的 `IsMissilePending = false` 并从字典移除。

### Phase 3 验收清单

- [ ] 敌人进入视界后锁定进度开始增加（速率 = VisionLayer.lockOnSpeed）
- [ ] 两层视界重叠时，进度增加速率 = 两层 lockOnSpeed 之和（实测）
- [ ] 敌人离开视界后，进度按 `decayRatePerSecond` 百分比衰减
- [ ] 进度达到 `ConcealmentValue` 时，`CombatEvents.OnEnemyFullyLocked` 被正确触发
- [ ] 触发后进度立即重置为 0
- [ ] 敌人死亡后，其 Entry 被从字典中清除
- [ ] `IsMissilePending = true` 时 HUD 不再显示该敌人的锁定条（留给 Phase 6 验证 UI）
- [ ] 无 GC Alloc（字典操作、进度更新均无装箱/拆箱）

---

## 六、Phase 4：跟踪导弹

> **目标**：实现具有物理感的跟踪导弹，对象池管理，100 点固定伤害，目标死亡自毁。

### 文件清单

| 文件 | 类型 |
|------|------|
| `Missile/TrackingMissile.cs` | MonoBehaviour（挂在导弹 Prefab 上）|
| `Missile/MissilePool.cs` | MonoBehaviour（场景单例，对象池）|

### 详细规格

**`TrackingMissile`**

Inspector 参数：

| 字段 | 类型 | 说明 | 默认值 |
|------|------|------|--------|
| `launchSpeed` | float | 初始速度（抛物线阶段） | 5f |
| `trackingSpeed` | float | 追踪阶段飞行速度 | 12f |
| `maxTurnSpeedDeg` | float | 追踪阶段最大转向速度（°/s） | 180f |
| `launchDuration` | float | 抛物线阶段持续时间（s） | 0.4f |
| `launchArcHeight` | float | 抛物线弧高（世界单位） | 2f |
| `damage` | float | 伤害量（固定 100） | 100f |

飞行状态机（两阶段）：

```
Phase A（抛物线，持续 launchDuration 秒）:
  t = elapsedTime / launchDuration  // [0, 1]
  arcOffset = sin(t * PI) * launchArcHeight  // 垂直于初始飞行方向的偏移
  position += forward * launchSpeed * dt + perpendicular * arcOffset

Phase B（追踪）:
  if target == null || !target.IsAlive → ReturnToPool()
  dirToTarget = (target.BodyTransform.position - position).normalized
  currentDir = Quaternion.RotateTowards(currentRot, targetRot, maxTurnSpeedDeg * dt)
  Rigidbody2D.velocity = currentDir * forward * trackingSpeed
```

命中处理：
- `OnTriggerEnter2D` 检测碰撞层（EnemyLayer）
- 调用 `target.OnMissileHit(damage)`
- 发布 `CombatEvents.OnMissileFired`（供 VFX 订阅）
- `ReturnToPool()`

目标死亡监听：
- 订阅 `CombatEvents.OnEnemyDied`
- 若 payload.Target == this.target → `ReturnToPool()`

**`MissilePool`**

- 使用 `Queue<TrackingMissile>` 作为空闲池，初始化时预热 N 个（Inspector 配置）
- API：`TrackingMissile Get(ILockableTarget target, Vector3 firePosition)`
- 订阅 `CombatEvents.OnEnemyFullyLocked`，收到后从池中取导弹并发射
- 对象归还时 `gameObject.SetActive(false)` 并加入队列

### Phase 4 验收清单

- [ ] 导弹发射后有明显的抛物线弧形轨迹（0.4s 内）
- [ ] 抛物线结束后进入追踪阶段，朝目标旋转
- [ ] `maxTurnSpeedDeg` 限制生效（导弹无法做 180° 瞬间掉头）
- [ ] 命中敌人后调用 `OnMissileHit(100f)` 并回收
- [ ] 目标在导弹飞行途中死亡，导弹立即消失（回池）
- [ ] 连续触发 10 次发射，Profile 中 Instantiate 调用次数为 0（全走对象池）

---

## 七、Phase 5：敌人基础组件

> **目标**：实现 `ILockableTarget` 的最小可运行敌人，提供 EnemyRegistry 供 LockOnProcessor 查询。

### 文件清单

| 文件 | 类型 |
|------|------|
| `Enemy/EnemyBase.cs` | MonoBehaviour : ILockableTarget |
| `Enemy/EnemyHealth.cs` | MonoBehaviour（血量组件，可复用）|
| `Enemy/EnemyRegistry.cs` | 静态注册表（纯 C#，无 MonoBehaviour）|

### 详细规格

**`EnemyBase : MonoBehaviour, ILockableTarget`**

Inspector 参数：

| 字段 | 类型 | 说明 |
|------|------|------|
| `concealmentValue` | float | 隐蔽值（锁定阈值）|
| `health` | EnemyHealth | 引用 EnemyHealth 组件 |

接口实现：
- `ConcealmentValue` → 返回 `concealmentValue`
- `IsAlive` → 返回 `health.CurrentHealth > 0`
- `BodyTransform` → 返回 `transform`
- `OnMissileHit(damage)` → 调用 `health.TakeDamage(damage)`

生命周期：
- `OnEnable` → `EnemyRegistry.Register(this)`
- `OnDisable` → `EnemyRegistry.Unregister(this)`

**`EnemyHealth`**

- 维护 `CurrentHealth`（float）和 `MaxHealth`（float）
- `TakeDamage(float amount)`：扣血，若 ≤ 0 触发死亡
- 死亡时：发布 `CombatEvents.OnEnemyDied`（payload = `new EnemyDiedPayload { Target = GetComponent<ILockableTarget>() }`），然后 `gameObject.SetActive(false)` 或销毁

**`EnemyRegistry`（静态）**

```csharp
static readonly List<ILockableTarget> _alive = new();
static IReadOnlyList<ILockableTarget> Alive => _alive;
static void Register(ILockableTarget t) → _alive.Add(t)
static void Unregister(ILockableTarget t) → _alive.Remove(t)
```

### Phase 5 验收清单

- [ ] EnemyBase 实现 `ILockableTarget` 所有属性/方法，无编译错误
- [ ] `EnemyRegistry.Alive` 在运行时正确反映场景中存活敌人数量
- [ ] 导弹命中后 `CurrentHealth` 正确扣减
- [ ] 血量归零后发布 `CombatEvents.OnEnemyDied` 事件
- [ ] 死亡后 `IsAlive` 返回 false，`EnemyRegistry` 中已移除

---

## 八、Phase 6：系统整合与 PlayMode 测试

> **目标**：完整闭环跑通核心游戏循环，添加调试 HUD，出具集成测试清单。

### 需要完成的工作

**Prefab 配置**
1. `PlayerShip` Prefab：挂 `PlayerController`、`VisionRegistry`、`LockOnProcessor`、`MissilePool`（或作为场景单例）
2. `VisionLayer_Circle` Prefab：挂 `VisionLayer`，绑定 `VisionLayerData_Circle` SO
3. `Missile` Prefab：挂 `TrackingMissile`、`CircleCollider2D`（Trigger）、Rigidbody2D
4. `Enemy_Basic` Prefab：挂 `EnemyBase`、`EnemyHealth`、碰撞体

**调试 HUD（仅供开发期使用，可用 OnGUI 快速实现）**

订阅 `CombatEvents.OnLockProgressChanged`，显示：
```
[Enemy_xx] ████░░░░ 65% (0.65 / 1.00)
```

**集成测试场景 `Scene_CombatTest`**

场景构成：
- 1 个 PlayerShip
- 1 个圆形 VisionLayer（挂在战机子物体，常驻，lockOnSpeed = 1.0）
- 3 个 Enemy_Basic（concealmentValue 分别为 1.0、2.0、3.0）
- MissilePool（预热 10 个）

### Phase 6 验收清单（完整闭环）

- [ ] 玩家移动后视界跟随战机位置更新
- [ ] 将战机移动到敌人旁边，锁定进度条开始出现
- [ ] 进度条满后，导弹自动发射飞向目标
- [ ] 导弹命中后，目标血量减少（或死亡）
- [ ] 目标死亡后，不再出现锁定进度条
- [ ] 用两个 VisionLayer 叠加覆盖同一敌人，进度积累速度约为单层的 2 倍
- [ ] 将战机快速移开，进度条开始衰减
- [ ] 连续触发发射 20 次，无 MissingReferenceException，无 NullReferenceException
- [ ] 在 Profiler 中采样 10 秒 Play Mode，GC.Alloc 峰值 < 1KB/frame（理想 0B）

---

## 九、跨 Phase 依赖关系图

```
Phase 0 (Contracts)
    └─→ Phase 1 (Player)         [无直接依赖 Phase 0，但事件常量依赖]
    └─→ Phase 2 (Vision)         [VisionLayerData, IVisionShape]
    └─→ Phase 3 (LockOn)         [ILockableTarget, CombatEvents, VisionRegistry]
            └─→ Phase 4 (Missile)    [CombatEvents.OnEnemyFullyLocked]
            └─→ Phase 5 (Enemy)      [ILockableTarget, EnemyRegistry, CombatEvents.OnEnemyDied]
                    └─→ Phase 6 (Integration)
```

> **原则**：高 Phase 依赖低 Phase，绝不反向依赖。Phase 2 与 Phase 1 可并行开发。

---

## 十、关键风险与注意事项

| 风险 | 说明 | 缓解措施 |
|------|------|----------|
| 每帧 O(E×V) 遍历性能 | 敌人数×视界数，小规模可接受 | 敌人 > 100 时考虑空间哈希或 SpatialQuery 优化 |
| LockOnProcessor 与 MissilePool 耦合 | 通过 CombatEvents 解耦，不直接引用 | 严禁 LockOnProcessor 持有 MissilePool 引用 |
| 导弹追踪卡死（目标完全静止在导弹正后方） | 极端情况，转向半径无法到达 | 增加超时自毁（maxLifetime = 10s）|
| `IsMissilePending` 状态漏清 | 导弹回池后未通知 LockOnProcessor | Phase 4 验收时专项测试：目标在多导弹飞行中死亡 |
| EnemyRegistry 线程安全 | LockOnProcessor 在主线程，无并发写，暂无问题 | 若后续 Job System 介入，改为 NativeArray |

---

*文档版本：v1.0 | 对应需求版本：《视界边缘》2.0 | 最后更新：2026-05-06*
