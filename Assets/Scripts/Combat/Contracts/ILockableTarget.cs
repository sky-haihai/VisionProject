using UnityEngine;

namespace VisionProject.Combat.Contracts {
    /// <summary>
    /// 所有可被视界系统锁定的目标必须实现此接口。
    /// 模块 A 的锁定与导弹系统只依赖此接口，不依赖任何具体敌人实现。
    /// </summary>
    public interface ILockableTarget {
        /// <summary>
        /// 隐蔽值：锁定进度需要达到此值才会触发导弹发射。
        /// 值越高越难被锁定。
        /// </summary>
        float ConcealmentValue { get; }

        /// <summary>
        /// 当前生命值，供 LockOnProcessor 判断发射后是否能击杀目标（决定是否隐藏进度条）。
        /// </summary>
        float CurrentHealth { get; }

        /// <summary>
        /// 目标是否存活。导弹在追踪途中每帧检查此值，为 false 时立即自毁回池。
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// 目标的世界空间 Transform，用于视界几何判定和导弹追踪坐标。
        /// 分离出来而非直接暴露 position，是为了支持将来骨骼绑点等扩展。
        /// </summary>
        Transform BodyTransform { get; }

        /// <summary>
        /// 被跟踪导弹命中时调用。
        /// 实现方负责扣血、死亡判定和事件广播。
        /// </summary>
        /// <param name="damage">导弹造成的伤害量（当前固定 100）</param>
        void OnMissileHit(float damage);
    }
}
