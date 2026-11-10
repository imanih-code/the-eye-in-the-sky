// Contorno fino de siluetas: oscurece el pixel del objeto cercano donde la profundidad salta.
// Solo multiplica, asi que nunca revela nada dentro de una sala a oscuras.
Shader "EyeSky/FeedOutline"
{
    Properties
    {
        _EdgeMeters ("Salto de profundidad (m)", Float) = 0.14
        _EdgeDarken ("Oscurecido", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EdgeMeters;
            float _EdgeDarken;

            float DepthMeters(float2 uv)
            {
                float raw = SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
                raw = 1.0 - raw;
            #endif
                return raw * (_ProjectionParams.z - _ProjectionParams.y);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, uv);
                float2 texel = _BlitTexture_TexelSize.xy;

                float self = DepthMeters(uv);
                float farthest = max(max(DepthMeters(uv + float2(texel.x, 0.0)), DepthMeters(uv - float2(texel.x, 0.0))),
                                     max(DepthMeters(uv + float2(0.0, texel.y)), DepthMeters(uv - float2(0.0, texel.y))));
                // En negro no se dibuja: un contorno ahi delataria quien hay en la sala
                if (farthest - self > _EdgeMeters && max(color.r, max(color.g, color.b)) > 0.012)
                    color.rgb *= 1.0 - _EdgeDarken;
                return color;
            }
            ENDHLSL
        }
    }
}
