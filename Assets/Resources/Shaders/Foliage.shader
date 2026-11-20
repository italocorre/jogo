Shader "MarAberto/Foliage"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull Off

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert fullforwardshadows addshadow
        #pragma target 3.0
        #include "MA_Common.cginc"

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
            float facing : VFACE;
        };

        fixed4 _Color;

        void vert (inout appdata_full v)
        {
            // Balanço leve com o vento (e com a correnteza, debaixo d'água).
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float under = wp.y < _MA_SeaLevel ? 1.0 : 0.0;
            float t = _MA_Time * lerp(1.6, 0.9, under);
            float amp = lerp(0.05, 0.12, under);
            float sway = sin(t + wp.x * 0.35 + wp.z * 0.21) + 0.5 * sin(t * 2.3 + wp.z * 0.7);
            float3 offs = float3(sway, 0, sway * 0.6) * amp;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offs);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 albedo = IN.color.rgb * _Color.rgb;
            // Lado de trás um pouco mais escuro.
            albedo *= IN.facing > 0 ? 1.0 : 0.8;
            o.Albedo = albedo;
            o.Alpha = 1;
            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
                o.Albedo = lerp(albedo, albedo * float3(0.55, 0.85, 0.95), saturate(depth / 12.0));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
