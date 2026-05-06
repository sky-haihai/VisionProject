using UnityEngine;

namespace VisionProject.Combat.Vision.Shapes {
    /// <summary>
    /// 矩形视界：以 origin 为起点、沿 <paramref name="forward"/> 方向延伸 <c>length</c>，
    /// 左右各扩 <c>width/2</c> 的矩形区域。
    /// 矩形不跨越 origin 后方（localY &lt; 0 不在范围内）。
    /// </summary>
    public sealed class RectVisionShape : IVisionShape {
        private readonly float _length;
        private readonly float _halfWidth;

        public RectVisionShape(float width, float length) {
            _halfWidth = Mathf.Max(0f, width)  * 0.5f;
            _length    = Mathf.Max(0f, length);
        }

        /// <inheritdoc/>
        public bool IsPointInside(Vector2 worldPoint, Vector2 origin, Vector2 forward) {
            Vector2 diff = worldPoint - origin;

            // 将 diff 投影到战机坐标系
            // forward 为战机前方（+Y 在世界空间的映射），right 为顺时针 90° 的垂直方向
            Vector2 right  = new Vector2(forward.y, -forward.x);
            float   localY = Vector2.Dot(diff, forward); // 前后分量
            float   localX = Vector2.Dot(diff, right);   // 左右分量

            return localY >= 0f && localY <= _length && Mathf.Abs(localX) <= _halfWidth;
        }

        /// <inheritdoc/>
        public void DrawGizmos(Vector2 origin, Vector2 forward) {
            Vector2 right     = new Vector2(forward.y, -forward.x);
            Vector2 halfRight = right * _halfWidth;

            Vector2 bl = origin - halfRight;                    // bottom-left
            Vector2 br = origin + halfRight;                    // bottom-right
            Vector2 tl = bl + forward * _length;                // top-left
            Vector2 tr = br + forward * _length;                // top-right

            Gizmos.DrawLine(bl, br);
            Gizmos.DrawLine(br, tr);
            Gizmos.DrawLine(tr, tl);
            Gizmos.DrawLine(tl, bl);
        }
    }
}
