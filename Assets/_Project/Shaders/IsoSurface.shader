// Superficie del mundo vista por las camaras: luz en bandas planas y nada de brillos.
// La luz sale de datos globales (RoomLighting), asi que imagen y simulacion leen lo mismo.
Shader "EyeSky/IsoSurface"
{
    Properties
    {
        [MainColor] _BaseColor ("Color", Color) = (1,1,1,1)
        _Color2 ("Color 2", Color) = (0.5,0.5,0.5,1)
        _Color3 ("Color 3", Color) = (0.2,0.2,0.2,1)
        [Enum(Flat,0,Parquet,1,Wall,2,Tile,3)] _Pattern ("Patron", Float) = 0
        _Emission ("Emision", Color) = (0,0,0,1)
        [Toggle] _EmissionNeedsPower ("Se apaga con la sala", Float) = 0
        _Heat ("Calor (IR)", Range(0,1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 0
        [Enum(Off,0,On,1)] _ZWrite ("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define EYE_MAX_ROOMS 16
            #define EYE_MAX_LAMPS 48
            #define EYE_MAX_SPOTS 8

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Color2;
                float4 _Color3;
                float _Pattern;
                float4 _Emission;
                float _EmissionNeedsPower;
                float _Heat;
                float _SrcBlend;
                float _DstBlend;
                float _ZWrite;
            CBUFFER_END

            float4 _Eye_RoomRects[EYE_MAX_ROOMS];   // minX, minZ, maxX, maxZ
            float _Eye_RoomLevels[EYE_MAX_ROOMS];
            float _Eye_RoomCount;
            float4 _Eye_Lamps[EYE_MAX_LAMPS];       // posicion, radio
            float4 _Eye_LampInfo[EYE_MAX_LAMPS];    // sala, potencia, emergencia
            float _Eye_LampCount;
            float4 _Eye_SpotPos[EYE_MAX_SPOTS];     // posicion, alcance
            float4 _Eye_SpotDir[EYE_MAX_SPOTS];     // direccion, coseno del angulo
            float4 _Eye_SpotRect[EYE_MAX_SPOTS];
            float _Eye_SpotPower[EYE_MAX_SPOTS];
            float _Eye_SpotCount;
            float _Eye_Ambient;
            float _Eye_OutsideLevel;
            float _Eye_Bands;
            float _Eye_Dither;
            float _Eye_ShadowStrength;
            float4 _Eye_DarkColor;
            float _Eye_IRMode;
            float4 _Eye_IRCenter;                   // centro, alcance
            float4 _Eye_ViewRect;                   // lo que abarca la camara activa
            float _Eye_ViewRoom;                    // sala de la camara activa

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Bayer4(float2 pixel)
            {
                static const float matrixValues[16] =
                {
                    0.0, 8.0, 2.0, 10.0,
                    12.0, 4.0, 14.0, 6.0,
                    3.0, 11.0, 1.0, 9.0,
                    15.0, 7.0, 13.0, 5.0
                };
                int x = (int)pixel.x & 3;
                int y = (int)pixel.y & 3;
                return (matrixValues[y * 4 + x] + 0.5) / 16.0;
            }

            // Las juntas miden al menos 0.04 m: a 40 px/m y 2:1 es lo minimo para que no se corten
            float3 Parquet(float2 xz)
            {
                const float width = 0.26;
                const float length = 1.3;
                const float seam = 0.04;
                float row = floor(xz.y / width);
                float offset = Hash21(float2(row, 7.0)) * length;
                float id = floor((xz.x + offset) / length);
                float3 color = lerp(_BaseColor.rgb, _Color2.rgb, Hash21(float2(row, id)));
                float inRow = frac(xz.y / width) * width;
                float inPlank = frac((xz.x + offset) / length) * length;
                return (inRow < seam || inPlank < seam) ? _Color3.rgb : color;
            }

            float3 Tile(float2 xz)
            {
                const float size = 0.8;
                const float seam = 0.04;
                float2 cell = floor(xz / size);
                float2 inCell = frac(xz / size) * size;
                if (inCell.x < seam || inCell.y < seam)
                    return _Color3.rgb;
                return fmod(abs(cell.x + cell.y), 2.0) < 0.5 ? _BaseColor.rgb : _Color2.rgb;
            }

            // Pared: zocalo, friso de madera con paneles, moldura y pintura arriba
            float3 Wall(float3 p, float3 n)
            {
                if (n.y > 0.5)
                    return _Color3.rgb * 0.35;
                const float wainscot = 0.95;
                if (p.y < 0.12)
                    return _Color2.rgb * 0.55;
                if (p.y >= wainscot && p.y < wainscot + 0.08)
                    return _Color3.rgb;
                if (p.y >= wainscot + 0.08)
                    return _BaseColor.rgb;
                float along = frac((p.x + p.z) / 0.9) * 0.9;
                if (along < 0.05 || p.y > wainscot - 0.05 || p.y < 0.17)
                    return _Color2.rgb * 0.72;
                return _Color2.rgb;
            }

            float PlanarSpot(int i, float3 p)
            {
                float4 rect = _Eye_SpotRect[i];
                if (p.x < rect.x || p.z < rect.y || p.x > rect.z || p.z > rect.w)
                    return 0.0;
                float2 toPoint = p.xz - _Eye_SpotPos[i].xz;
                float distanceToPoint = length(toPoint);
                float range = _Eye_SpotPos[i].w;
                if (distanceToPoint >= range)
                    return 0.0;
                float spill = distanceToPoint < 0.7 ? 0.32 : 0.0;
                if (distanceToPoint < 0.001)
                    return spill;
                float aligned = dot(toPoint / distanceToPoint, normalize(_Eye_SpotDir[i].xz));
                if (aligned < _Eye_SpotDir[i].w)
                    return spill;
                return max(spill, (1.0 - distanceToPoint / range) * 0.75 + 0.35);
            }

            float Quantize(float intensity, float2 pixel)
            {
                if (intensity <= 0.001)
                    return 0.0;
                float scaled = intensity * _Eye_Bands;
                float baseBand = floor(scaled);
                float fraction = scaled - baseBand;
                float threshold = 1.0 - _Eye_Dither;
                float ramp = saturate((fraction - threshold) / max(_Eye_Dither, 0.001));
                return (baseBand + (ramp > Bayer4(pixel) ? 1.0 : 0.0)) / _Eye_Bands;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                // Una persona fuera de lo que abarca la camara no se pinta
                if (_Eye_ViewRect.z > _Eye_ViewRect.x &&
                    (p.x < _Eye_ViewRect.x || p.z < _Eye_ViewRect.y || p.x > _Eye_ViewRect.z || p.z > _Eye_ViewRect.w))
                    discard;

                float3 n = normalize(input.normalWS);
                float3 vertexColor = FastSRGBToLinear(input.color.rgb);

                if (_Eye_IRMode > 0.5)
                {
                    float fade = saturate(1.0 - length(p.xz - _Eye_IRCenter.xz) / _Eye_IRCenter.w);
                    float heat = _Heat * smoothstep(0.0, 0.45, fade);
                    float room = 0.012 + 0.02 * saturate(n.y) * fade;
                    float ir = room + heat * (0.55 + 0.35 * saturate(n.y + 0.4));
                    return float4(ir, ir, ir, _BaseColor.a);
                }

                float3 albedo;
                if (_Pattern > 2.5)
                    albedo = Tile(p.xz);
                else if (_Pattern > 1.5)
                    albedo = Wall(p, n);
                else if (_Pattern > 0.5)
                    albedo = Parquet(p.xz);
                else
                    albedo = _BaseColor.rgb;
                albedo *= vertexColor;

                int room = -1;
                float level = _Eye_OutsideLevel;
                float edgeDistance = 10.0;
                for (int i = 0; i < (int)_Eye_RoomCount; i++)
                {
                    float4 rect = _Eye_RoomRects[i];
                    if (p.x >= rect.x && p.z >= rect.y && p.x <= rect.z && p.z <= rect.w)
                    {
                        room = i;
                        level = _Eye_RoomLevels[i];
                        edgeDistance = min(min(p.x - rect.x, rect.z - p.x), min(p.z - rect.y, rect.w - p.z));
                        // La sala que se esta mirando manda sobre la vecina en los muros compartidos
                        if (i == (int)_Eye_ViewRoom)
                            break;
                    }
                }

                float light = level * _Eye_Ambient;
                for (int i = 0; i < (int)_Eye_LampCount; i++)
                {
                    float4 info = _Eye_LampInfo[i];
                    if ((int)info.x != room)
                        continue;
                    float reach = saturate(1.0 - length(p.xz - _Eye_Lamps[i].xz) / _Eye_Lamps[i].w);
                    light += reach * reach * info.y * (info.z > 0.5 ? 1.0 : level);
                }

                // Tres tonos, como una baldosa pintada: arriba, cara izquierda, cara derecha
                float side = 0.74 + 0.16 * dot(n.xz, normalize(float2(-1.0, 0.45)));
                float tone = lerp(side, 1.0, saturate(n.y));

                float shadow = MainLightRealtimeShadow(TransformWorldToShadowCoord(p));
                light *= tone * lerp(1.0 - _Eye_ShadowStrength, 1.0, shadow);

                // La sombra de las esquinas va pintada en el suelo
                if (_Pattern > 0.5 && _Pattern < 1.5 || _Pattern > 2.5)
                    light *= lerp(0.72, 1.0, saturate(edgeDistance / 0.55));

                for (int i = 0; i < (int)_Eye_SpotCount; i++)
                    light += PlanarSpot(i, p) * _Eye_SpotPower[i] * tone;

                float band = Quantize(light, input.positionCS.xy);
                float3 tint = lerp(float3(0.84, 0.86, 0.98), float3(1.06, 1.0, 0.92), saturate(band));
                float3 color = albedo * band * tint + _Eye_DarkColor.rgb;
                color += _Emission.rgb * (_EmissionNeedsPower > 0.5 ? level : 1.0);
                return float4(color, _BaseColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert(Attributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            float4 frag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Eye_ViewRect;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(float4 positionOS : POSITION)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS;
                if (_Eye_ViewRect.z > _Eye_ViewRect.x &&
                    (p.x < _Eye_ViewRect.x || p.z < _Eye_ViewRect.y || p.x > _Eye_ViewRect.z || p.z > _Eye_ViewRect.w))
                    discard;
                return 0;
            }
            ENDHLSL
        }
    }
}
