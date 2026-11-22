Shader "MarAberto/PBR"
{
    // Modelos realistas: cor + relevo (normal map) + recorte (folhas) + vento opcional.
    Properties
    {
        _Color ("Cor", Color) = (1,1,1,1)
        _MainTex ("Textura", 2D) = "white" {}
        _BumpMap ("Relevo", 2D) = "bump" {}
        _BumpScale ("Força do relevo", Float) = 1
        _AlphaTex ("Recorte", 2D) = "white" {}
        _UseAlpha ("Usa recorte", Float) = 0
        _Cutoff ("Corte", Range(0,1)) = 0.45
        _Glossiness ("Brilho", Range(0,1)) = 0.2
        _Metallic ("Metal", Range(0,1)) = 0
        _UseVertexColor ("Usa cor do vértice", Float) = 0
        _Wind ("Vento", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Lados", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 300
        Cull [_Cull]

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow alphatest:_Cutoff vertex:vert
        #pragma target 3.0
        #include "MA_Common.cginc"

        sampler2D _MainTex, _BumpMap, _AlphaTex;
        fixed4 _Color;
        half _Glossiness, _Metallic, _UseAlpha, _BumpScale, _Wind, _UseVertexColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
            float4 color : COLOR;
            float facing : VFACE;
            INTERNAL_DATA
        };

        void vert (inout appdata_full v)
        {
            if (_Wind > 0.001)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _MA_Time * 1.6;
                float gust = 0.6 + 0.4 * sin(_MA_Time * 0.35 + wp.x * 0.02);
                // Quanto mais alto/longe do tronco, mais balança (usa o V da textura e a altura).
                float w = saturate(v.color.a) * _Wind * gust;
                float sway = sin(t + wp.x * 0.35 + wp.z * 0.21) + 0.45 * sin(t * 2.3 + wp.z * 0.7 + wp.x * 0.3);
                float3 offs = float3(sway, sin(t * 1.7 + wp.x) * 0.3, sway * 0.6) * 0.12 * w;
                v.vertex.xyz += mul((float3x3)unity_WorldToObject, offs);
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            if (_UseVertexColor > 0.5) c.rgb *= MA_VertexColor(IN.color.rgb);
            o.Alpha = _UseAlpha > 0.5 ? tex2D(_AlphaTex, IN.uv_MainTex).r : 1.0;
            float3 n = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            n.z *= IN.facing > 0 ? 1.0 : -1.0; // folhas: lado de trás com normal invertida
            o.Normal = n;
            float3 albedo = c.rgb;

            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
            {
                float3 wn = WorldNormalVector(IN, o.Normal);
                float up = saturate(wn.y);
                float fade = saturate(depth / 1.2) * saturate(1.0 - depth / 40.0);
                float caus = MA_Caustics(IN.worldPos.xz, _MA_Time);
                float3 att = exp(-depth * float3(0.11, 0.045, 0.035));
                o.Emission = albedo * att * _MA_SunColor.rgb * caus * up * fade * 0.55;
                albedo = albedo * (att * 0.85 + float3(0.02, 0.07, 0.09));
            }
            o.Albedo = albedo;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
