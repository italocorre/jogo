Shader "MarAberto/Rain"
{
    // Cortina de chuva em volta da câmera: riscos finos caindo, mais fortes com _MA_Rain.
    Properties { _Scale ("Escala", Float) = 1 }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "MA_Common.cginc"
            float _Scale;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wp : TEXCOORD1; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord.xy;
                o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                if (_MA_Underwater > 0.5 || _MA_Rain < 0.01) discard;
                float2 uv = i.uv * float2(90.0 * _Scale, 5.0);
                float col = floor(uv.x);
                float h = MA_Hash(float2(col, _Scale * 17.0));
                float speed = 2.2 + h * 1.2;
                float y = frac(uv.y + _MA_Time * speed + h * 13.0);
                float streak = smoothstep(0.0, 0.05, y) * smoothstep(0.22, 0.05, y);
                float thin = 1.0 - abs(frac(uv.x) - 0.5) * 2.0;
                thin = smoothstep(0.75, 1.0, thin);
                float present = step(1.0 - _MA_Rain * 0.85, h);
                float a = streak * thin * present * 0.35 * saturate(_MA_Rain * 1.5);
                float3 c = lerp(float3(0.65, 0.7, 0.75), float3(1, 1, 1), _MA_Flash) * lerp(0.25, 1.0, _MA_Day);
                return fixed4(c, a);
            }
            ENDCG
        }
    }
}
