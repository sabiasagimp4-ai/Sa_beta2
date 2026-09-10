using SaGlitchYmm;

// EffectiveSeed: animation speed 0 or frame 0 must reproduce the seed exactly,
// so a still frame (or a fully static glitch) is deterministic.
if (GlitchMath.EffectiveSeed(42f, 0f, 500) != 42f) throw new Exception("AnimationSpeed=0 must not change the seed");
if (GlitchMath.EffectiveSeed(42f, 10f, 0) != 42f) throw new Exception("Frame 0 must not change the seed");
if (GlitchMath.EffectiveSeed(0f, 100f, 10) != 50f) throw new Exception("EffectiveSeed formula changed unexpectedly");

// Hash11 must stay inside the [0,1) band-activation range for every input the
// shader actually evaluates (band index combined with seed and a fixed offset).
var rng = new Random(7);
for (int i = 0; i < 100000; i++)
{
    float x = (float)(rng.NextDouble() * 20000 - 10000);
    float h = GlitchMath.Hash11(x);
    if (h < 0f || h >= 1f) throw new Exception($"Hash11({x}) out of range: {h}");
}

Console.WriteLine("PASS: GlitchMath.EffectiveSeed and Hash11");
