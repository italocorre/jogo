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
                col += _MA_SunColor.rgb * smoothstep(0.9990, 0.9995, sd) * 6.0;
                col += _MA_SunColor.rgb * pow(saturate(sd), 300.0) * 0.6;

                // Lua e estrelas
                float night = smoothstep(0.55, 1.0, 1.0 - _MA_Day);
                float3 moonDir = normalize(float3(-sunDir.x, -sunDir.y, 0.2));
                float md = dot(dir, moonDir);
                col += float3(0.85, 0.9, 1.0) * smoothstep(0.9993, 0.9996, md) * night * 1.5;
                col += float3(0.4, 0.5, 0.8) * pow(saturate(md), 200.0) * night * 0.4;
                if (dir.y > 0.0)
                {
                    float2 g = floor(float2(atan2(dir.z, dir.x), asin(dir.y)) * 160.0);
                    float star = step(0.9965, MA_Hash(g)) * MA_Hash(g + 7.0);
                    col += star * night * saturate(dir.y * 5.0) * 0.9;
                }

                // Nuvens
                if (dir.y > 0.0)
                {
                    float2 cp = dir.xz / (dir.y + 0.12) * 0.9 + float2(_MA_Time * 0.004, _MA_Time * 0.002);
                    float c = MA_Fbm(cp * 1.4);
                    c = smoothstep(0.48, 0.72, c);
                    float shade = MA_Fbm(cp * 1.4 + sunDir.xz * 0.08);
                    float3 cloudCol = lerp(float3(1, 1, 1), _MA_SkyHorizon.rgb * 0.9, saturate(shade - c * 0.3));
                    cloudCol = cloudCol * lerp(0.12, 1.0, _MA_Day) + _MA_SunColor.rgb * pow(saturate(sd), 6.0) * 0.4;
                    col = lerp(col, cloudCol, c * saturate(dir.y * 5.0) * 0.85);
                }
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
