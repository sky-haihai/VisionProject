using UnityEngine;
using XiheFramework.Runtime;
using VisionProject.Combat.Contracts;

namespace VisionProject.Combat.Missile {
    /// <summary>
    /// 跟踪导弹行为组件，挂载在导弹 Prefab 上，由 <see cref="MissilePool"/> 统一管理生命周期。
    /// <para>
    /// 飞行分为两个阶段：
    /// <list type="bullet">
    ///   <item><b>Phase A（抛物线）</b>：持续 <see cref="launchDuration"/> 秒，沿初始方向做弧线出膛动画，
    ///         营造导弹"爬升离轨"的视觉效果，不进行目标追踪。</item>
    ///   <item><b>Phase B（追踪）</b>：朝目标旋转，每帧用 <c>Quaternion.RotateTowards</c> 限制转速，
    ///         避免导弹瞬间 180° 掉头。目标死亡时立即回池。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 生命周期钩子：<c>OnEnable</c> 订阅 <see cref="CombatEvents.EnemyDied"/>；
    /// <c>OnDisable</c> 取消订阅并清零刚体速度。
    /// 对象池通过 <c>SetActive(false/true)</c> 驱动此钩子，无需手动清理。
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class TrackingMissile : MonoBehaviour {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [Header("飞行参数")]
        [SerializeField, Tooltip("抛物线阶段的平移速度（世界单位/秒）"), Min(0.1f)]
        private float launchSpeed = 5f;

        [SerializeField, Tooltip("追踪阶段的飞行速度（世界单位/秒）"), Min(0.1f)]
        private float trackingSpeed = 12f;

        [SerializeField, Tooltip("追踪阶段每秒最大转向角度（°/s）；过小会导致导弹追不上急转目标"), Min(1f)]
        private float maxTurnSpeedDeg = 180f;

        [SerializeField, Tooltip("抛物线阶段持续时间（秒）"), Min(0f)]
        private float launchDuration = 0.4f;

        [SerializeField, Tooltip("抛物线弧高（世界单位），垂直于初始飞行方向的最大偏移量"), Min(0f)]
        private float launchArcHeight = 2f;

        [Header("命中参数")]
        [SerializeField, Tooltip("导弹伤害量（固定值，传给 ILockableTarget.OnMissileHit）"), Min(0f)]
        private float damage = 100f;

        [SerializeField, Tooltip("最大存活时间（秒）；超时自毁回池，防止追踪卡死"), Min(1f)]
        private float maxLifetime = 10f;

        [SerializeField, Tooltip("敌人所在的碰撞层（LayerMask），用于 OnTriggerEnter2D 过滤")]
        private LayerMask enemyLayer;

        // ── 内部状态 ──────────────────────────────────────────────────────

        private Rigidbody2D        _rb;
        private ILockableTarget    _target;
        private MissilePool        _pool;

        // 抛物线阶段缓存
        private Vector2 _launchStartPos;    // 发射原点（世界坐标）
        private Vector2 _launchDir;         // 归一化初始飞行方向
        private Vector2 _launchPerp;        // 垂直于 _launchDir 的法向量（CCW 90°）

        private float _elapsedTime;         // 从发射起累计时间
        private string _enemyDiedHandlerId;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake() {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void OnEnable() {
            _enemyDiedHandlerId = Game.Event.Subscribe(CombatEvents.EnemyDied, OnEnemyDied);
        }

        private void OnDisable() {
            if (!string.IsNullOrEmpty(_enemyDiedHandlerId)) {
                Game.Event.Unsubscribe(CombatEvents.EnemyDied, _enemyDiedHandlerId);
                _enemyDiedHandlerId = null;
            }
            // 防止 SetActive(false) 后刚体残留速度在下次激活前闪现
            _rb.velocity = Vector2.zero;
        }

        // ── 公开 API（供 MissilePool 调用）──────────────────────────────

        /// <summary>
        /// 在 <c>SetActive(true)</c> 之前由 <see cref="MissilePool"/> 调用，初始化发射状态。
        /// 必须在 <c>OnEnable</c> 之前完成，否则追踪状态不正确。
        /// </summary>
        /// <param name="target">锁定目标。</param>
        /// <param name="firePosition">发射起点（世界坐标）。</param>
        /// <param name="launchDirection">初始飞行方向（会被归一化）。</param>
        /// <param name="pool">回池引用。</param>
        public void PrepareToLaunch(ILockableTarget target, Vector3 firePosition,
                                    Vector2 launchDirection, MissilePool pool) {
            _target    = target;
            _pool      = pool;
            _elapsedTime = 0f;

            transform.position = firePosition;
            _launchStartPos = firePosition;
            _launchDir      = launchDirection.sqrMagnitude > 0.0001f
                              ? launchDirection.normalized
                              : Vector2.up;
            // 法向量：将 _launchDir 逆时针旋转 90°
            _launchPerp = new Vector2(-_launchDir.y, _launchDir.x);

            // 使 transform.up 对齐初始飞行方向（2D 旋转公式：angle = Atan2(-dx, dy)）
            float initialAngle = Mathf.Atan2(-_launchDir.x, _launchDir.y) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, initialAngle);
        }

        // ── 主循环 ────────────────────────────────────────────────────────

        private void Update() {
            _elapsedTime += Time.deltaTime;

            if (_elapsedTime >= maxLifetime) {
                ReturnToPool();
                return;
            }

            if (_elapsedTime < launchDuration) {
                UpdateLaunchPhase();
            } else {
                UpdateTrackingPhase();
            }
        }

        // ── 私有飞行逻辑 ──────────────────────────────────────────────────

        /// <summary>
        /// Phase A：基于参数曲线直接设置 transform.position，模拟抛物线出膛弧。
        /// 导弹朝向实时跟随速度切线方向（更真实的弧线姿态）。
        /// </summary>
        private void UpdateLaunchPhase() {
            float t = _elapsedTime / launchDuration;  // [0, 1)

            // 基础直线位移 + 法向弧线位移，直接写位置（非物理驱动，不影响 Rigidbody 状态）
            Vector2 basePos     = _launchStartPos + _launchDir * (launchSpeed * _elapsedTime);
            float   arcOffset   = Mathf.Sin(t * Mathf.PI) * launchArcHeight;
            transform.position  = (Vector3)(basePos + _launchPerp * arcOffset);

            // 速度切线方向：对弧线函数求导，使导弹姿态始终沿飞行路径切线
            // d/dt [sin(t*PI)*H] = cos(t*PI)*PI/launchDuration*H
            float arcVelComponent = Mathf.Cos(t * Mathf.PI) * (Mathf.PI / launchDuration) * launchArcHeight;
            Vector2 tangent = _launchDir * launchSpeed + _launchPerp * arcVelComponent;
            if (tangent.sqrMagnitude > 0.0001f) {
                float angle = Mathf.Atan2(-tangent.x, tangent.y) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>
        /// Phase B：追踪目标，每帧以 <see cref="maxTurnSpeedDeg"/> 限速旋转，
        /// Rigidbody2D.velocity 始终等于 transform.up * trackingSpeed。
        /// </summary>
        private void UpdateTrackingPhase() {
            if (_target == null || !_target.IsAlive) {
                ReturnToPool();
                return;
            }

            Vector2 toTarget = (Vector2)_target.BodyTransform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude > 0.0001f) {
                float targetAngle = Mathf.Atan2(-toTarget.x, toTarget.y) * Mathf.Rad2Deg;
                Quaternion targetRot = Quaternion.Euler(0f, 0f, targetAngle);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, maxTurnSpeedDeg * Time.deltaTime);
            }

            // 速度方向始终与机头朝向一致（transform.up = 导弹前方）
            _rb.velocity = (Vector2)transform.up * trackingSpeed;
        }

        // ── 碰撞检测 ──────────────────────────────────────────────────────

        private void OnTriggerEnter2D(Collider2D other) {
            // 层过滤：位操作比 CompareTag 更快，且无字符串分配
            if (((1 << other.gameObject.layer) & enemyLayer.value) == 0) return;

            ILockableTarget hitTarget = other.GetComponent<ILockableTarget>();
            if (hitTarget == null || !hitTarget.IsAlive) return;

            hitTarget.OnMissileHit(damage);
            // MissileFired 在命中时广播，供 VFX / 音效系统订阅（记录命中位置）
            Game.Event.InvokeNow(CombatEvents.MissileFired,
                new MissileFiredPayload(hitTarget, transform.position));
            ReturnToPool();
        }

        // ── 事件与对象池 ──────────────────────────────────────────────────

        private void OnEnemyDied(object sender, object e) {
            if (e is not EnemyDiedPayload payload) return;
            // 仅处理当前正在追踪的目标，其他目标的死亡事件忽略
            if (payload.Target == _target) {
                ReturnToPool();
            }
        }

        private void ReturnToPool() {
            if (_pool != null) {
                _pool.Return(this);
            } else {
                // 没有池引用时（例如测试场景），直接禁用
                gameObject.SetActive(false);
            }
        }
    }
}
