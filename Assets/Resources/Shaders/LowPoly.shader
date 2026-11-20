Shader "MarAberto/LowPoly"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _Glossiness ("Brilho", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "MA_Common.cginc"

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
        };

        fixed4 _Color;
        half _Glossiness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = IN.color.rgb * _Color.rgb;
            o.Albedo = albedo;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;

            // Debaixo d'água: cáusticas no que está virado para cima.
            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
            {
                float up = saturate(IN.worldNormal.y);
                float fade = saturate(depth / 1.5) * saturate(1.0 - depth / 40.0);
                float c = MA_Caustics(IN.worldPos.xz, _MA_Time);
                o.Emission = albedo * _MA_SunColor.rgb * c * up * fade * 0.9;
                // Água absorve o vermelho: o fundo fica azulado.
                o.Albedo = lerp(albedo, albedo * float3(0.55, 0.85, 0.95), saturate(depth / 12.0));
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
