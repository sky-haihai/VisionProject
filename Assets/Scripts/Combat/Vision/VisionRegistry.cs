using System.Collections.Generic;
using UnityEngine;

namespace VisionProject.Combat.Vision {
    /// <summary>
    /// 场景级视界注册表（Scene Singleton）。
    /// 持有当前所有激活的 <see cref="VisionLayer"/>，
    /// 为 LockOnProcessor 提供零 GC 的查询入口。
    /// <para>
    /// 使用 <c>[DefaultExecutionOrder(-100)]</c> 确保 Awake 在其他 Combat 脚本之前执行，
    /// 避免 VisionLayer.OnEnable 调用 Register 时 Instance 尚未初始化。
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class VisionRegistry : MonoBehaviour {
        // ── Singleton ─────────────────────────────────────────────────────

        public static VisionRegistry Instance { get; private set; }

        // ── 数据 ──────────────────────────────────────────────────────────

        // 预分配容量 16，正常局内视界层不会超过此数，避免动态扩容
        private readonly List<VisionLayer> _activeLayers = new(16);

        // ── 生命周期 ──────────────────────────────────────────────────────

        private void Awake() {
            if (Instance != null && Instance != this) {
                Debug.LogWarning("[VisionRegistry] 场景中存在多个实例，销毁多余的。", this);
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        // ── 公开 API ──────────────────────────────────────────────────────

        /// <summary>
        /// 注册一个激活的视界层。由 <see cref="VisionLayer.OnEnable"/> 调用。
        /// </summary>
        public void Register(VisionLayer layer) {
            if (layer == null) return;
            if (!_activeLayers.Contains(layer)) {
                _activeLayers.Add(layer);
            }
        }

        /// <summary>
        /// 注销一个视界层。由 <see cref="VisionLayer.OnDisable"/> 调用。
        /// </summary>
        public void Unregister(VisionLayer layer) {
            _activeLayers.Remove(layer); // List.Remove 是 O(n)，层数少时可接受
        }

        /// <summary>
        /// 返回覆盖指定世界坐标的所有激活视界的锁定速度之和。
        /// 此方法每帧被 LockOnProcessor 为每个存活敌人调用一次，
        /// 全程零 GC（无装箱、无集合分配）。
        /// </summary>
        /// <param name="worldPoint">待检测的世界坐标（敌人中心位置）。</param>
        public float GetTotalLockSpeedAt(Vector2 worldPoint) {
            float total = 0f;
            // 直接 for 循环 + 索引，避免 foreach 的 Enumerator 分配
            for (int i = 0, n = _activeLayers.Count; i < n; i++) {
                VisionLayer layer = _activeLayers[i];
                if (layer == null || !layer.isActiveAndEnabled) continue;
                if (layer.IsPointInside(worldPoint)) {
                    total += layer.LockOnSpeed;
                }
            }
            return total;
        }

        /// <summary>返回所有当前激活视界层的只读视图（供调试和 Gizmos 使用）。</summary>
        public IReadOnlyList<VisionLayer> GetAllLayers() => _activeLayers;
    }
}
