Shader "MarAberto/Flame"
{
    // Chama da tocha: brilha sozinha (sem luz), soma a cor e tremula.
    Properties { _Color ("Cor", Color) = (1, 0.55, 0.15, 1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            float _MA_Time;
            struct v2f { float4 pos : SV_POSITION; float4 col : COLOR; float h : TEXCOORD0; };
            v2f vert (appdata_full v)
            {
                v2f o;
                float h = saturate(v.color.a);
                float3 p = v.vertex.xyz;
                float t = _Time.y * 9.0;
                p.x += sin(t + p.y * 30.0) * 0.012 * h;
                p.z += cos(t * 1.3 + p.y * 25.0) * 0.012 * h;
                p.y *= 1.0 + sin(t * 0.7) * 0.08 * h;
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.col = v.color;
                o.h = h;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 c = lerp(float3(1.0, 0.85, 0.45), float3(1.0, 0.35, 0.05), i.h) * 2.2;
                return fixed4(c * i.col.rgb, (1.0 - i.h * 0.6) * _Color.a);
            }
            ENDCG
        }
    }
}
