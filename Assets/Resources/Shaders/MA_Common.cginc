#ifndef MA_COMMON_INCLUDED
#define MA_COMMON_INCLUDED

// Valores globais enviados pelo C# (WorldEnvironment / Ocean)
float4 _MA_SkyTop;
float4 _MA_SkyHorizon;
float4 _MA_SunDir;
float4 _MA_SunColor;
float _MA_Day;
float _MA_Time;
float _MA_SeaLevel;
float _MA_Underwater;
float4 _MA_Wave0, _MA_Wave1, _MA_Wave2, _MA_Wave3;

// Cores de vértice são escritas em sRGB; converte para o espaço linear.
float3 MA_VertexColor(float3 c)
{
#if defined(UNITY_COLORSPACE_GAMMA)
    return c;
#else
    return c * (c * (c * 0.305306011 + 0.682171111) + 0.012522878);
#endif
}

float MA_Hash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float MA_ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = MA_Hash(i);
    float b = MA_Hash(i + float2(1, 0));
    float c = MA_Hash(i + float2(0, 1));
    float d = MA_Hash(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float MA_Fbm(float2 p)
{
    float s = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        s += MA_ValueNoise(p) * a;
        p = p * 2.03 + float2(17.1, 9.2);
        a *= 0.5;
    }
    return s;
}

// Cor do céu numa direção (usada no céu e no reflexo da água).
float3 MA_SkyColor(float3 dir)
{
    float y = dir.y;
    float t = saturate(pow(saturate(y), 0.45));
    float3 col = lerp(_MA_SkyHorizon.rgb, _MA_SkyTop.rgb, t);
    float3 sunDir = normalize(_MA_SunDir.xyz);
    float sd = saturate(dot(dir, sunDir));
    col += _MA_SunColor.rgb * pow(sd, 8.0) * 0.35;
    // Abaixo do horizonte: tom de mar profundo.
    float3 below = lerp(_MA_SkyHorizon.rgb, float3(0.05, 0.22, 0.32) * (0.25 + 0.75 * _MA_Day), saturate(-y * 4.0));
    return y < 0 ? below : col;
}

// Padrão de cáusticas (luz dançando no fundo do mar).
float MA_Caustics(float2 p, float t)
{
    float2 q = p * 0.35;
    float c = 0.0;
    float2 o1 = float2(t * 0.11, t * 0.07);
    float2 o2 = float2(-t * 0.09, t * 0.12);
    float n1 = MA_ValueNoise(q + o1) + MA_ValueNoise(q * 2.1 - o1);
    float n2 = MA_ValueNoise(q * 1.3 + o2) + MA_ValueNoise(q * 2.7 + o2 * 1.3);
    c = 1.0 - abs(n1 - n2);
    return pow(saturate(c), 8.0);
}

// Onda de Gerstner (mesma fórmula do Ocean.cs)
float3 MA_Gerstner(float4 w, float3 p, inout float3 tangent, inout float3 binormal, float fade)
{
    float2 d = normalize(w.xy);
    float steep = w.z * fade;
    float k = 6.2831853 / w.w;
    float c = sqrt(9.8 / k);
    float f = k * (dot(d, p.xz) - c * _MA_Time);
    float a = steep / k;
    float sf = sin(f);
    float cf = cos(f);
    tangent += float3(-d.x * d.x * steep * sf, d.x * steep * cf, -d.x * d.y * steep * sf);
    binormal += float3(-d.x * d.y * steep * sf, d.y * steep * cf, -d.y * d.y * steep * sf);
    return float3(d.x * a * cf, a * sf, d.y * a * cf);
}

#endif
