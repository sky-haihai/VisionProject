using System.Collections.Generic;
using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.Enemy {
    /// <summary>
    /// 场景中所有存活 <see cref="ILockableTarget"/> 的静态注册表。
    /// <para>
    /// 纯静态类（无 MonoBehaviour），零框架依赖，线程安全假设：所有操作在主线程进行。
    /// </para>
    /// <para>
    /// <b>生命周期维护方</b>：<c>EnemyBase</c>（Phase 5）在 <c>OnEnable</c> 调用 <see cref="Register"/>，
    /// 在 <c>OnDisable</c> 调用 <see cref="Unregister"/>。
    /// </para>
    /// <para>
    /// <b>查询方</b>：<c>LockOnProcessor</c>（Phase 3）每帧读取 <see cref="Alive"/> 遍历所有存活目标。
    /// 返回值为 <see cref="IReadOnlyList{T}"/>，不可外部修改，内部 <c>for</c> 遍历零 GC。
    /// </para>
    /// </summary>
    public static class EnemyRegistry {
        // 预分配容量 32，普通局内敌人数量不超过此值，避免首次扩容
        private static readonly List<ILockableTarget> _alive = new(32);

        /// <summary>当前场景中所有已注册且存活的目标（只读视图）。</summary>
        public static IReadOnlyList<ILockableTarget> Alive => _alive;

        /// <summary>
        /// 将目标加入存活列表。由 <c>EnemyBase.OnEnable</c> 调用。
        /// 重复注册会被忽略（Contains 检查）。
        /// </summary>
        public static void Register(ILockableTarget target) {
            if (target != null && !_alive.Contains(target)) {
                _alive.Add(target);
            }
        }

        /// <summary>
        /// 从存活列表中移除目标。由 <c>EnemyBase.OnDisable</c> 调用。
        /// 目标不在列表中时安全忽略。
        /// </summary>
        public static void Unregister(ILockableTarget target) {
            _alive.Remove(target);
        }

        /// <summary>
        /// 清空注册表。用于场景卸载或重置关卡时确保无残留引用。
        /// </summary>
        public static void Clear() {
            _alive.Clear();
        }
    }
}
