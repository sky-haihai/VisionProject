using UnityEngine;

namespace VisionProject.Combat.Player {
    /// <summary>
    /// 玩家战机控制器。负责：
    /// <list type="bullet">
    ///   <item>WASD 全向移动（Rigidbody2D.MovePosition，FixedUpdate 驱动）</item>
    ///   <item>鼠标控制战机朝向，转向速度由 <see cref="maxTurnSpeedDegPerSec"/> 限制</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour {
        // ── Inspector 参数 ─────────────────────────────────────────────────

        [Header("移动")]
        [SerializeField, Tooltip("战机平移速度（单位/秒）")]
        private float moveSpeed = 8f;

        [Header("旋转")]
        [SerializeField, Tooltip("战机最大转向速度（°/秒）；设为 0 则完全无法转向")]
        private float maxTurnSpeedDegPerSec = 360f;

        [SerializeField, Tooltip("用于鼠标射线的摄像机；留空则自动取 Camera.main")]
        private Camera mainCamera;

        // ── 私有字段 ───────────────────────────────────────────────────────

        private Rigidbody2D _rb;

        // 缓存每帧移动方向，在 Update 读取、FixedUpdate 消费，避免跳帧
        private Vector2 _moveInput;

        // ── 生命周期 ───────────────────────────────────────────────────────

        private void Awake() {
            _rb = GetComponent<Rigidbody2D>();

            // 俯视角 2D：锁定 Z 轴位移和 XY 轴旋转，防止物理模拟偏移
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.gravityScale = 0f;

            if (mainCamera == null) {
                mainCamera = Camera.main;
            }
        }

        private void Update() {
            ReadMoveInput();
            UpdateRotation();
        }

        private void FixedUpdate() {
            ApplyMovement();
        }

        // ── 移动 ───────────────────────────────────────────────────────────

        /// <summary>每帧读取原始按键输入并归一化，避免斜向移动速度叠加。</summary>
        private void ReadMoveInput() {
            // GetAxisRaw 不做插值，响应更直接；归一化防止斜向速度 √2 倍
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            _moveInput = new Vector2(h, v).normalized;
        }

        /// <summary>
        /// 使用 MovePosition 驱动移动，与物理引擎协作，
        /// 保留碰撞检测能力（不会穿透 Collider）。
        /// </summary>
        private void ApplyMovement() {
            if (_moveInput == Vector2.zero) return;
            Vector2 nextPos = _rb.position + _moveInput * (moveSpeed * Time.fixedDeltaTime);
            _rb.MovePosition(nextPos);
        }

        // ── 旋转 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 将鼠标屏幕坐标转换为世界坐标，计算目标朝向角度，
        /// 再以 <see cref="maxTurnSpeedDegPerSec"/> 为上限做插值旋转。
        /// </summary>
        private void UpdateRotation() {
            if (mainCamera == null) return;

            Vector2 mouseWorld = GetMouseWorldPosition();
            Vector2 dir        = mouseWorld - (Vector2)transform.position;

            // 死区：鼠标与战机重合时不计算（避免 Atan2(0,0) 的不稳定输出）
            if (dir.sqrMagnitude < 0.001f) return;

            // Atan2(y, x) → 从 +X 轴逆时针的弧度；减 90° 将参考方向改为 +Y（战机"前方"）
            float targetAngle  = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            float currentAngle = transform.eulerAngles.z;

            // MoveTowardsAngle 内部处理 0°/360° 边界跳变
            float newAngle = Mathf.MoveTowardsAngle(
                currentAngle,
                targetAngle,
                maxTurnSpeedDegPerSec * Time.deltaTime
            );

            transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
        }

        // ── 辅助方法 ───────────────────────────────────────────────────────

        /// <summary>
        /// 将鼠标屏幕像素坐标转换为游戏世界坐标（XY 平面，Z=0）。
        /// </summary>
        private Vector2 GetMouseWorldPosition() {
            // z 分量设为摄像机到 XY 平面的距离，确保投影正确
            Vector3 screenPos = Input.mousePosition;
            screenPos.z       = -mainCamera.transform.position.z;
            return mainCamera.ScreenToWorldPoint(screenPos);
        }

        // ── 公开 API（供外部系统查询）──────────────────────────────────────

        /// <summary>当前战机朝向的世界空间单位向量（+Y 方向为"前方"）。</summary>
        public Vector2 Forward => transform.up;

        /// <summary>当前世界坐标。</summary>
        public Vector2 Position => transform.position;
    }
}
