using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.LockOn {
    /// <summary>
    /// 单个可锁定目标的锁定进度快照（per-enemy state）。
    /// <para>
    /// 设计为可变 struct，存储在 <c>Dictionary&lt;ILockableTarget, LockOnProgressEntry&gt;</c> 的值槽中。
    /// 调用方读取后修改，必须显式写回字典（C# struct 语义），以避免"修改副本"的隐患。
    /// </para>
    /// </summary>
    internal struct LockOnProgressEntry {
        /// <summary>被追踪的目标引用（非 null）。</summary>
        public ILockableTarget Target;

        /// <summary>
        /// 当前锁定进度，范围 <c>[0, Target.ConcealmentValue]</c>。
        /// <list type="bullet">
        ///   <item>目标在视界内：每帧 += totalLockSpeed * deltaTime</item>
        ///   <item>目标离开视界：每帧 -= currentProgress * decayRate * deltaTime（指数衰减）</item>
        /// </list>
        /// </summary>
        public float CurrentProgress;

        /// <summary>
        /// 已对此目标发射导弹，且该导弹预计足以击杀目标（currentHealth ≤ missileDamage）时为 <c>true</c>。
        /// <para>
        /// 此标记为 <c>true</c> 时，<see cref="LockOnProcessor"/> 不再向 HUD 广播该目标的进度事件，
        /// 避免玩家看到"锁定满了但目标仍活"的错误反馈。
        /// 目标死亡后（收到 <c>CombatEvents.OnEnemyDied</c>）整个 Entry 将被从字典中移除。
        /// </para>
        /// </summary>
        public bool IsMissilePending;

        /// <summary>创建一个针对指定目标的初始进度记录。</summary>
        public LockOnProgressEntry(ILockableTarget target) {
            Target           = target;
            CurrentProgress  = 0f;
            IsMissilePending = false;
        }
    }
}
