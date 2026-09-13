namespace SaGlitchYmm;

// Pure math shared with the HLSL shader's band hash and seed animation, kept
// dependency-free so it can be checked without a GPU (see tests/Program.cs
// and prototype/glitch_reference.py, which mirrors the same formulas).
internal static class GlitchMath
{
    public static float EffectiveSeed(float seed, float animationSpeed, int frame) => seed + animationSpeed * frame * 0.05f;

    public static float Hash11(float x) => Frac(MathF.Sin(x) * 43758.5453123f);

    public static float Frac(float x) => x - MathF.Floor(x);
}
