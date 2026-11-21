Shader "MarAberto/Water"
{
    Properties
    {
        _Scatter ("Cor da água", Color) = (0.03, 0.42, 0.58, 1)
        _Shallow ("Turquesa raso", Color) = (0.20, 0.85, 0.78, 1)
        _Foam ("Espuma", Color) = (0.95, 0.98, 1.0, 1)
        _Absorb ("Absorção (RGB)", Vector) = (0.42, 0.11, 0.08, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "IgnoreProjector"="True" }

        GrabPass { "_MAWaterGrab" }

        Pass
        {
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "MA_Common.cginc"

            UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
            sampler2D _MAWaterGrab;
            float4 _Scatter, _Shallow, _Foam, _Absorb;

            struct appdata { float4 vertex : POSITION; };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 wpos : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float4 grabPos : TEXCOORD3;
                float3 extra : TEXCOORD4; // x = altura da onda, y = profundidade na câmera, z = fade
                UNITY_FOG_COORDS(5)
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
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.extra = float3(g.y, -mul(UNITY_MATRIX_V, float4(p, 1.0)).z, fade);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float DetailHeight(float2 p, float t)
            {
                float h = MA_ValueNoise(p * 0.45 + float2(t * 0.30, t * 0.18));
                h += MA_ValueNoise(p * 1.1 - float2(t * 0.24, -t * 0.36)) * 0.55;
                h += MA_ValueNoise(p * 2.7 + float2(-t * 0.5, t * 0.55)) * 0.3;
                h += MA_ValueNoise(p * 6.3 + float2(t * 0.8, t * 0.3)) * 0.12;
                return h;
            }

            float SceneDepth(float4 sp)
            {
                return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(sp)));
            }

            fixed4 frag (v2f i, float facing : VFACE) : SV_Target
            {
                float3 v = normalize(_WorldSpaceCameraPos - i.wpos);
                float camDist = length(_WorldSpaceCameraPos - i.wpos);

                // Normal das ondas + ondinhas de detalhe.
                float3 n = normalize(i.normal);
                float detail = saturate(1.0 - camDist / 260.0) * 0.32;
                float2 uv = i.wpos.xz;
                float e = 0.15;
                float h0 = DetailHeight(uv, _MA_Time);
                float hx = DetailHeight(uv + float2(e, 0), _MA_Time);
                float hz = DetailHeight(uv + float2(0, e), _MA_Time);
                n = normalize(n + float3(h0 - hx, 0, h0 - hz) / e * detail);

                float3 sunDir = normalize(_MA_SunDir.xyz);
                float light = lerp(0.1, 1.0, _MA_Day);
                bool fromBelow = facing < 0 || _MA_Underwater > 0.5;

                if (fromBelow)
                {
                    // Olhando a superfície de baixo: janela de luz (Snell) e reflexo total nas bordas.
                    float cosT = saturate(-dot(n, v));
                    float window = smoothstep(0.5, 0.8, cosT);
                    float3 above = MA_SkyColor(float3(-v.x, abs(v.y), -v.z)) * 1.2
                                 + _MA_SunColor.rgb * pow(saturate(dot(-v, sunDir)), 30.0) * 3.0;
                    float3 col = lerp(unity_FogColor.rgb, above, window);
                    UNITY_APPLY_FOG(i.fogCoord, col);
                    return fixed4(col, 1.0);
                }

                // ---------- Refração: o que está debaixo d'água ----------
                float eye = i.extra.y;
                float sceneZ = SceneDepth(i.screenPos);
                float depthStraight = max(0.0, sceneZ - eye);
                float2 distort = n.xz * 0.045 * saturate(depthStraight * 0.6);
                float4 sp2 = i.screenPos; sp2.xy += distort * sp2.w;
                float4 gp2 = i.grabPos;   gp2.xy += distort * gp2.w;
                float sceneZ2 = SceneDepth(sp2);
                if (sceneZ2 < eye) { sp2 = i.screenPos; gp2 = i.grabPos; sceneZ2 = sceneZ; }
                float waterDepth = max(0.0, sceneZ2 - eye);
                float3 refr = tex2Dproj(_MAWaterGrab, UNITY_PROJ_COORD(gp2)).rgb;

                // Luz atravessando a água: vermelho some primeiro (fica turquesa / azul).
                float3 trans = exp(-waterDepth * _Absorb.xyz);
                float3 scatter = lerp(_Shallow.rgb, _Scatter.rgb, saturate(waterDepth / 14.0)) * light;
                // Brilho "por dentro" da onda quando o sol está atrás dela.
                float sss = pow(saturate(dot(v, -sunDir) * 0.6 + 0.4), 3.0) * saturate(i.extra.x * 0.8 + 0.4);
                scatter += _Shallow.rgb * sss * 0.35 * light;
                float3 water = refr * trans + scatter * (1.0 - trans);

                // Reflexo do céu (Fresnel)
                float ndv = saturate(dot(n, v));
                float fresnel = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);
                float3 refl = MA_SkyColor(reflect(-v, n));
                float3 col = lerp(water, refl, fresnel * 0.9);

                // Brilho do sol (no HDR vira "brilhinho" com o bloom)
                float3 hv = normalize(sunDir + v);
                float nh = saturate(dot(n, hv));
                float spec = pow(nh, 900.0) * 18.0 + pow(nh, 90.0) * 0.35;
                col += _MA_SunColor.rgb * spec * step(0.0, sunDir.y);

                // Espuma: linhas na beira da praia e um pouco nas cristas.
                float fn = MA_ValueNoise(uv * 1.3 + float2(_MA_Time * 0.25, 0)) * 0.55 + MA_ValueNoise(uv * 3.7 - _MA_Time * 0.2) * 0.45;
                float shore = saturate(1.0 - depthStraight / 1.6);
                float bands = saturate(sin(depthStraight * 5.0 - _MA_Time * 1.6) * 0.5 + 0.5);
                float foam = smoothstep(0.42, 0.62, shore * (0.55 + bands * 0.5) * fn * 1.7);
                foam += smoothstep(0.25, 0.04, depthStraight) * 0.7;
                float crest = (i.extra.x - 0.85) * 1.2 + (fn - 0.5) * 0.7;
                foam += smoothstep(0.15, 0.45, crest) * 0.45 * i.extra.z;
                foam = saturate(foam) * saturate(1.0 - camDist / 500.0);
                col = lerp(col, _Foam.rgb * light * 1.1, foam);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
