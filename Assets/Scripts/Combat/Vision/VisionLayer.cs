using UnityEngine;

namespace VisionProject.Combat.Vision {
    /// <summary>
    /// 单个视界层的运行时组件。挂载在战机或其子对象上。
    /// <list type="bullet">
    ///   <item>Awake 时根据 <see cref="data"/> 实例化 <see cref="IVisionShape"/>。</item>
    ///   <item>OnEnable / OnDisable 时自动向 <see cref="VisionRegistry"/> 注册/注销。</item>
    ///   <item>若 data.Duration > 0，自计时到期后自动禁用自身。</item>
    /// </list>
    /// </summary>
    public sealed class VisionLayer : MonoBehaviour {
        // ── Inspector 参数 ────────────────────────────────────────────────

        [SerializeField, Tooltip("此视界层的几何与属性配置（ScriptableObject）")]
        private VisionLayerData data;

        [SerializeField, Tooltip("视界的世界空间原点 Transform；留空则使用自身 Transform")]
        private Transform originOverride;

        // ── 私有字段 ──────────────────────────────────────────────────────

        private IVisionShape _shape;
        private float        _remainingDuration;

        // ── 属性 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 此视界层的锁定速度；data 为 null 时返回 0。
        /// LockOnProcessor 每帧读取此值，应为 O(1) 无分配。
        /// </summary>
        public float LockOnSpeed => data != null ? data.LockOnSpeed : 0f;

        /// <summary>此视界层的配置数据。</summary>
        public VisionLayerData Data => data;

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake() {
            RebuildShape();
        }

        private void OnEnable() {
            // 有时限视界重新激活时重置计时器（适用于从对象池取出的场景）
            if (data != null && data.Duration > 0f) {
                _remainingDuration = data.Duration;
            }

            if (VisionRegistry.Instance != null) {
                VisionRegistry.Instance.Register(this);
            } else {
                Debug.LogWarning("[VisionLayer] VisionRegistry.Instance 为 null，" +
                                 "请确认场景中已放置 VisionRegistry 组件。", this);
            }
        }

        private void OnDisable() {
            VisionRegistry.Instance?.Unregister(this);
        }

        private void Update() {
            if (data == null || data.Duration <= 0f) return;

            _remainingDuration -= Time.deltaTime;
            if (_remainingDuration <= 0f) {
                gameObject.SetActive(false); // 触发 OnDisable → 自动从 Registry 注销
            }
        }

        // ── 公开 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 判断指定世界坐标是否在此视界的几何范围内。
        /// 由 <see cref="VisionRegistry.GetTotalLockSpeedAt"/> 每帧调用，
        /// 必须零 GC、无装箱。
        /// </summary>
        public bool IsPointInside(Vector2 worldPoint) {
            if (_shape == null) return false;

            Transform origin = originOverride != null ? originOverride : transform;
            return _shape.IsPointInside(worldPoint, origin.position, origin.up);
        }

        /// <summary>
        /// 运行时替换视界数据（例如武器升级时扩大范围）。
        /// 会重新实例化内部的 IVisionShape。
        /// </summary>
        public void SetData(VisionLayerData newData) {
            data = newData;
            RebuildShape();
            if (data != null && data.Duration > 0f) {
                _remainingDuration = data.Duration;
            }
        }

        // ── 私有方法 ──────────────────────────────────────────────────────

        private void RebuildShape() {
            _shape = data != null ? data.CreateShape() : null;
        }

        // ── 编辑器 Gizmos ─────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos() {
            // 编辑期间 Awake 不一定已调用，使用临时 shape 进行预览
            IVisionShape drawShape = _shape ?? data?.CreateShape();
            if (drawShape == null) return;

            Transform origin = originOverride != null ? originOverride : transform;
            // 激活状态：亮绿；未激活状态：暗灰（帮助区分哪些视界当前有效）
            Gizmos.color = isActiveAndEnabled
                ? new Color(0f, 1f, 0.4f, 0.85f)
                : new Color(0.5f, 0.5f, 0.5f, 0.4f);

            drawShape.DrawGizmos(origin.position, origin.up);
        }
#endif

#if UNITY_EDITOR
        private void OnValidate() {
            // 策划改参数后立即更新 shape，使 Gizmos 实时刷新
            RebuildShape();
        }
#endif
    }
}
