Shader "MarAberto/Ghost"
{
    // Prévia da construção: translúcida, verde (pode) ou vermelha (não pode).
    Properties { _Color ("Cor", Color) = (0.4, 1, 0.5, 0.35) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color;
            struct v2f { float4 pos : SV_POSITION; float3 n : TEXCOORD0; float3 v : TEXCOORD1; };
            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.n = UnityObjectToWorldNormal(v.normal);
                o.v = normalize(WorldSpaceViewDir(v.vertex));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float rim = 1.0 - saturate(abs(dot(normalize(i.n), i.v)));
                float pulse = 0.85 + 0.15 * sin(_Time.y * 5.0);
                return fixed4(_Color.rgb * (0.8 + rim * 0.8), _Color.a * pulse + rim * 0.35);
            }
            ENDCG
        }
    }
}
