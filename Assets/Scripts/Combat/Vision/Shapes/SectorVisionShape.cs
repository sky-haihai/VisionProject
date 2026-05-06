using UnityEngine;

namespace VisionProject.Combat.Vision.Shapes {
    /// <summary>
    /// 扇形视界：以 origin 为顶点、朝 <paramref name="forward"/> 方向展开，
    /// 覆盖角度 ±<c>sectorAngle/2</c>、半径 <c>sectorRadius</c> 的扇形区域。
    /// </summary>
    public sealed class SectorVisionShape : IVisionShape {
        private readonly float _radius;
        private readonly float _radiusSq;
        private readonly float _sectorAngleDeg;

        // 半张角的余弦值，构造期预算，避免每帧调用 Mathf.Cos
        private readonly float _cosHalfAngle;

        public SectorVisionShape(float radius, float sectorAngleDeg) {
            _radius         = Mathf.Max(0f, radius);
            _radiusSq       = _radius * _radius;
            _sectorAngleDeg = Mathf.Clamp(sectorAngleDeg, 0f, 360f);
            _cosHalfAngle   = Mathf.Cos(_sectorAngleDeg * 0.5f * Mathf.Deg2Rad);
        }

        /// <inheritdoc/>
        public bool IsPointInside(Vector2 worldPoint, Vector2 origin, Vector2 forward) {
            Vector2 diff   = worldPoint - origin;
            float   distSq = diff.sqrMagnitude;

            // 先用平方距离快速剔除：超出半径则必不在扇形内
            if (distSq > _radiusSq) return false;
            // 距离极近（重合）时直接判定为内部，规避零向量归一化
            if (distSq < 0.0001f) return true;

            // 使用点积判断角度：dot(normalize(diff), forward) >= cos(halfAngle)
            float dot = Vector2.Dot(diff / Mathf.Sqrt(distSq), forward);
            return dot >= _cosHalfAngle;
        }

        /// <inheritdoc/>
        public void DrawGizmos(Vector2 origin, Vector2 forward) {
            // 将 forward 向量转为标准数学角（从 +X 轴逆时针）
            float forwardDeg = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg;
            float half       = _sectorAngleDeg * 0.5f;
            float startDeg   = forwardDeg - half;
            float endDeg     = forwardDeg + half;

            // 两条半径
            Vector2 startEdge = origin + new Vector2(Mathf.Cos(startDeg * Mathf.Deg2Rad), Mathf.Sin(startDeg * Mathf.Deg2Rad)) * _radius;
            Vector2 endEdge   = origin + new Vector2(Mathf.Cos(endDeg   * Mathf.Deg2Rad), Mathf.Sin(endDeg   * Mathf.Deg2Rad)) * _radius;
            Gizmos.DrawLine(origin, startEdge);
            Gizmos.DrawLine(origin, endEdge);

            // 弧线
            VisionGizmoHelper.DrawArc(origin, _radius, startDeg, endDeg);
        }
    }
}
