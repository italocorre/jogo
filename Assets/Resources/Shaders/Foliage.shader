Shader "MarAberto/Foliage"
{
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _Wind ("Vento", Range(0,1)) = 1
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
            float3 viewDir;
        };

        fixed4 _Color;
        half _Wind;

        void vert (inout appdata_full v)
        {
            // Balanço com o vento (e com a correnteza, debaixo d'água).
            // O alfa da cor do vértice diz quanto aquele ponto balança (0 = preso no chão).
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float under = wp.y < _MA_SeaLevel ? 1.0 : 0.0;
            float t = _MA_Time * lerp(1.7, 0.9, under);
            float gust = 0.6 + 0.4 * sin(_MA_Time * 0.35 + wp.x * 0.02);
            float amp = lerp(0.12, 0.2, under) * _Wind * v.color.a * gust;
            float sway = sin(t + wp.x * 0.35 + wp.z * 0.21) + 0.45 * sin(t * 2.3 + wp.z * 0.7 + wp.x * 0.3);
            float3 offs = float3(sway, 0, sway * 0.6) * amp;
            v.vertex.xyz += mul((float3x3)unity_WorldToObject, offs);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 albedo = MA_VertexColor(IN.color.rgb) * _Color.rgb;
            o.Albedo = albedo;
            o.Alpha = 1;
            // Luz atravessando as folhas (contraluz).
            float3 sunDir = normalize(_MA_SunDir.xyz);
            float back = pow(saturate(dot(normalize(IN.viewDir), -sunDir)), 4.0);
            o.Emission = albedo * _MA_SunColor.rgb * back * 0.45;
            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
                o.Albedo = lerp(albedo, albedo * float3(0.45, 0.8, 0.9), saturate(depth / 10.0));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
