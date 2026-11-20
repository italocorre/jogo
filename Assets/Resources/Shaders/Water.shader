Shader "MarAberto/Water"
{
    Properties
    {
        _Shallow ("Água rasa", Color) = (0.16, 0.80, 0.74, 1)
        _Deep ("Água funda", Color) = (0.02, 0.23, 0.42, 1)
        _Foam ("Espuma", Color) = (0.95, 0.98, 1.0, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "MA_Common.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            float4 _Shallow, _Deep, _Foam;

            struct appdata { float4 vertex : POSITION; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float2 extra : TEXCOORD3; // x = altura da onda, y = profundidade na câmera
                UNITY_FOG_COORDS(4)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float dist = distance(wp.xz, _WorldSpaceCameraPos.xz);
                float fade = saturate(1.0 - (dist - 350.0) / 1200.0);
                float3 t = float3(1, 0, 0);
                float3 b = float3(0, 0, 1);
                float3 g = MA_Gerstner(_MA_Wave0, wp, t, b, fade);
                g += MA_Gerstner(_MA_Wave1, wp, t, b, fade);
                g += MA_Gerstner(_MA_Wave2, wp, t, b, fade);
                g += MA_Gerstner(_MA_Wave3, wp, t, b, fade);
                float3 p = wp + g;
                o.wpos = p;
                o.normal = normalize(cross(b, t));
                o.pos = mul(UNITY_MATRIX_VP, float4(p, 1.0));
                o.screenPos = ComputeScreenPos(o.pos);
                o.extra = float2(g.y, -mul(UNITY_MATRIX_V, float4(p, 1.0)).z);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float DetailHeight(float2 p, float t)
            {
                float h = MA_ValueNoise(p * 0.55 + float2(t * 0.35, t * 0.22));
                h += MA_ValueNoise(p * 1.3 - float2(t * 0.28, -t * 0.4)) * 0.5;
                h += MA_ValueNoise(p * 3.1 + float2(-t * 0.5, t * 0.6)) * 0.25;
                return h;
            }

            fixed4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float camDist = length(_WorldSpaceCameraPos - i.wpos);

                // Normal das ondas + ondinhas de detalhe.
                float3 n = normalize(i.normal);
                float detail = saturate(1.0 - camDist / 220.0) * 0.35;
                float2 uv = i.wpos.xz;
                float e = 0.2;
                float h0 = DetailHeight(uv, _MA_Time);
                float hx = DetailHeight(uv + float2(e, 0), _MA_Time);
                float hz = DetailHeight(uv + float2(0, e), _MA_Time);
                n = normalize(n + float3(h0 - hx, 0, h0 - hz) / e * detail);

                float3 sunDir = normalize(_MA_SunDir.xyz);
                float light = lerp(0.12, 1.0, _MA_Day);
                bool fromBelow = facing < 0 || _MA_Underwater > 0.5;

                if (fromBelow)
                {
                    // Olhando a superfície de baixo: janela de luz (Snell) e reflexo total nas bordas.
                    float cosT = saturate(-dot(n, v));
                    float window = smoothstep(0.55, 0.85, cosT);
                    float3 above = MA_SkyColor(float3(-v.x, abs(v.y), -v.z)) * 1.15 + _MA_SunColor.rgb * pow(saturate(dot(-v, sunDir)), 40.0) * 2.0;
                    float3 col = lerp(unity_FogColor.rgb * 0.9, above, window);
                    UNITY_APPLY_FOG(i.fogCoord, col);
                    return fixed4(col, 1.0);
                }

                // Profundidade da água atrás deste pixel (para transparência e espuma da praia).
                float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos)));
                float waterDepth = max(0.0, sceneZ - i.extra.y);

                float absorb = 1.0 - exp(-waterDepth * 0.28);
                float3 water = lerp(_Shallow.rgb, _Deep.rgb, absorb);
                // Luz atravessando a crista das ondas (tom turquesa).
                float crest = saturate(i.extra.x * 0.7 + 0.3);
                water += _Shallow.rgb * crest * 0.18 * saturate(dot(sunDir, -v) * 0.5 + 0.6);
                float diffuse = saturate(dot(n, sunDir)) * 0.35 + 0.65;
                water *= diffuse * light;

                // Reflexo do céu (Fresnel)
                float ndv = saturate(dot(n, v));
                float fresnel = 0.03 + 0.97 * pow(1.0 - ndv, 5.0);
                float3 refl = MA_SkyColor(reflect(-v, n));
                float3 col = lerp(water, refl, fresnel * 0.85);

                // Brilho do sol
                float3 hv = normalize(sunDir + v);
                float spec = pow(saturate(dot(n, hv)), 350.0) * 3.0 + pow(saturate(dot(n, hv)), 60.0) * 0.15;
                col += _MA_SunColor.rgb * spec * step(0.0, sunDir.y);

                // Espuma: beira da praia e crista das ondas.
                float foamNoise = MA_ValueNoise(uv * 1.7 + float2(_MA_Time * 0.4, 0)) * 0.6 + MA_ValueNoise(uv * 4.3 - _MA_Time * 0.3) * 0.4;
                float shore = saturate(1.0 - waterDepth / 1.3);
                float shoreBands = saturate(sin(waterDepth * 6.0 - _MA_Time * 2.0) * 0.5 + 0.5);
                float foam = smoothstep(0.35, 0.6, shore * (0.6 + shoreBands * 0.6) * foamNoise * 1.8);
                foam += smoothstep(0.55, 0.75, (i.extra.x - 0.55) * 1.6 + foamNoise * 0.6) * 0.8;
                foam = saturate(foam) * saturate(1.0 - camDist / 600.0);
                col = lerp(col, _Foam.rgb * light, foam);

                float alpha = saturate(0.35 + absorb * 1.3 + fresnel);
                alpha = max(alpha, foam);
                alpha *= saturate(waterDepth * 4.0); // borda suave na areia

                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
