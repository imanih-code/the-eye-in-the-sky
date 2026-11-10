// Monitor de la camioneta: toma la imagen a baja resolucion y le pone el filtro de cada camara.
// El ruido es casi todo multiplicativo: una sala a oscuras sigue siendo negra.
Shader "EyeSky/CctvComposite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Imagen", 2D) = "black" {}
        _OsdTex ("Rotulos", 2D) = "black" {}
        _LowRes ("Resolucion", Vector) = (800,450,0,0)
        _Scanline ("Lineas", Range(0,1)) = 0.14
        _Noise ("Ruido", Range(0,1)) = 0.08
        _NoiseFloor ("Ruido de fondo", Range(0,0.05)) = 0.006
        _Chroma ("Desplazamiento de color (px)", Range(0,3)) = 1
        _Desaturate ("Desaturar", Range(0,1)) = 0.3
        _Tint ("Tinte", Color) = (1,1,1,1)
        _Vignette ("Vineta", Range(0,1)) = 0.35
        _Tracking ("Banda de seguimiento", Range(0,1)) = 0.5
        _Static ("Estatica", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OsdTex;
            float4 _LowRes;
            float _Scanline;
            float _Noise;
            float _NoiseFloor;
            float _Chroma;
            float _Desaturate;
            float4 _Tint;
            float _Vignette;
            float _Tracking;
            float _Static;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(443.897, 441.423));
                p += dot(p, p.yx + 19.19);
                return frac((p.x + p.y) * p.x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                float rowIndex = floor(uv.y * _LowRes.y);

                float bandCenter = frac(t * 0.06);
                float band = smoothstep(0.035, 0.0, abs(uv.y - bandCenter)) * _Tracking;
                uv.x += band * (Hash(float2(rowIndex, floor(t * 18.0))) - 0.5) * 6.0 / _LowRes.x;

                float2 shift = float2(_Chroma / _LowRes.x, 0.0);
                float3 color = float3(tex2D(_MainTex, uv + shift).r,
                                      tex2D(_MainTex, uv).g,
                                      tex2D(_MainTex, uv - shift).b);

                fixed4 osd = tex2D(_OsdTex, i.uv);
                color = lerp(color, osd.rgb, osd.a);

                float luma = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(color, luma.xxx, _Desaturate) * _Tint.rgb;

                float2 pixel = floor(uv * _LowRes.xy);
                float noise = Hash(pixel + frac(t * 7.13) * 91.0);
                color *= 1.0 + (noise - 0.5) * _Noise;
                color += noise * _NoiseFloor;
                color *= 1.0 + band * 0.12;

                if (frac(i.uv.y * _LowRes.y) >= 0.5)
                    color *= 1.0 - _Scanline;

                float2 centered = i.uv * 2.0 - 1.0;
                color *= 1.0 - _Vignette * smoothstep(0.55, 1.45, dot(centered, centered));

                if (_Static > 0.0)
                {
                    float snow = Hash(pixel + frac(t * 23.7) * 57.0);
                    color = lerp(color, snow.xxx * 0.8, _Static);
                }
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
