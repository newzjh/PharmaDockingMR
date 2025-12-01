using System;
using UnityEngine;

namespace ImageEffects
{

    public interface IBlur
    {
        void BlurAndBlitBuffer(RenderTexture rbuffer, RenderTexture destination, GlowSettings settings, bool highPrecision,bool bOptimitzed = true);
    }
}

