/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MK.EdgeDetection.PostProcessing.Generic
{
	public static class Compatibility
    {
        private static readonly bool _defaultHDRFormatSupported = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR);
        private static readonly bool _11R11G10BFormatSupported = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float);
        private static readonly bool _2A10R10G10BFormatSupported = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB2101010);
        public static readonly bool copyTextureSupported = SystemInfo.copyTextureSupport != UnityEngine.Rendering.CopyTextureSupport.None;

        public static bool CheckGeometryShaderSupport()
        {
            return SystemInfo.supportsGeometryShaders;
        }

        public static bool CheckComputeShaderSupport()
        {
            return SystemInfo.supportsComputeShaders;
        }

        public static RenderTextureFormat GetHDRSupportedFormatIfPossible()
        {
            //return _defaultHDRFormatSupported ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            return _11R11G10BFormatSupported ? RenderTextureFormat.RGB111110Float : _2A10R10G10BFormatSupported ? RenderTextureFormat.ARGB2101010 : _defaultHDRFormatSupported ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }
    }
}
