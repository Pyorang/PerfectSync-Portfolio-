/*****************************************************
Copyright © 2024 Michael Kremmel
https://www.michaelkremmel.de
All rights reserved
*****************************************************/
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace MK.EdgeDetection.PostProcessing.Generic
{
    public static class MKBlitter
    {
        #if ENABLE_VR && MK_VR_ENABLED || UNITY_6000_5_OR_NEWER
            public static bool xrEnabled { get{ return UnityEngine.XR.XRSettings.enabled; } }
            public static bool singlePassStereoDoubleWideEnabled { get{ return xrEnabled && UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePass; } }
            public static bool singlePassStereoInstancedEnabled { get{ return xrEnabled && (UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced || UnityEngine.XR.XRSettings.stereoRenderingMode == UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassMultiview); } }
        #else
            public static bool xrEnabled { get{ return false; } }
            public static bool singlePassStereoDoubleWideEnabled { get{ return false; } }
            public static bool singlePassStereoInstancedEnabled { get{ return false; } }
        #endif

        private static readonly Vector3[] _vertexPositions = new Vector3[]
        {
            new Vector3(-1f, -1f, 0f),
            new Vector3( 3f, -1f, 0f),
            new Vector3(-1f,  3f, 0f)
        };
        private static readonly int[] _indices = new[] { 0, 1, 2 };

        private static Mesh _screenMesh;
        public static Mesh screenMesh
        {
            get
            {
                if(_screenMesh == null)
                {
                    _screenMesh = new Mesh { name = "MKShaderPostProcessingMesh" };

                    _screenMesh.SetVertices(_vertexPositions);
                    _screenMesh.SetIndices(_indices, MeshTopology.Triangles, 0, false);
                    _screenMesh.UploadMeshData(false);
                }
                
                return _screenMesh;
            }
        }

        public static void SetKeyword(UnityEngine.Material material, string keyword, bool state)
        {
            if(state)
                material.EnableKeyword(keyword);
            else
                material.DisableKeyword(keyword);
        }

        public static void SetKeyword(CommandBuffer commandBuffer, UnityEngine.Material material, GlobalKeyword globalKeyword, bool state)
        {
            if(state)
                commandBuffer.EnableKeyword(globalKeyword);
            else
                commandBuffer.DisableKeyword(globalKeyword);
        }

        public static void SetKeyword(CommandBuffer commandBuffer, UnityEngine.Material material, LocalKeyword localKeyword, bool state)
        {
            if(state)
                commandBuffer.EnableKeyword(material, localKeyword);
            else
                commandBuffer.DisableKeyword(material, localKeyword);
        }

        private static RenderTargetBinding defaultRenderTargetBinding = new RenderTargetBinding
        (
            new RenderTargetIdentifier[1], 
            new RenderBufferLoadAction[1] { RenderBufferLoadAction.DontCare },
            new RenderBufferStoreAction[1] { RenderBufferStoreAction.Store },
            0, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
        );

        private static RenderTargetSetup defaultRenderTargetSetup = new RenderTargetSetup
        (
            new RenderBuffer[1], 
            new RenderBuffer(),
            0,
            CubemapFace.Unknown,
            new RenderBufferLoadAction[1]{RenderBufferLoadAction.DontCare},
            new RenderBufferStoreAction[1]{RenderBufferStoreAction.Store},
            RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare
        ) { depthSlice = -1};

        /*
        private static readonly int _renderTargetSizeID = UnityEngine.Shader.PropertyToID("_RenderTargetSize");
        public static void UpdateSystemBuffers(CommandBuffer cmd, int renderTargetWidth, int renderTargetHeight)
        {
            cmd.SetGlobalVector(_renderTargetSizeID, new Vector2(renderTargetWidth, renderTargetHeight));
        }
        */

        public static void Draw(CommandBuffer cmd, RenderTargetIdentifier destination, UnityEngine.Material material, MaterialPropertyBlock materialPropertyBlock, int pass, Rect viewport)
        {
            defaultRenderTargetBinding.colorRenderTargets[0] = destination;
            defaultRenderTargetBinding.depthRenderTarget = destination;
            cmd.SetRenderTarget(defaultRenderTargetBinding, 0, CubemapFace.Unknown, -1);
            cmd.SetViewport(viewport);
            if(UnityEngine.SystemInfo.graphicsShaderLevel >= 35)
                cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1, materialPropertyBlock);
            else
                cmd.DrawMesh(screenMesh, Matrix4x4.identity, material, 0, pass, materialPropertyBlock);
        }

        /*
        public static void Draw(RenderTexture destination, Material material, MaterialPropertyBlock materialPropertyBlock, int pass, Rect viewport)
        {
            
            //defaultRenderTargetSetup.color[0] = destination.colorBuffer;
            //defaultRenderTargetSetup.depth = destination.depthBuffer;
            //Graphics.SetRenderTarget(defaultRenderTargetSetup);
            //GL.Viewport(viewport);
            //material.SetPass(pass);
            //Graphics.DrawMeshNow(screenMesh, Matrix4x4.identity, 0);
            

            //GL Triangles breaks compatibility. Camera gets random flipped on different platforms
            //Seems to be like an legacy internal behaviour on the cpp backend
            Graphics.SetRenderTarget(destination, 0, CubemapFace.Unknown, -1);
            GL.PushMatrix();
            GL.LoadOrtho();
            material.SetPass(pass);
            GL.Begin(GL.QUADS);
            {
                GL.TexCoord2(0, 1);
                GL.Vertex(new Vector3(0, 1, 0));
    
                GL.TexCoord2(1, 1);
                GL.Vertex(new Vector3(1, 1, 0));
    
                GL.TexCoord2(1, 0);
                GL.Vertex(new Vector3(1, 0, 0));
    
                GL.TexCoord2(0, 0);
                GL.Vertex(new Vector3(0, 0, 0));          
            }
            GL.End();
            GL.PopMatrix();
        }
        */

        private static int SinglePassStereoAdjustWidth(bool stereoEnabled, int width)
		{
			return stereoEnabled && singlePassStereoDoubleWideEnabled ? width * 2 : width;
		}

        public static void DownScaleTexureDimension(bool cameraStereoEnabled, ref int sourceWidth, ref int sourceHeight, int divider)
        {
            //using single pass stereo can introduce some Texeloffset which makes the rendering occur on the wrong place
            //This happens because the samples are build on base of different mip levels
            //single pass stereo TexelSize needs to be adjusted in the shader too
            sourceWidth = cameraStereoEnabled && singlePassStereoDoubleWideEnabled && ((sourceWidth / 2) % 2 > 0) ? 1 + sourceWidth / divider : sourceWidth / divider;
            sourceHeight = Mathf.Max(sourceHeight / divider, 1);
        }

        public static RenderTextureDescriptor GetRenderTextureDescriptor(ICameraData cameraData, int width, int height, RenderTextureFormat format, bool adjustSinglePassStereo = true)
        {
            #if ENABLE_VR && MK_VR_ENABLED || UNITY_6000_5_OR_NEWER
                RenderTextureDescriptor descriptor = cameraData.stereoEnabled && xrEnabled ? XRSettings.eyeTextureDesc : new RenderTextureDescriptor();
            #else
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor();
            #endif
            if(cameraData.useCustomRenderTargetSetup)
            {
                descriptor.dimension = cameraData.customRenderTargetDimension;
                #if ENABLE_VR && MK_VR_ENABLED || UNITY_6000_5_OR_NEWER
                    descriptor.vrUsage = cameraData.xrEnvironment && cameraData.stereoEnabled ? XRSettings.eyeTextureDesc.vrUsage : VRTextureUsage.None;
                #else
                    descriptor.vrUsage = VRTextureUsage.None;
                #endif
                descriptor.volumeDepth = cameraData.customRenderTargetVolumeDepth;
            }
            else
            {
                #if ENABLE_VR && MK_VR_ENABLED || UNITY_6000_5_OR_NEWER
                if(cameraData.xrEnvironment)
                {
                    descriptor.dimension = cameraData.stereoEnabled && !cameraData.hasTargetTexture ? XRSettings.eyeTextureDesc.dimension : UnityEngine.Rendering.TextureDimension.Tex2D;
                    descriptor.vrUsage = cameraData.stereoEnabled && !cameraData.hasTargetTexture ? XRSettings.eyeTextureDesc.vrUsage : VRTextureUsage.None;
                    descriptor.volumeDepth = cameraData.stereoEnabled && !cameraData.hasTargetTexture ? XRSettings.eyeTextureDesc.volumeDepth : 1;
                }
                else
                {
                    descriptor.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                    descriptor.vrUsage = VRTextureUsage.None;
                    descriptor.volumeDepth = 1;
                }
                #else
                    descriptor.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
                    descriptor.vrUsage = VRTextureUsage.None;
                    descriptor.volumeDepth = 1;
                #endif
            }
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
			descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.memoryless = RenderTextureMemoryless.None;
            descriptor.enableRandomWrite = false;
            descriptor.depthBufferBits = 16;
            if(adjustSinglePassStereo)
                descriptor.width = SinglePassStereoAdjustWidth(cameraData.stereoEnabled, width);
            else
                descriptor.width = width;
            descriptor.height = height;
            descriptor.colorFormat = format;
            descriptor.sRGB = RenderTextureReadWrite.Default != RenderTextureReadWrite.Linear;
			descriptor.shadowSamplingMode = UnityEngine.Rendering.ShadowSamplingMode.None;
            descriptor.useDynamicScale = false;
            return descriptor;
        }

        public static void GetTemporaryTexture(CommandBuffer cmd, int nameID, RenderTextureDescriptor descriptor)
        {
            cmd.GetTemporaryRT(nameID, descriptor);
        }

        public static void ReleaseTemporaryTexture(CommandBuffer cmd, int nameID)
        {
            cmd.ReleaseTemporaryRT(nameID);
        }

        public static RenderTexture GetTemporaryTexture(RenderTextureDescriptor descriptor)
        {
            return RenderTexture.GetTemporary(descriptor);
        }

        public static void ReleaseTemporaryTexture(RenderTexture renderTexture)
        {
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }
}