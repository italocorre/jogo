Shader "MarAberto/LowPoly"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _Glossiness ("Brilho", Range(0,1)) = 0.08
        _Detail ("Detalhe do terreno", Range(0,1)) = 0
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
        half _Detail;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 albedo = MA_VertexColor(IN.color.rgb) * _Color.rgb;

            if (_Detail > 0.0)
            {
                // Variação suave de cor para o terreno não parecer liso.
                float2 p = IN.worldPos.xz;
                float n1 = MA_ValueNoise(p * 0.08);
                float n2 = MA_ValueNoise(p * 0.6);
                float n3 = MA_ValueNoise(p * 3.1);
                float v = n1 * 0.5 + n2 * 0.35 + n3 * 0.15;
                albedo *= lerp(1.0, 0.82 + v * 0.36, _Detail);
                // Areia molhada perto da água.
                float wet = saturate(1.0 - abs(IN.worldPos.y - 0.25) / 0.6);
                albedo *= lerp(1.0, 0.78, wet * _Detail);
            }

            o.Albedo = albedo;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;

            // Debaixo d'água: cáusticas no que está virado para cima.
            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
            {
                float up = saturate(IN.worldNormal.y);
                float fade = saturate(depth / 1.2) * saturate(1.0 - depth / 40.0);
                float c = MA_Caustics(IN.worldPos.xz, _MA_Time);
                // A luz enfraquece com a profundidade (vermelho primeiro).
                float3 att = exp(-depth * float3(0.11, 0.045, 0.035));
                o.Albedo = albedo * (att * 0.85 + float3(0.02, 0.07, 0.09));
                o.Emission = albedo * att * _MA_SunColor.rgb * c * up * fade * 0.55;
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
