using UnityEngine;
using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.Enemy {
    /// <summary>
    /// 敌人基础组件，实现 <see cref="ILockableTarget"/> 接口，
    /// 是视界系统、锁定系统与导弹系统感知敌人的唯一入口。
    /// <para>
    /// 模块 A 的任何系统均不依赖此类的具体类型，只通过 <see cref="ILockableTarget"/> 接口交互，
    /// 满足依赖倒置原则。不同类型的敌人（精英、Boss）可继承此类或另行实现接口。
    /// </para>
    /// <para>
    /// 生命周期：
    /// <list type="bullet">
    ///   <item><c>OnEnable</c> → <see cref="EnemyRegistry.Register"/>：进入场景/从对象池激活后加入存活列表。</item>
    ///   <item><c>OnDisable</c> → <see cref="EnemyRegistry.Unregister"/>：死亡（由 <see cref="EnemyHealth"/> 触发
    ///         <c>SetActive(false)</c>）或被手动禁用时从列表移除。</item>
    /// </list>
    /// </para>
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyBase : MonoBehaviour, ILockableTarget {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [SerializeField, Tooltip("隐蔽值：锁定进度需要累积到此值才会触发导弹发射；值越大越难被锁定"), Min(0.1f)]
        private float concealmentValue = 1f;

        [SerializeField, Tooltip("血量组件引用；留空则在 Awake 中自动从同 GameObject 获取")]
        private EnemyHealth health;

        // ── 生命周期 ──────────────────────────────────────────────────────

        protected virtual void Awake() {
            if (health == null) {
                health = GetComponent<EnemyHealth>();
            }
        }

        protected virtual void OnEnable() {
            EnemyRegistry.Register(this);
        }

        protected virtual void OnDisable() {
            EnemyRegistry.Unregister(this);
        }

        // ── ILockableTarget 实现 ──────────────────────────────────────────

        /// <inheritdoc/>
        public float ConcealmentValue => concealmentValue;

        /// <inheritdoc/>
        /// <remarks>
        /// 直接透传 <see cref="EnemyHealth.CurrentHealth"/>，
        /// 供 <c>LockOnProcessor</c> 判断"已在飞行中的导弹是否足以击杀目标"。
        /// </remarks>
        public float CurrentHealth => health != null ? health.CurrentHealth : 0f;

        /// <inheritdoc/>
        /// <remarks>
        /// 使用 <c>health.CurrentHealth > 0</c> 而非 <c>gameObject.activeSelf</c>：
        /// 两者在大多数情况等价，但此写法更准确表达语义，
        /// 且在死亡事件广播期间（SetActive 调用前的一帧内）也能返回正确值。
        /// </remarks>
        public bool IsAlive => health != null && health.CurrentHealth > 0f;

        /// <inheritdoc/>
        public Transform BodyTransform => transform;

        /// <inheritdoc/>
        public void OnMissileHit(float damage) {
            health?.TakeDamage(damage);
        }
    }
}
