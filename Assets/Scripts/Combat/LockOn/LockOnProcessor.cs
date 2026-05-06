using System.Collections.Generic;
using UnityEngine;
using XiheFramework.Runtime;
using VisionProject.Combat.Contracts;
using VisionProject.Combat.Enemy;
using VisionProject.Combat.Vision;

namespace VisionProject.Combat.LockOn {
    /// <summary>
    /// 锁定进度处理器（核心每帧计算）。
    /// <para>
    /// 职责：每帧遍历所有存活目标，根据其是否在视界内累加/衰减锁定进度；
    /// 进度达阈值时立即触发 <see cref="CombatEvents.EnemyFullyLocked"/>，进度重置为 0；
    /// 定期向 HUD 广播 <see cref="CombatEvents.LockProgressChanged"/>（节流，非每帧）。
    /// </para>
    /// <para>
    /// 性能原则：O(E × V) 遍历，无 LINQ，无临时集合分配，字典操作不装箱
    /// （<see cref="ILockableTarget"/> 实现者为引用类型 <see cref="MonoBehaviour"/>）。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(10)] // 晚于 VisionRegistry(-100)，确保 Registry 已初始化
    public sealed class LockOnProcessor : MonoBehaviour {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [SerializeField, Range(0f, 1f),
         Tooltip("脱离视界后每秒衰减当前进度的比例（0~1）。" +
                 "0.2 = 每秒减少当前进度的 20%（指数衰减）。")]
        private float decayRatePerSecond = 0.2f;

        [SerializeField, Min(0.01f),
         Tooltip("HUD 进度事件的广播间隔（秒）。" +
                 "避免每帧 Invoke 产生不必要的事件队列压力，默认 50ms（20 次/秒）。")]
        private float lockProgressBroadcastInterval = 0.05f;

        [SerializeField,
         Tooltip("单枚导弹的固定伤害量。" +
                 "用于判断一枚导弹是否足以击杀目标（target.CurrentHealth ≤ 此值），" +
                 "以决定是否设置 IsMissilePending 以抑制 HUD 重复显示锁定条。")]
        private float missileDamage = 100f;

        // ── 内部状态 ──────────────────────────────────────────────────────

        // 初始容量 32：正常局内不会超过此数，避免首次动态扩容
        private readonly Dictionary<ILockableTarget, LockOnProgressEntry> _progressMap = new(32);

        // 复用列表：每帧收集待移除的 Key，避免在 foreach 字典时修改集合（InvalidOperationException）
        private readonly List<ILockableTarget> _keysToRemove = new(8);

        private float  _broadcastTimer;
        private string _enemyDiedHandlerId;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void OnEnable() {
            // 订阅敌人死亡事件，在事件触发时立即清理对应 Entry，早于下帧 Update 的安全网清理
            _enemyDiedHandlerId = Game.Event.Subscribe(CombatEvents.EnemyDied, OnEnemyDied);
        }

        private void OnDisable() {
            if (!string.IsNullOrEmpty(_enemyDiedHandlerId)) {
                Game.Event.Unsubscribe(CombatEvents.EnemyDied, _enemyDiedHandlerId);
                _enemyDiedHandlerId = null;
            }
            // 清空进度表，防止场景切换后残留过期引用
            _progressMap.Clear();
            _broadcastTimer = 0f;
        }

        // ── 主循环 ────────────────────────────────────────────────────────

        private void Update() {
            CleanDeadEntries();
            UpdateLockProgress();
            ThrottledBroadcast();
        }

        // ── 私有方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 步骤 1：安全网——清除进度表中已死亡目标的 Entry。
        /// 正常流程由 <see cref="OnEnemyDied"/> 即时处理；此处处理"死亡事件丢失"的边缘情况。
        /// </summary>
        private void CleanDeadEntries() {
            _keysToRemove.Clear();
            foreach (KeyValuePair<ILockableTarget, LockOnProgressEntry> pair in _progressMap) {
                if (!pair.Key.IsAlive) {
                    _keysToRemove.Add(pair.Key);
                }
            }
            for (int i = 0; i < _keysToRemove.Count; i++) {
                _progressMap.Remove(_keysToRemove[i]);
            }
        }

        /// <summary>
        /// 步骤 2-5：遍历所有存活目标，累加或衰减进度，并在满足阈值时触发锁定事件。
        /// </summary>
        private void UpdateLockProgress() {
            IReadOnlyList<ILockableTarget> alive = EnemyRegistry.Alive;
            VisionRegistry registry = VisionRegistry.Instance;
            float dt = Time.deltaTime;

            for (int i = 0, n = alive.Count; i < n; i++) {
                ILockableTarget target = alive[i];
                if (target == null || !target.IsAlive) continue;

                // 确保进度条目存在（首次见到此目标时创建）
                if (!_progressMap.TryGetValue(target, out LockOnProgressEntry entry)) {
                    entry = new LockOnProgressEntry(target);
                }

                // 步骤 3：查询该目标世界坐标上所有覆盖视界的锁定速度之和（零 GC）
                float totalSpeed = registry != null
                    ? registry.GetTotalLockSpeedAt(target.BodyTransform.position)
                    : 0f;

                // 步骤 4：更新进度
                if (totalSpeed > 0f) {
                    entry.CurrentProgress += totalSpeed * dt;
                    entry.CurrentProgress  = Mathf.Min(entry.CurrentProgress, target.ConcealmentValue);
                } else {
                    // 指数式衰减：每帧减少当前值的 decayRatePerSecond * dt 比例
                    // 效果：进度越高衰减越快，最终趋近 0 而非线性归零
                    entry.CurrentProgress -= entry.CurrentProgress * decayRatePerSecond * dt;
                    entry.CurrentProgress  = Mathf.Max(entry.CurrentProgress, 0f);
                }

                // 步骤 5：阈值检测 → 触发发射事件，重置进度
                // IsMissilePending 为 true 时跳过（目标已有导弹在飞，等待落地结果）
                if (!entry.IsMissilePending &&
                    entry.CurrentProgress >= target.ConcealmentValue) {
                    // InvokeNow：同帧立即通知 MissilePool 取出并发射导弹
                    Game.Event.InvokeNow(CombatEvents.EnemyFullyLocked,
                        new EnemyLockedPayload(target));

                    entry.CurrentProgress = 0f;
                    // 判断发射的导弹是否足以击杀目标：若是，抑制 HUD 后续显示（避免"死了还在锁"）
                    entry.IsMissilePending = target.CurrentHealth <= missileDamage;
                }

                // struct 值类型：修改后必须写回字典（直接赋值，无装箱）
                _progressMap[target] = entry;
            }
        }

        /// <summary>
        /// 步骤 6：节流广播——按 <see cref="lockProgressBroadcastInterval"/> 间隔发送 HUD 进度事件。
        /// 跳过 <see cref="LockOnProgressEntry.IsMissilePending"/> 为 true 的目标。
        /// </summary>
        private void ThrottledBroadcast() {
            _broadcastTimer += Time.deltaTime;
            if (_broadcastTimer < lockProgressBroadcastInterval) return;

            _broadcastTimer = 0f;

            // Invoke（入队，下帧派发）：HUD 不需要同帧响应，避免在遍历中触发订阅者副作用
            foreach (KeyValuePair<ILockableTarget, LockOnProgressEntry> pair in _progressMap) {
                if (pair.Value.IsMissilePending) continue;

                ILockableTarget target = pair.Key;
                Game.Event.Invoke(CombatEvents.LockProgressChanged,
                    new LockProgressPayload(target, pair.Value.CurrentProgress, target.ConcealmentValue));
            }
        }

        /// <summary>
        /// 敌人死亡事件处理器。即时从进度表中移除对应 Entry，
        /// 确保下帧 <see cref="UpdateLockProgress"/> 不再处理已死亡目标。
        /// </summary>
        private void OnEnemyDied(object sender, object e) {
            if (e is not EnemyDiedPayload payload) {
                Debug.LogWarning("[LockOnProcessor] OnEnemyDied 收到意外载荷类型。", this);
                return;
            }
            _progressMap.Remove(payload.Target);
        }
    }
}
