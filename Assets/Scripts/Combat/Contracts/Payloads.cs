using UnityEngine;

namespace VisionProject.Combat.Contracts {
    /// <summary>
    /// 导弹成功发射时广播。
    /// 订阅方：VFX 系统、音频系统、统计系统。
    /// </summary>
    public readonly struct MissileFiredPayload {
        /// <summary>导弹的发射目标</summary>
        public readonly ILockableTarget Target;

        /// <summary>发射时导弹的世界坐标（用于特效生成位置）</summary>
        public readonly Vector3 FirePosition;

        public MissileFiredPayload(ILockableTarget target, Vector3 firePosition) {
            Target       = target;
            FirePosition = firePosition;
        }
    }

    /// <summary>
    /// 某敌人锁定进度首次达到隐蔽值阈值时广播（内部事件）。
    /// 订阅方：MissilePool（负责取出导弹并发射）。
    /// </summary>
    public readonly struct EnemyLockedPayload {
        /// <summary>被完全锁定的目标</summary>
        public readonly ILockableTarget Target;

        public EnemyLockedPayload(ILockableTarget target) {
            Target = target;
        }
    }

    /// <summary>
    /// 敌人死亡时广播。
    /// 订阅方：LockOnProcessor（清理 Entry）、飞行中的 TrackingMissile（自毁）。
    /// </summary>
    public readonly struct EnemyDiedPayload {
        /// <summary>已死亡的目标</summary>
        public readonly ILockableTarget Target;

        public EnemyDiedPayload(ILockableTarget target) {
            Target = target;
        }
    }

    /// <summary>
    /// 锁定进度发生变化时广播（节流后定期发送，非每帧）。
    /// 订阅方：HUD 系统（渲染锁定进度条）。
    /// </summary>
    public readonly struct LockProgressPayload {
        /// <summary>进度变化的目标</summary>
        public readonly ILockableTarget Target;

        /// <summary>当前锁定进度值 [0, MaxProgress]</summary>
        public readonly float Progress;

        /// <summary>锁定进度上限（等于 Target.ConcealmentValue）</summary>
        public readonly float MaxProgress;

        /// <summary>当前进度百分比 [0, 1]，= Progress / MaxProgress</summary>
        public float NormalizedProgress => MaxProgress > 0f ? Progress / MaxProgress : 0f;

        public LockProgressPayload(ILockableTarget target, float progress, float maxProgress) {
            Target      = target;
            Progress    = progress;
            MaxProgress = maxProgress;
        }
    }
}
