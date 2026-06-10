/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MK.EdgeDetection.PostProcessing.Generic
{
    public interface ICameraData
    {
        public bool xrEnvironment { get; }
        public int width { get; }
        public int height { get; }
        public int widthRaw { get; }
        public int heightRaw { get; }
        public bool stereoEnabled { get; }
        public float aspect { get; }
        public Matrix4x4 viewMatrix { get; }
        public Matrix4x4 projectionMatrix { get; }
        public bool hasTargetTexture { get; }
        public bool useCustomRenderTargetSetup { get; }
        public UnityEngine.Rendering.TextureDimension customRenderTargetDimension { get; }
        public int customRenderTargetVolumeDepth { get; }
        public bool isSceneView { get; }
        public bool allowDynamicResolution { get; }
    }
}
