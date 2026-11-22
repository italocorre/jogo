Shader "MarAberto/Sky"
{
    Properties { }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "MA_Common.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 col = MA_SkyColor(dir);
                float3 sunDir = normalize(_MA_SunDir.xyz);

                // Sol
                float sd = dot(dir, sunDir);
                col += _MA_SunColor.rgb * smoothstep(0.99955, 0.99975, sd) * 12.0;      // disco
                col += _MA_SunColor.rgb * pow(saturate(sd), 900.0) * 2.0;                // brilho em volta

                // Lua e estrelas
                float night = smoothstep(0.55, 1.0, 1.0 - _MA_Day);
                float3 moonDir = normalize(float3(-sunDir.x, -sunDir.y, 0.2));
                float md = dot(dir, moonDir);
                col += float3(0.85, 0.9, 1.0) * smoothstep(0.9993, 0.9996, md) * night * 1.5;
                col += float3(0.4, 0.5, 0.8) * pow(saturate(md), 200.0) * night * 0.4;
                if (dir.y > 0.0)
                {
                    float2 g = floor(float2(atan2(dir.z, dir.x), asin(dir.y)) * 160.0);
                    float star = step(0.9965, MA_Hash(g)) * MA_Hash(g + 7.0) * (0.75 + 0.25 * sin(_MA_Time * 3.0 + MA_Hash(g + 3.0) * 40.0));
                    col += star * night * saturate(dir.y * 5.0) * 0.9;
                }

                // Nuvens com volume: base mais escura, bordas iluminadas pelo sol, duas camadas.
                if (dir.y > 0.0)
                {
                    float2 cp = dir.xz / (dir.y + 0.09) * 0.8;
                    float2 wind = float2(_MA_Time * 0.006, _MA_Time * 0.003);
                    float2 warp = float2(MA_Fbm(cp * 0.7 + wind * 0.5), MA_Fbm(cp * 0.7 + 5.2 - wind * 0.5)) * 0.6;
                    float dens = MA_Fbm(cp * 1.3 + warp + wind);
                    float cover = lerp(0.52, 0.22, _MA_Cloud) + 0.05 * sin(_MA_Time * 0.01);   // mais nuvem com tempo fechado
                    float c = smoothstep(cover, cover + 0.22, dens);
                    // "Espessura" na direção do sol: quanto mais nuvem entre o ponto e o sol, mais escura a base.
                    float toward = MA_Fbm(cp * 1.3 + warp + wind + sunDir.xz * 0.12);
                    float shadow = saturate((toward - dens) * 3.0 + 0.5);
                    float3 lit = lerp(float3(1, 1, 1), _MA_SunColor.rgb * 1.1, 0.35);
                    float3 dark = lerp(_MA_SkyHorizon.rgb * 0.55, float3(0.55, 0.58, 0.64), 0.5) * lerp(1.0, 0.45, _MA_Cloud);
                    lit = lerp(lit, float3(0.62, 0.65, 0.7), _MA_Cloud * 0.7);
                    float3 cloudCol = lerp(lit, dark, saturate(shadow * 0.8 + c * 0.25));
                    cloudCol += _MA_SunColor.rgb * pow(saturate(sd), 10.0) * (1.0 - c) * 0.8; // borda prateada
                    cloudCol *= lerp(0.06, 1.0, _MA_Day);
                    float fadeH = saturate(dir.y * 4.0);
                    col = lerp(col, cloudCol, c * fadeH * 0.92);
                    // Camada alta, fininha (cirros).
                    float ci = smoothstep(0.55, 0.85, MA_Fbm(float2(cp.x * 0.35, cp.y * 2.2) + wind * 2.0));
                    col = lerp(col, float3(1, 1, 1) * lerp(0.05, 0.95, _MA_Day), ci * 0.25 * fadeH * (1.0 - c));
                }
                // Tempo fechado: céu acinzentado; relâmpago clareia tudo.
                col = lerp(col, dot(col, float3(0.3, 0.59, 0.11)) * float3(0.85, 0.88, 0.92), _MA_Cloud * 0.6);
                col += _MA_Flash * float3(0.8, 0.85, 1.0);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
