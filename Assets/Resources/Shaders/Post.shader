Shader "Hidden/MarAberto/Post"
{
    Properties
    {
        _MainTex ("", 2D) = "white" {}
        _BloomTex ("", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    float4 _Params;   // x = limiar, y = joelho, z = intensidade do bloom, w = exposição
    float4 _Grade;    // x = saturação, y = contraste, z = vinheta, w = calor
    float4 _Lift;     // sombras (rgb)

    struct v2f_img2 { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

    v2f_img2 vert_img2 (appdata_img v)
    {
        v2f_img2 o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    float3 Box4(float2 uv, float d)
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-d, -d, d, d);
        float3 s = tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb
                 + tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb;
        return s * 0.25;
    }

    float3 ACES(float3 x)
    {
        const float a = 2.51; const float b = 0.03; const float c = 2.43; const float d = 0.59; const float e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0: filtro de brilho
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img2
            #pragma fragment frag
            float4 frag (v2f_img2 i) : SV_Target
            {
                float3 c = Box4(i.uv, 1.0);
                c = min(c, 60.0);
                float br = max(c.r, max(c.g, c.b));
                float soft = clamp(br - _Params.x + _Params.y, 0, 2 * _Params.y);
                soft = soft * soft / (4 * _Params.y + 1e-4);
                float contrib = max(soft, br - _Params.x) / max(br, 1e-4);
                return float4(c * contrib, 1);
            }
            ENDCG
        }

        // 1: reduzir
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img2
            #pragma fragment frag
            float4 frag (v2f_img2 i) : SV_Target { return float4(Box4(i.uv, 1.0), 1); }
            ENDCG
        }

        // 2: ampliar e somar
        Pass
        {
            Blend One One
            CGPROGRAM
            #pragma vertex vert_img2
            #pragma fragment frag
            float4 frag (v2f_img2 i) : SV_Target { return float4(Box4(i.uv, 0.5), 1); }
            ENDCG
        }

        // 3: composição final (bloom + exposição + ACES + cor + vinheta)
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img2
            #pragma fragment frag
            float4 frag (v2f_img2 i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
                c += tex2D(_BloomTex, i.uv).rgb * _Params.z;
                c *= _Params.w;
                c = ACES(c);
                // Saturação
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(l.xxx, c, _Grade.x);
                // Contraste (em torno do cinza médio, no espaço linear)
                c = pow(max(c, 0), 1.0 / 2.2);
                c = saturate((c - 0.5) * _Grade.y + 0.5);
                c = pow(c, 2.2);
                // Tom quente / sombras levemente azuladas
                c *= float3(1.0 + _Grade.w, 1.0 + _Grade.w * 0.4, 1.0 - _Grade.w * 0.6);
                c += _Lift.rgb * (1.0 - saturate(l * 3.0));
                // Vinheta
                float2 d = i.uv - 0.5;
                float vig = 1.0 - dot(d, d) * _Grade.z;
                c *= saturate(vig);
                return float4(c, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
