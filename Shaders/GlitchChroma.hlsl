#define D2D_REQUIRES_SCENE_POSITION
#define D2D_ENTRY main
#include <d2d1effecthelpers.hlsli>

float glitchAmount;
float glitchBandSize;
float glitchMaxShift;
float glitchSeed;
float chromaticAberration;
float scanlineAmount;
float scanlineSpacing;
float vignetteAmount;
float saturation;
float hueRotate;
float brightness;
float padding0;
float4 inputBounds;

float hash11(float x) { return frac(sin(x) * 43758.5453123); }

float3 rgb2hsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 hsv2rgb(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

float3 unpremultiply(float4 c) { return c.a > 0 ? c.rgb / c.a : 0; }

// Each horizontal band either sits still or jumps sideways by a pseudo-random
// amount; both the activation chance and the direction come from a hash of
// the band index and the seed, so the same seed always reproduces the same
// glitch pattern (see GlitchMath.Hash11 / prototype/glitch_reference.py).
float2 BandOffset(float y)
{
    float band = floor(y / max(1.0, glitchBandSize));
    float active = hash11(band * 0.6180339887 + glitchSeed * 13.37) < (0.15 + 0.55 * glitchAmount) ? 1.0 : 0.0;
    float dir = hash11(band * 1.3702 + glitchSeed * 4.7 + 91.0) * 2.0 - 1.0;
    return float2(active * dir * glitchMaxShift * glitchAmount, 0.0);
}

// Explicit LOD is valid inside the dynamic passthrough/aberration branches.
// Implicit-derivative sampling can trigger FXC's main_Impl uninitialized warning.
float4 SampleAt(float2 pos)
{
    float4 uv = D2DGetInputCoordinate(0);
    return InputTexture0.SampleLevel(InputSampler0, uv.xy + uv.zw * (pos - D2DGetScenePosition().xy), 0);
}

// Clamp to the true image bounds instead of letting the border go transparent,
// so large shifts/aberration stretch the edge pixels instead of showing black.
float4 SampleClamped(float2 pos)
{
    float2 clamped = clamp(pos, inputBounds.xy + 0.5, inputBounds.zw - 0.5);
    return SampleAt(clamped);
}

D2D_PS_ENTRY(main)
{
    float2 p = D2DGetScenePosition().xy;
    // Materialize the source sample before any dynamic branch. FXC's
    // D2D helper can otherwise report its generated input coordinate as
    // potentially uninitialized on the passthrough return path.
    float4 passthrough = SampleAt(p);
    bool flat = glitchAmount <= 0 && chromaticAberration <= 0 && scanlineAmount <= 0
        && vignetteAmount <= 0 && saturation == 1 && hueRotate == 0 && brightness == 1;
    if (flat) return passthrough;

    float2 shift = BandOffset(p.y);
    float2 basePos = p + shift;
    float4 centerSample = SampleClamped(basePos);
    float alpha = centerSample.a;

    float3 rgb = unpremultiply(centerSample);
    if (chromaticAberration > 0)
    {
        float r = unpremultiply(SampleClamped(basePos + float2(chromaticAberration, 0))).r;
        float g = unpremultiply(centerSample).g;
        float b = unpremultiply(SampleClamped(basePos - float2(chromaticAberration, 0))).b;
        rgb = float3(r, g, b);
    }
    else
    {
        rgb = unpremultiply(centerSample);
    }

    if (saturation != 1 || hueRotate != 0)
    {
        float3 hsv = rgb2hsv(saturate(rgb));
        hsv.y = saturate(hsv.y * saturation);
        hsv.x = frac(hsv.x + hueRotate / 360.0);
        rgb = hsv2rgb(hsv);
    }
    rgb *= brightness;

    if (scanlineAmount > 0)
    {
        float wave = 0.5 + 0.5 * cos(p.y * (6.2831853 / max(1.0, scanlineSpacing)));
        rgb *= lerp(1.0, wave, scanlineAmount);
    }
    if (vignetteAmount > 0)
    {
        float2 center = (inputBounds.xy + inputBounds.zw) * 0.5;
        float2 extent = max((inputBounds.zw - inputBounds.xy) * 0.5, 1.0);
        float dist = length((p - center) / extent);
        float falloff = saturate(1.0 - dist * dist);
        rgb *= lerp(1.0, falloff, vignetteAmount);
    }
    rgb = saturate(rgb);
    return float4(rgb * alpha, alpha);
}

