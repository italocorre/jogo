Shader "MarAberto/Outline"
{
    // Contorno suave em volta do item que você está mirando (casca invertida e um pouco maior).
    Properties { _Color ("Cor", Color) = (1, 0.9, 0.6, 0.45) _Width ("Espessura", Float) = 0.012 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Front
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            fixed4 _Color; float _Width;
            struct v2f { float4 pos : SV_POSITION; };
            v2f vert (appdata_base v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);
                wp += wn * _Width;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float pulse = 0.8 + 0.2 * sin(_Time.y * 4.0);
                return fixed4(_Color.rgb, _Color.a * pulse);
            }
            ENDCG
        }
    }
}
