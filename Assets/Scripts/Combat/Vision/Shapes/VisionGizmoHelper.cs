using UnityEngine;

namespace VisionProject.Combat.Vision.Shapes {
    /// <summary>
    /// 供各形状的 DrawGizmos 实现使用的静态绘制工具方法。
    /// 只依赖 UnityEngine.Gizmos，可在非 Editor 环境编译（运行时 Gizmos 为空操作）。
    /// </summary>
    internal static class VisionGizmoHelper {
        /// <summary>用折线段模拟绘制一个完整圆形。</summary>
        internal static void DrawCircle(Vector2 center, float radius, int segments = 36) {
            if (radius <= 0f) return;

            float step = 2f * Mathf.PI / segments;
            // 首点复用为 prev，避免在循环内重复计算
            var prev = center + new Vector2(radius, 0f);
            for (int i = 1; i <= segments; i++) {
                float angle = i * step;
                var next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        /// <summary>
        /// 用折线段绘制一段圆弧（不含端点到圆心的连线）。
        /// 角度参数为标准数学角（从 +X 轴逆时针，单位：度）。
        /// </summary>
        internal static void DrawArc(Vector2 center, float radius, float startDeg, float endDeg, int segments = 24) {
            if (radius <= 0f || segments < 1) return;

            float startRad = startDeg * Mathf.Deg2Rad;
            float endRad   = endDeg   * Mathf.Deg2Rad;
            float step     = (endRad - startRad) / segments;

            var prev = center + new Vector2(Mathf.Cos(startRad), Mathf.Sin(startRad)) * radius;
            for (int i = 1; i <= segments; i++) {
                float a    = startRad + i * step;
                var   next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
