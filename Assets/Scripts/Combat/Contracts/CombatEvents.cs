namespace VisionProject.Combat.Contracts {
    /// <summary>
    /// 模块 A 所有跨系统事件名的集中定义。
    /// <para>
    /// 命名规则：<c>"Combat.{动作/状态}"</c>，采用名词+动词过去式，
    /// 表示"某件事已经发生"，订阅方只需响应，不需反向修改发布方状态。
    /// </para>
    /// <para>
    /// 使用方式：<c>Game.Event.Invoke(CombatEvents.MissileFired, payload)</c>
    /// </para>
    /// </summary>
    public static class CombatEvents {
        /// <summary>
        /// 跟踪导弹成功发射时广播。
        /// <para>载荷类型：<see cref="MissileFiredPayload"/></para>
        /// <para>典型订阅方：VFX 系统、音频系统</para>
        /// </summary>
        public const string MissileFired = "Combat.MissileFired";

        /// <summary>
        /// 某敌人锁定进度达到隐蔽值阈值，即将触发导弹发射。
        /// <para>载荷类型：<see cref="EnemyLockedPayload"/></para>
        /// <para>典型订阅方：MissilePool（内部通信，外部模块不应依赖此事件触发时序）</para>
        /// </summary>
        public const string EnemyFullyLocked = "Combat.EnemyFullyLocked";

        /// <summary>
        /// 敌人生命值归零，确认死亡时广播。
        /// <para>载荷类型：<see cref="EnemyDiedPayload"/></para>
        /// <para>典型订阅方：LockOnProcessor、飞行中的 TrackingMissile、经验掉落系统</para>
        /// </summary>
        public const string EnemyDied = "Combat.EnemyDied";

        /// <summary>
        /// 锁定进度发生变化（节流后定期发送，不保证每帧触发）。
        /// <para>载荷类型：<see cref="LockProgressPayload"/></para>
        /// <para>典型订阅方：HUD 系统（锁定进度条）</para>
        /// </summary>
        public const string LockProgressChanged = "Combat.LockProgressChanged";
    }
}
