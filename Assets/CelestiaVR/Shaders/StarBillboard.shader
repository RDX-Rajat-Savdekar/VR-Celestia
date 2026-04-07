Shader "CelestiaVR/StarBillboard"
{
    Properties
    {
        _Color ("Star Color", Color) = (1,1,1,1)
        _Brightness ("Brightness", Range(0,1)) = 0.5
        _GlowFalloff ("Glow Falloff", Range(1, 8)) = 3
        _CoreSize ("Core Size", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "StarBillboard"
            Blend One One          // Additive blending for glow
            ZWrite Off
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
                UNITY_DEFINE_INSTANCED_PROP(float,  _Brightness)
            UNITY_INSTANCING_BUFFER_END(Props)

            float _GlowFalloff;
            float _CoreSize;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Billboard: extract camera-facing axes from the view matrix inverse
                float3 worldPos = TransformObjectToWorld(float3(0,0,0));
                float3 camRight = UNITY_MATRIX_V[0].xyz;  // view-space X in world
                float3 camUp    = UNITY_MATRIX_V[1].xyz;

                // Offset the quad vertices in camera space
                float3 vertexOffset = (IN.positionOS.x * camRight + IN.positionOS.y * camUp);
                float3 finalWorld = worldPos + vertexOffset * length(float3(
                    UNITY_MATRIX_M[0][0], UNITY_MATRIX_M[1][0], UNITY_MATRIX_M[2][0]));

                OUT.positionCS = TransformWorldToHClip(finalWorld);
                OUT.uv = IN.uv;

                float4 col = UNITY_ACCESS_INSTANCED_PROP(Props, _Color);
                float  br  = UNITY_ACCESS_INSTANCED_PROP(Props, _Brightness);
                OUT.color = float4(col.rgb * br, 1.0);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Distance from center of billboard [0,1]
                float2 uv = IN.uv * 2.0 - 1.0; // remap to [-1,1]
                float dist = length(uv);

                // Discard outside unit circle
                clip(1.0 - dist);

                // Bright core + soft glow halo
                float core = smoothstep(_CoreSize, 0.0, dist);
                float glow = pow(saturate(1.0 - dist), _GlowFalloff);
                float intensity = saturate(core + glow * 0.5);

                return half4(IN.color.rgb * intensity, intensity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
