#if OPENGL
    #define SV_POSITION POSITION
    #define VS_SHADERMODEL vs_3_0
    #define PS_SHADERMODEL ps_3_0
#else
    #define VS_SHADERMODEL vs_4_0_level_9_1
    #define PS_SHADERMODEL ps_4_0_level_9_1
#endif

float4x4 WorldViewProjection;
float2 Center;
float3 HighlightColor;
float Time;
float Interval;
float PhaseOffset;
float ShimmerStrength = 0.65f;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 WorldPos : TEXCOORD0;
};

VertexShaderOutput MainVS(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.WorldPos = input.Position.xy;
    output.Position = mul(float4(input.Position.xyz, 1.0), WorldViewProjection);
    output.Color = input.Color;
    return output;
}

float SmoothBand(float x, float start, float peak, float end)
{
    float rise = smoothstep(start, peak, x);
    float fall = 1.0 - smoothstep(peak, end, x);
    return saturate(rise * fall);
}

float4 MainPS(VertexShaderOutput input) : COLOR0
{
    float4 col = input.Color;

    float angle = atan2(input.WorldPos.y - Center.y, input.WorldPos.x - Center.x);
    float normalized = angle / 6.2831853 + 0.5;
    float wave = frac(normalized - ((Time - PhaseOffset) / Interval));

    float shine = SmoothBand(wave, 0.0, 0.05, 0.14);
    col.rgb += HighlightColor * shine * ShimmerStrength;
    col.a = saturate(col.a + shine * 0.22);

    return col;
}

technique BrickShimmerTechnique
{
    pass Pass0
    {
        VertexShader = compile VS_SHADERMODEL MainVS();
        PixelShader = compile PS_SHADERMODEL MainPS();
    }
}
