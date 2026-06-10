Shader "Hidden/HTraceAO/MotionVectorsURP"
{
    SubShader
    {
        Pass
        {
            Cull Off 
            ZWrite Off 

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "../../Headers/HMain.hlsl"

            H_TEXTURE(_ObjectMotionVectors);
            H_TEXTURE(_ObjectMotionVectorsDepth);

            float _BiasOffset;
            
            struct Attributes
            {
                uint VertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 PositionCS : SV_POSITION;
                float2 TexCoord   : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            struct FragOutput
            {
                float2 MotionVectors : SV_Target0;
                float Mask : SV_Target1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                output.PositionCS = GetFullScreenTriangleVertexPosition(input.VertexID);
                output.TexCoord = GetFullScreenTriangleTexCoord(input.VertexID);

                return output;
            }

            FragOutput frag(Varyings input)
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                FragOutput output = (FragOutput)0;
                
                float2 ObjectMotionVectorsColor = H_LOAD(_ObjectMotionVectors, input.PositionCS.xy).xy;
                float ObjectMotionVectorsDepth = H_LOAD(_ObjectMotionVectorsDepth, input.PositionCS.xy).x;
                float CameraDepth = LoadSceneDepth(input.PositionCS.xy);

                #if !UNITY_REVERSED_Z
                    CameraDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, LoadSceneDepth(input.PositionCS.xy).x);
                #endif
                
                if (ObjectMotionVectorsDepth >= CameraDepth + _BiasOffset)
                {
                    output.MotionVectors = ObjectMotionVectorsColor;
                    output.Mask = 1;
                    return output;
                }

                // Reconstruct world position
                float3 PositionWS = ComputeWorldSpacePosition(input.PositionCS.xy * _ScreenSize.zw, CameraDepth, UNITY_MATRIX_I_VP);

                // Multiply with current and previous non-jittered view projection
                float4 PositionCS = mul(H_MATRIX_VP, float4(PositionWS.xyz, 1.0));
                float4 PreviousPositionCS = mul(H_MATRIX_PREV_VP, float4(PositionWS.xyz, 1.0));

                // Non-uniform raster needs to keep the posNDC values in float to avoid additional conversions
                // since uv remap functions use floats
                float2 PositionNDC = PositionCS.xy * rcp(PositionCS.w);
                float2 PreviousPositionNDC = PreviousPositionCS.xy * rcp(PreviousPositionCS.w);
                
                // Calculate forward velocity
                float2 Velocity = (PositionNDC - PreviousPositionNDC);

                // TODO: test that velocity.y is correct
                #if UNITY_UV_STARTS_AT_TOP
                    Velocity.y = -Velocity.y;
                #endif

                // Convert velocity from NDC space (-1..1) to screen UV 0..1 space
                // Note: It doesn't mean we don't have negative values, we store negative or positive offset in the UV space.
                // Note: ((posNDC * 0.5 + 0.5) - (prevPosNDC * 0.5 + 0.5)) = (velocity * 0.5)
                Velocity.xy *= 0.5;
             
                output.MotionVectors = Velocity;
                output.Mask = 0;
                
                return output;
            }

            ENDHLSL
        }
    }
}
