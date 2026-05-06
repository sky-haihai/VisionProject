using UnityEngine;
using VisionProject.Combat.Vision.Shapes;

namespace VisionProject.Combat.Vision {
    /// <summary>
    /// 单个视界层的配置数据（ScriptableObject）。
    /// 由武器或被动装备在运行时传给 <see cref="VisionLayer"/>，
    /// 策划可在 Project 窗口中直接创建并配置。
    /// </summary>
    [CreateAssetMenu(
        fileName = "VisionLayerData_New",
        menuName  = "VisionProject/Combat/Vision Layer Data",
        order     = 0
    )]
    public sealed class VisionLayerData : ScriptableObject {
        // ── 通用属性 ──────────────────────────────────────────────────────

        [Header("通用")]
        [SerializeField, Tooltip("视界的几何形状")]
        private VisionShapeType shapeType = VisionShapeType.Circle;

        [SerializeField, Tooltip("处于此视界内的敌人每秒增加的锁定进度量"), Min(0f)]
        private float lockOnSpeed = 1f;

        [SerializeField, Tooltip("视界持续时间（秒）；≤ 0 表示常驻，直到组件被禁用"), ]
        private float duration = -1f;

        // ── 圆形参数 ──────────────────────────────────────────────────────

        [Header("Circle（仅 shapeType = Circle 时生效）")]
        [SerializeField, Tooltip("圆形视界半径（世界单位）"), Min(0.1f)]
        private float circleRadius = 5f;

        // ── 扇形参数 ──────────────────────────────────────────────────────

        [Header("Sector（仅 shapeType = Sector 时生效）")]
        [SerializeField, Tooltip("扇形视界半径（世界单位）"), Min(0.1f)]
        private float sectorRadius = 6f;

        [SerializeField, Tooltip("扇形张角（度），以战机前方为中心线"), Range(1f, 360f)]
        private float sectorAngle = 90f;

        // ── 矩形参数 ──────────────────────────────────────────────────────

        [Header("Rect（仅 shapeType = Rect 时生效）")]
        [SerializeField, Tooltip("矩形宽度（左右各 width/2，世界单位）"), Min(0.1f)]
        private float rectWidth = 3f;

        [SerializeField, Tooltip("矩形长度（沿战机前方方向，世界单位）"), Min(0.1f)]
        private float rectLength = 8f;

        // ── 环形参数 ──────────────────────────────────────────────────────

        [Header("Ring（仅 shapeType = Ring 时生效）")]
        [SerializeField, Tooltip("环形内半径（世界单位）"), Min(0f)]
        private float ringInnerRadius = 2f;

        [SerializeField, Tooltip("环形外半径（必须 > 内半径，世界单位）"), Min(0.1f)]
        private float ringOuterRadius = 7f;

        // ── 只读属性（供 VisionLayer 使用）───────────────────────────────

        /// <summary>锁定速度（每秒进度增量）。</summary>
        public float LockOnSpeed => lockOnSpeed;

        /// <summary>持续时间（秒）；≤ 0 表示常驻。</summary>
        public float Duration => duration;

        /// <summary>形状类型，供调试/UI 显示。</summary>
        public VisionShapeType ShapeType => shapeType;

        // ── 工厂方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 根据当前配置实例化对应的 <see cref="IVisionShape"/> 实现。
        /// 每次调用都会 new 一个新对象——应仅在 Awake/OnValidate 等低频路径调用，
        /// 不要在 Update 中调用。
        /// </summary>
        public IVisionShape CreateShape() {
            return shapeType switch {
                VisionShapeType.Circle => new CircleVisionShape(circleRadius),
                VisionShapeType.Sector => new SectorVisionShape(sectorRadius, sectorAngle),
                VisionShapeType.Rect   => new RectVisionShape(rectWidth, rectLength),
                VisionShapeType.Ring   => new RingVisionShape(ringInnerRadius, ringOuterRadius),
                _                      => throw new System.ArgumentOutOfRangeException(
                                              nameof(shapeType), shapeType, "未知的 VisionShapeType")
            };
        }

#if UNITY_EDITOR
        private void OnValidate() {
            // 防止策划配置内半径 > 外半径
            if (ringInnerRadius >= ringOuterRadius) {
                ringOuterRadius = ringInnerRadius + 0.1f;
            }
        }
#endif
    }
}
