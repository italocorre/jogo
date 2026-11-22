Shader "MarAberto/Water"
{
    Properties
    {
        _Scatter ("Cor da água funda", Color) = (0.015, 0.11, 0.17, 1)
        _Shallow ("Turquesa raso", Color) = (0.12, 0.48, 0.46, 1)
        _Foam ("Espuma", Color) = (0.92, 0.95, 0.96, 1)
        _Absorb ("Absorção (RGB)", Vector) = (0.45, 0.09, 0.06, 0)
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
                // Ondinhas em várias escalas (perto e longe), para o mar não ficar liso.
                float detail = saturate(1.0 - camDist / 500.0) * 0.17 + 0.025;
                float2 uv = i.wpos.xz;
                float e = 0.12 + camDist * 0.002;
                float h0 = DetailHeight(uv, _MA_Time);
                float hx = DetailHeight(uv + float2(e, 0), _MA_Time);
                float hz = DetailHeight(uv + float2(0, e), _MA_Time);
                n = normalize(n + float3(h0 - hx, 0, h0 - hz) / e * detail);
                float2 uvB = uv * 0.08;
                float b0 = MA_ValueNoise(uvB + _MA_Time * 0.03), bx = MA_ValueNoise(uvB + float2(0.05, 0) + _MA_Time * 0.03), bz = MA_ValueNoise(uvB + float2(0, 0.05) + _MA_Time * 0.03);
                n = normalize(n + float3(b0 - bx, 0, b0 - bz) * 2.5);

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

                // Luz atravessando a água: o vermelho some primeiro (raso turquesa, fundo azul-escuro).
                float3 trans = exp(-waterDepth * _Absorb.xyz);
                float3 ambient = lerp(float3(0.03, 0.04, 0.07), float3(1, 1, 1), _MA_Day);
                float sunUp = saturate(sunDir.y * 3.0);
                float3 scatter = lerp(_Shallow.rgb, _Scatter.rgb, saturate(waterDepth / 10.0)) * ambient;
                // Luz passando por dentro da crista quando o sol está atrás dela.
                float sss = pow(saturate(dot(v, -sunDir)), 4.0) * saturate(i.extra.x * 0.7 + 0.2) * sunUp;
                scatter += _Shallow.rgb * sss * 0.5;
                float3 water = refr * trans + scatter * (1.0 - trans);

                // Reflexo do céu (Fresnel físico da água).
                float ndv = saturate(dot(n, v));
                float fresnel = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);
                float3 refl = MA_SkyColor(reflect(-v, n));
                float3 col = lerp(water, refl, saturate(fresnel));

                // Brilho do sol: ponto forte e nítido + faixa de reflexo larga.
                float3 hv = normalize(sunDir + v);
                float nh = saturate(dot(n, hv));
                float spec = pow(nh, 1400.0) * 2.5 * saturate(1.0 - camDist / 250.0) + pow(nh, 180.0) * 0.18;
                col += _MA_SunColor.rgb * spec * sunUp;

                // Espuma: só uma linha fina e quebrada na beira (e um pouco nas cristas fortes), acesa pela luz do momento.
                float fn = MA_ValueNoise(uv * 1.6 + float2(_MA_Time * 0.25, 0)) * 0.6 + MA_ValueNoise(uv * 5.3 - _MA_Time * 0.2) * 0.4;
                float edge = smoothstep(0.35, 0.0, depthStraight);
                float bands = saturate(sin(depthStraight * 9.0 - _MA_Time * 1.8) * 0.5 + 0.5) * smoothstep(1.2, 0.2, depthStraight);
                float foam = edge * smoothstep(0.35, 0.65, fn) + bands * smoothstep(0.6, 0.8, fn) * 0.5;
                float crest = (i.extra.x - 1.1) * 1.2 + (fn - 0.5) * 0.6;
                foam += smoothstep(0.2, 0.5, crest) * 0.3 * i.extra.z;
                foam = saturate(foam) * saturate(1.0 - camDist / 400.0) * 0.75;
                float3 foamLight = ambient * 0.55 + _MA_SunColor.rgb * sunUp * 0.6;
                col = lerp(col, _Foam.rgb * foamLight, foam);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
