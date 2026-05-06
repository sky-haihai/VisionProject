using System.Collections.Generic;
using UnityEngine;
using XiheFramework.Runtime;
using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.Missile {
    /// <summary>
    /// 跟踪导弹对象池（Scene Singleton）。
    /// <para>
    /// 职责：预热 N 个导弹 Prefab 实例，并在收到 <see cref="CombatEvents.EnemyFullyLocked"/>
    /// 事件时从池中取出一枚发射；导弹命中或超时后调用 <see cref="Return"/> 回池。
    /// </para>
    /// <para>
    /// 与 <see cref="LockOn.LockOnProcessor"/> 完全解耦：两者仅通过
    /// <see cref="CombatEvents.EnemyFullyLocked"/> 事件通信，
    /// <c>LockOnProcessor</c> 不持有本组件的任何引用。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-50)] // 在 LockOnProcessor(10) 之前 Awake，确保 Prewarm 完成
    public sealed class MissilePool : MonoBehaviour {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [Header("对象池配置")]
        [SerializeField, Tooltip("导弹 Prefab（需挂有 TrackingMissile + Rigidbody2D + Collider2D Trigger）")]
        private TrackingMissile missilePrefab;

        [SerializeField, Tooltip("启动时预热的导弹数量；设为游戏中同时在飞的最大导弹数 +2 作为缓冲"), Min(1)]
        private int prewarmCount = 10;

        [Header("发射配置")]
        [SerializeField, Tooltip("导弹出膛的世界坐标原点；留空则使用此组件的 Transform（通常为战机 Transform）")]
        private Transform firePoint;

        // ── 内部状态 ──────────────────────────────────────────────────────

        // Queue 保证 FIFO 取用顺序，避免同一枚导弹被反复复用
        private Queue<TrackingMissile> _pool;
        private string _enemyLockedHandlerId;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake() {
            if (missilePrefab == null) {
                Debug.LogError("[MissilePool] missilePrefab 未配置，请在 Inspector 中指定！", this);
                return;
            }

            _pool = new Queue<TrackingMissile>(prewarmCount);
            Prewarm();
        }

        private void OnEnable() {
            _enemyLockedHandlerId = Game.Event.Subscribe(CombatEvents.EnemyFullyLocked, OnEnemyFullyLocked);
        }

        private void OnDisable() {
            if (!string.IsNullOrEmpty(_enemyLockedHandlerId)) {
                Game.Event.Unsubscribe(CombatEvents.EnemyFullyLocked, _enemyLockedHandlerId);
                _enemyLockedHandlerId = null;
            }
        }

        // ── 公开 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 从池中取出一枚导弹并立即发射。
        /// <para>池耗尽时会动态扩容并打出警告日志，正式关卡中此路径不应被触发。</para>
        /// </summary>
        /// <param name="target">跟踪目标。</param>
        /// <returns>已激活的 <see cref="TrackingMissile"/> 实例（已在飞行中）。</returns>
        public TrackingMissile Get(ILockableTarget target) {
            if (missilePrefab == null) return null;

            TrackingMissile missile;
            if (_pool.Count > 0) {
                missile = _pool.Dequeue();
            } else {
                // 池耗尽：动态扩容（记录警告，便于策划调整 prewarmCount）
                Debug.LogWarning("[MissilePool] 对象池已耗尽，动态扩容中。" +
                                 "建议增大 prewarmCount 以避免运行时 Instantiate。", this);
                missile = CreateMissile();
            }

            // 确定发射原点和初始方向（firePoint.up = 战机机头朝向）
            Transform fp = firePoint != null ? firePoint : transform;
            // PrepareToLaunch 必须在 SetActive(true) 之前调用，确保状态就绪再触发 OnEnable
            missile.PrepareToLaunch(target, fp.position, fp.up, this);
            missile.gameObject.SetActive(true);
            return missile;
        }

        /// <summary>
        /// 将导弹回收入池。由 <see cref="TrackingMissile"/> 在命中、目标死亡或超时时调用。
        /// <c>SetActive(false)</c> 会触发 <see cref="TrackingMissile.OnDisable"/>，自动取消事件订阅。
        /// </summary>
        public void Return(TrackingMissile missile) {
            if (missile == null) return;
            missile.gameObject.SetActive(false);
            _pool.Enqueue(missile);
        }

        // ── 私有方法 ──────────────────────────────────────────────────────

        private void Prewarm() {
            for (int i = 0; i < prewarmCount; i++) {
                TrackingMissile missile = CreateMissile();
                missile.gameObject.SetActive(false);
                _pool.Enqueue(missile);
            }
        }

        private TrackingMissile CreateMissile() {
            // 将导弹组织在此 GameObject 下，保持 Hierarchy 整洁
            TrackingMissile missile = Instantiate(missilePrefab, transform);
            return missile;
        }

        /// <summary>
        /// 收到锁定完成事件：从池中取出导弹发射。
        /// 使用 <c>InvokeNow</c> 派发的事件在同帧调用此方法，导弹本帧即开始飞行。
        /// </summary>
        private void OnEnemyFullyLocked(object sender, object e) {
            if (e is not EnemyLockedPayload payload) {
                Debug.LogWarning("[MissilePool] OnEnemyFullyLocked 收到意外载荷类型。", this);
                return;
            }
            Get(payload.Target);
        }
    }
}
