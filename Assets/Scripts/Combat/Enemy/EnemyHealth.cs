using UnityEngine;
using XiheFramework.Runtime;
using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.Enemy {
    /// <summary>
    /// 敌人血量组件。负责维护 HP 状态，处理伤害扣减，并在死亡时广播事件。
    /// <para>
    /// 设计为独立的可复用组件：血量逻辑与敌人的移动/AI 逻辑完全分离，
    /// 符合单一职责原则。其他系统（如护盾、BUFF）可通过在 <c>TakeDamage</c>
    /// 调用链前插入来修改最终伤害，而不需要修改此类。
    /// </para>
    /// <para>
    /// 死亡时序：广播 <see cref="CombatEvents.EnemyDied"/> → <c>gameObject.SetActive(false)</c>。
    /// <c>SetActive(false)</c> 会触发 <see cref="EnemyBase.OnDisable"/>，
    /// 自动将目标从 <see cref="EnemyRegistry"/> 中移除，无需手动清理。
    /// </para>
    /// </summary>
    public sealed class EnemyHealth : MonoBehaviour {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [SerializeField, Tooltip("最大生命值"), Min(1f)]
        private float maxHealth = 100f;

        // ── 运行时状态 ────────────────────────────────────────────────────

        /// <summary>当前生命值，范围 <c>[0, MaxHealth]</c>。</summary>
        public float CurrentHealth { get; private set; }

        /// <summary>最大生命值。</summary>
        public float MaxHealth => maxHealth;

        /// <summary>生命值归一化比例 <c>[0, 1]</c>，供 HUD 血条读取。</summary>
        public float NormalizedHealth => maxHealth > 0f ? CurrentHealth / maxHealth : 0f;

        // ILockableTarget 引用缓存：死亡时作为事件载荷，避免 GetComponent 产生 GC
        private ILockableTarget _lockableTarget;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake() {
            CurrentHealth    = maxHealth;
            _lockableTarget  = GetComponent<ILockableTarget>();

            if (_lockableTarget == null) {
                Debug.LogWarning("[EnemyHealth] 同 GameObject 上未找到 ILockableTarget 实现。" +
                                 "死亡事件载荷将为 null，请确保 EnemyBase 已挂载。", this);
            }
        }

        // ── 公开 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 对目标施加伤害。生命值归零后触发死亡流程（仅触发一次）。
        /// </summary>
        /// <param name="amount">伤害量（正数），传入负值时视为 0 处理。</param>
        public void TakeDamage(float amount) {
            if (CurrentHealth <= 0f) return; // 已死亡，防止多次触发死亡逻辑

            CurrentHealth -= Mathf.Max(amount, 0f);

            if (CurrentHealth <= 0f) {
                CurrentHealth = 0f;
                OnDeath();
            }
        }

        /// <summary>
        /// 直接恢复生命值（上限为 <see cref="MaxHealth"/>）。
        /// 供技能/道具系统调用。
        /// </summary>
        /// <param name="amount">回复量（正数）。</param>
        public void Heal(float amount) {
            if (CurrentHealth <= 0f) return;
            CurrentHealth = Mathf.Min(CurrentHealth + Mathf.Max(amount, 0f), maxHealth);
        }

        /// <summary>重置血量至满值，用于对象池复用时重置状态。</summary>
        public void ResetHealth() {
            CurrentHealth = maxHealth;
        }

        // ── 私有方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 死亡处理：广播死亡事件，然后禁用 GameObject。
        /// 使用 <c>InvokeNow</c> 确保同帧内所有订阅者（导弹、锁定系统）立即响应，
        /// 避免死亡后又被"锁定一帧"或"追踪一帧"的视觉错误。
        /// </summary>
        private void OnDeath() {
            Game.Event.InvokeNow(CombatEvents.EnemyDied,
                new EnemyDiedPayload(_lockableTarget));

            // SetActive(false) 触发 EnemyBase.OnDisable → EnemyRegistry.Unregister
            gameObject.SetActive(false);
        }
    }
}
