using UnityEngine;

namespace VisionProject.Combat.Vision {
    /// <summary>
    /// 视界几何形状的策略接口。
    /// 所有形状仅包含纯数学判定逻辑，不持有任何 MonoBehaviour 或 Transform 引用，
    /// 方便单元测试且不产生 GC。
    /// </summary>
    public interface IVisionShape {
        /// <summary>
        /// 判断世界空间中的点是否位于该视界几何区域内。
        /// </summary>
        /// <param name="worldPoint">待检测的世界坐标（敌人位置）。</param>
        /// <param name="origin">视界的世界空间原点（通常为 VisionLayer 自身 Transform 坐标）。</param>
        /// <param name="forward">视界朝向的归一化方向向量（通常为 <c>transform.up</c>）。</param>
        bool IsPointInside(Vector2 worldPoint, Vector2 origin, Vector2 forward);

        /// <summary>
        /// 在编辑器中绘制该形状的 Gizmos 轮廓，仅用于调试可视化。
        /// 实现中只使用 <c>UnityEngine.Gizmos</c> 方法，不依赖 UnityEditor 命名空间。
        /// </summary>
        /// <param name="origin">绘制原点。</param>
        /// <param name="forward">朝向（归一化）。</param>
        void DrawGizmos(Vector2 origin, Vector2 forward);
    }
}
