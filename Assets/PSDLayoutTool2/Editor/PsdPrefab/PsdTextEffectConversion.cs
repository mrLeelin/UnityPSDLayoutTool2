namespace PsdLayoutTool2
{
    using UnityEngine;

    /// <summary>
    /// 负责把 PSD 中以像素为单位的文字效果，转换成 TextMeshPro 材质使用的归一化参数。
    /// </summary>
    /// <remarks>
    /// PSD 描边宽度是绝对像素值，而 TMP 的 <c>_OutlineWidth</c> 是 0～1 范围的相对值，
    /// 因此不能直接把 PSD 数值写入材质。所有需要人工校准的参数都集中在本类顶部，
    /// 调整算法时不需要进入材质查找、复用或创建逻辑。
    /// </remarks>
    internal static class PsdTextEffectConversion
    {
        /// <summary>
        /// PSD 描边换算到 TMP 描边时使用的视觉校准倍率。
        /// </summary>
        /// <remarks>
        /// 当前值 <c>7 / 3</c> 与项目使用的 Figma Bridge 换算结果保持一致。
        /// 增大此值会让所有导入文字的描边整体变粗，减小则会整体变细。
        /// 如果只想调整小数精度或文字内部膨胀，不应修改此值。
        /// </remarks>
        public const float OutlineScale = 7f / 3f;

        /// <summary>
        /// TMP 描边结果保留的小数位数。
        /// </summary>
        /// <remarks>
        /// 统一舍入可以避免浮点微小差异为视觉上相同的文字生成多个重复材质。
        /// 当前保留两位小数，例如原始结果 0.2475 会写成 0.25。
        /// </remarks>
        public const int OutlineDecimalPlaces = 2;

        /// <summary>
        /// TMP 文字面膨胀值相对于最终描边值的比例。
        /// </summary>
        /// <remarks>
        /// 当前设置为描边值的一半，例如描边为 0.25 时，
        /// <c>_FaceDilate</c> 为 0.125。它只影响新生成材质的目标参数，
        /// 不会回写或修改任何已有字体材质。
        /// </remarks>
        public const float FaceDilateRatio = 0.5f;

        /// <summary>
        /// 将 PSD 描边像素宽度转换为 TMP 的 <c>_OutlineWidth</c>。
        /// </summary>
        /// <param name="pixelWidth">PSD 图层样式记录的描边宽度，单位为像素。</param>
        /// <param name="fontSize">PSD 文字字号，用于把绝对像素换算成相对比例。</param>
        /// <returns>
        /// 0～1 范围内、按 <see cref="OutlineDecimalPlaces"/> 舍入后的 TMP 描边值。
        /// 描边宽度或字号小于等于 0 时返回 0。
        /// </returns>
        /// <remarks>
        /// 完整公式为：
        /// <c>Round(Clamp01(OutlineScale * pixelWidth / fontSize), OutlineDecimalPlaces)</c>。
        /// 先除以字号可以让不同字号下相同像素描边得到符合视觉比例的结果；
        /// <c>Clamp01</c> 用于保证结果不会超出 TMP 材质允许的 0～1 范围。
        /// </remarks>
        public static float ConvertOutline(float pixelWidth, float fontSize)
        {
            if (pixelWidth <= 0f || fontSize <= 0f)
            {
                return 0f;
            }

            float normalized = Mathf.Clamp01(OutlineScale * pixelWidth / fontSize);
            float roundingScale = Mathf.Pow(10f, OutlineDecimalPlaces);
            return Mathf.Round(normalized * roundingScale) / roundingScale;
        }

        /// <summary>
        /// 根据最终 TMP 描边值计算文字面的 <c>_FaceDilate</c>。
        /// </summary>
        /// <param name="outlineWidth">
        /// 已经由 <see cref="ConvertOutline"/> 转换并舍入后的 TMP 描边值。
        /// </param>
        /// <returns>描边值乘以 <see cref="FaceDilateRatio"/> 后的文字面膨胀值。</returns>
        /// <remarks>
        /// 使用最终舍入后的描边值计算，可以保证材质签名、材质比较和实际写入值完全一致，
        /// 避免由于中间浮点误差生成视觉相同但数值不同的材质变体。
        /// </remarks>
        public static float ConvertFaceDilate(float outlineWidth)
        {
            return outlineWidth * FaceDilateRatio;
        }
    }
}
