using UnityEngine;

namespace VisionProject.Combat.Vision.Shapes {
    /// <summary>
    /// 圆形视界：以 origin 为圆心，<see cref="_radius"/> 为半径的全向区域。
    /// 判定与朝向无关，<paramref name="forward"/> 参数在此形状中被忽略。
    /// </summary>
    public sealed class CircleVisionShape : IVisionShape {
        private readonly float _radius;
        private readonly float _radiusSq; // 避免每帧 sqrt

        public CircleVisionShape(float radius) {
            _radius   = Mathf.Max(0f, radius);
            _radiusSq = _radius * _radius;
        }

        /// <inheritdoc/>
        public bool IsPointInside(Vector2 worldPoint, Vector2 origin, Vector2 forward) {
            // sqrMagnitude 避免开方运算
            return (worldPoint - origin).sqrMagnitude <= _radiusSq;
        }

        /// <inheritdoc/>
        public void DrawGizmos(Vector2 origin, Vector2 forward) {
            VisionGizmoHelper.DrawCircle(origin, _radius);
        }
    }
}
