Shader "MarAberto/Terrain"
{
    // Terreno realista: mistura 4 texturas pela cor do vértice (R areia, G grama, B terra, A rocha).
    // Areia/grama/terra projetadas de cima; rocha projetada pelos 3 eixos (encostas sem esticar).
    Properties
    {
        _Sand ("Areia", 2D) = "white" {}
        _SandN ("Areia relevo", 2D) = "bump" {}
        _Grass ("Grama", 2D) = "white" {}
        _GrassN ("Grama relevo", 2D) = "bump" {}
        _Ground ("Terra", 2D) = "white" {}
        _GroundN ("Terra relevo", 2D) = "bump" {}
        _Rock ("Rocha", 2D) = "white" {}
        _RockN ("Rocha relevo", 2D) = "bump" {}
        _Scale ("Escala (repetições por metro)", Float) = 0.18
        _RockScale ("Escala da rocha", Float) = 0.12
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.5
        #include "MA_Common.cginc"

        sampler2D _Sand, _SandN, _Grass, _GrassN, _Ground, _GroundN, _Rock, _RockN;
        float _Scale, _RockScale;

        struct Input
        {
            float4 color : COLOR;
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        // Amostra em duas escalas para esconder a repetição.
        float3 Tile(sampler2D t, float2 uv)
        {
            float3 a = tex2D(t, uv).rgb;
            float3 b = tex2D(t, uv * 0.27 + 0.31).rgb;
            float m = MA_ValueNoise(uv * 0.35);
            return lerp(a, b, smoothstep(0.35, 0.65, m) * 0.6);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 wn = WorldNormalVector(IN, float3(0, 0, 1));
            float4 w = max(IN.color, 0.0001);
            w /= (w.r + w.g + w.b + w.a);
            float2 top = IN.worldPos.xz * _Scale;

            const float3 LUM = float3(0.3, 0.59, 0.11);
            // Variação de cor em manchas grandes (o chão não fica de uma cor só).
            float patch = MA_ValueNoise(IN.worldPos.xz * 0.012);
            float patch2 = MA_ValueNoise(IN.worldPos.xz * 0.045 + 7.0);

            float3 sand = Tile(_Sand, top);
            sand = lerp(sand, dot(sand, LUM).xxx * float3(1.02, 0.98, 0.9), 0.35) * 1.12; // areia clara, menos amarela

            // Grama: verde vivo, com manchas de verde-escuro e verde-claro.
            float3 g = Tile(_Grass, top * 1.3);
            float gl = dot(g, LUM);
            float3 greenA = float3(0.30, 0.52, 0.16), greenB = float3(0.20, 0.40, 0.14), greenC = float3(0.46, 0.60, 0.22);
            float3 tint = lerp(lerp(greenB, greenA, patch), greenC, smoothstep(0.55, 0.8, patch2) * 0.6);
            float3 grass = lerp(g * float3(0.7, 1.0, 0.5), gl * tint * 2.6, 0.7);

            float3 ground = Tile(_Ground, top);
            ground = lerp(ground, dot(ground, LUM).xxx * float3(1.0, 0.9, 0.78), 0.3);

            // Rocha triplanar
            float3 bw = pow(abs(wn), 4.0); bw /= (bw.x + bw.y + bw.z);
            float3 p = IN.worldPos * _RockScale;
            float3 rock = tex2D(_Rock, p.zy).rgb * bw.x + tex2D(_Rock, p.xz).rgb * bw.y + tex2D(_Rock, p.xy).rgb * bw.z;
            rock = lerp(rock, dot(rock, LUM).xxx * float3(0.92, 0.94, 0.98), 0.7); // rocha cinza (não laranja)
            rock *= lerp(0.8, 1.1, patch2);

            float3 albedo = sand * w.r + grass * w.g + ground * w.b + rock * w.a;

            // Relevo: mistura dos normais projetados de cima (rocha usa o de cima também: barato e suficiente).
            float3 nS = UnpackNormal(tex2D(_SandN, top));
            float3 nG = UnpackNormal(tex2D(_GrassN, top * 1.3));
            float3 nD = UnpackNormal(tex2D(_GroundN, top));
            float3 nR = UnpackNormal(tex2D(_RockN, p.xz));
            o.Normal = normalize(nS * w.r + nG * w.g + nD * w.b + nR * w.a + float3(0, 0, 0.5));

            // Areia molhada perto da água
            float wet = saturate(1.0 - abs(IN.worldPos.y - 0.2) / 0.7);
            albedo *= lerp(1.0, 0.68, wet * w.r);
            float smooth = lerp(0.05, 0.35, wet * w.r);

            float depth = _MA_SeaLevel - IN.worldPos.y;
            if (depth > 0.0)
            {
                float up = saturate(wn.y);
                float fade = saturate(depth / 1.2) * saturate(1.0 - depth / 40.0);
                float caus = MA_Caustics(IN.worldPos.xz, _MA_Time);
                float3 att = exp(-depth * float3(0.11, 0.045, 0.035));
                o.Emission = albedo * att * _MA_SunColor.rgb * caus * up * fade * 0.6;
                albedo = albedo * (att * 0.85 + float3(0.02, 0.07, 0.09));
            }
            o.Albedo = albedo;
            o.Smoothness = smooth;
            o.Metallic = 0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
