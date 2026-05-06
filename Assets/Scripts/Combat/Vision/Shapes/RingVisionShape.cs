using UnityEngine;

namespace VisionProject.Combat.Vision.Shapes {
    /// <summary>
    /// 环形视界：以 origin 为圆心，内半径与外半径之间的圆环区域。
    /// 判定与朝向无关，<paramref name="forward"/> 参数在此形状中被忽略。
    /// </summary>
    public sealed class RingVisionShape : IVisionShape {
        private readonly float _innerRadius;
        private readonly float _outerRadius;
        private readonly float _innerRadiusSq;
        private readonly float _outerRadiusSq;

        public RingVisionShape(float innerRadius, float outerRadius) {
            // 确保内半径 ≤ 外半径
            _innerRadius   = Mathf.Max(0f, Mathf.Min(innerRadius, outerRadius));
            _outerRadius   = Mathf.Max(0f, Mathf.Max(innerRadius, outerRadius));
            _innerRadiusSq = _innerRadius * _innerRadius;
            _outerRadiusSq = _outerRadius * _outerRadius;
        }

        /// <inheritdoc/>
        public bool IsPointInside(Vector2 worldPoint, Vector2 origin, Vector2 forward) {
            float distSq = (worldPoint - origin).sqrMagnitude;
            return distSq >= _innerRadiusSq && distSq <= _outerRadiusSq;
        }

        /// <inheritdoc/>
        public void DrawGizmos(Vector2 origin, Vector2 forward) {
            VisionGizmoHelper.DrawCircle(origin, _innerRadius);
            VisionGizmoHelper.DrawCircle(origin, _outerRadius);
        }
    }
}
