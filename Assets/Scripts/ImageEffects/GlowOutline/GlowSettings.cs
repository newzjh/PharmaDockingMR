using System;
using UnityEngine;
namespace ImageEffects
{
    public enum Resolution
    {
        Full = 1,
        Half = 2,
        Quarter = 4
    }

    public enum BlendMode
    {
        Additive,
        Screen
    }

    public enum GlowTarget
    {
        /// <summary>
        /// 自己
        /// </summary>
        Self,

        /// <summary>
        /// 代码调用
        /// </summary>
        OutSidePointed,
    }

    public enum BlurMode
    {
        // Advanced = 5,
        Default = 0,
        HighQuality = 10,
        UnityBlur = 100
    }

    [Serializable]
    public class GlowSettings
    {
        public Resolution baseResolution = Resolution.Full;
        public BlendMode blendMode = BlendMode.Additive;
        public float blurSpread = 0.6f;
        public float boostStrength = 1f;
        public DownsampleBlendMode downsampleBlendMode = DownsampleBlendMode.Max;
        public DownsampleResolution downsampleResolution = DownsampleResolution.Quarter;
        public int downsampleSteps = 1;
        public AnimationCurve falloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public float falloffScale = 1f;
        public float innerStrength = 1f;
        public int iterations = 3;
        public bool normalize = true;
        public float outerStrength = 1f;
        public int radius = 4;

        //以下是高质量等级的参数

        public bool UseHightQualityGlow = false;


        public float HighQualityGlowIntensity = 0.05f;
        public float HighQualityGlowBlurSize = 4.0f;

    }
}

