namespace VisionProject.Combat.Vision {
    /// <summary>视界的几何形状种类，对应 <see cref="VisionLayerData"/> 中各组参数。</summary>
    public enum VisionShapeType {
        /// <summary>以中心点为圆心的圆形区域。</summary>
        Circle = 0,

        /// <summary>以中心点为顶点、朝战机前方展开的扇形区域。</summary>
        Sector = 1,

        /// <summary>以中心点为起点、向战机前方延伸的矩形区域。</summary>
        Rect = 2,

        /// <summary>以中心点为圆心、内外半径确定的圆环区域。</summary>
        Ring = 3,
    }
}
