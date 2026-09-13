using System.Runtime.InteropServices;
using Vortice;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SaGlitchYmm;

internal sealed class GlitchChromaEffect(IGraphicsDevicesAndContext devices)
    : D2D1CustomShaderEffectBase(Create<GlitchChromaEffect.Impl>(devices))
{
    public float GlitchAmount { set => SetValue(0, value); }
    public float GlitchBandSize { set => SetValue(1, value); }
    public float GlitchMaxShift { set => SetValue(2, value); }
    public float GlitchSeed { set => SetValue(3, value); }
    public float ChromaticAberration { set => SetValue(4, value); }
    public float ScanlineAmount { set => SetValue(5, value); }
    public float ScanlineSpacing { set => SetValue(6, value); }
    public float VignetteAmount { set => SetValue(7, value); }
    public float Saturation { set => SetValue(8, value); }
    public float HueRotate { set => SetValue(9, value); }
    public float Brightness { set => SetValue(10, value); }

    [CustomEffect(1)]
    private sealed class Impl : D2D1CustomShaderEffectImplBase<Impl>
    {
        private Constants _constants = new() { GlitchBandSize = 12f, Saturation = 1f, Brightness = 1f, ScanlineSpacing = 3f };

        [CustomEffectProperty(PropertyType.Float, 0)] public float GlitchAmount { get => _constants.GlitchAmount; set { _constants.GlitchAmount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 1)] public float GlitchBandSize { get => _constants.GlitchBandSize; set { _constants.GlitchBandSize = Math.Clamp(value, 1f, 128f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 2)] public float GlitchMaxShift { get => _constants.GlitchMaxShift; set { _constants.GlitchMaxShift = Math.Clamp(value, 0f, 200f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 3)] public float GlitchSeed { get => _constants.GlitchSeed; set { _constants.GlitchSeed = value; UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 4)] public float ChromaticAberration { get => _constants.ChromaticAberration; set { _constants.ChromaticAberration = Math.Clamp(value, 0f, 80f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 5)] public float ScanlineAmount { get => _constants.ScanlineAmount; set { _constants.ScanlineAmount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 6)] public float ScanlineSpacing { get => _constants.ScanlineSpacing; set { _constants.ScanlineSpacing = Math.Clamp(value, 1f, 20f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 7)] public float VignetteAmount { get => _constants.VignetteAmount; set { _constants.VignetteAmount = Math.Clamp(value, 0f, 1f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 8)] public float Saturation { get => _constants.Saturation; set { _constants.Saturation = Math.Clamp(value, 0f, 3f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 9)] public float HueRotate { get => _constants.HueRotate; set { _constants.HueRotate = Math.Clamp(value, -180f, 180f); UpdateConstants(); } }
        [CustomEffectProperty(PropertyType.Float, 10)] public float Brightness { get => _constants.Brightness; set { _constants.Brightness = Math.Clamp(value, 0f, 2f); UpdateConstants(); } }

        public Impl() : base(ShaderResourceLoader.Get("GlitchChroma")) { }
        protected override void UpdateConstants() => drawInformation?.SetPixelShaderConstantBuffer(_constants);

        public override void MapInputRectsToOutputRect(RawRect[] inputRects, RawRect[] inputOpaqueSubRects, out RawRect outputRect, out RawRect outputOpaqueSubRect)
        {
            outputRect = inputRects[0];
            outputOpaqueSubRect = default;
            _constants.Left = outputRect.Left; _constants.Top = outputRect.Top;
            _constants.Right = outputRect.Right; _constants.Bottom = outputRect.Bottom;
            UpdateConstants();
        }

        public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        {
            // Bands only move horizontally, so the halo only needs to grow
            // left/right by the worst case shift (max shift * amount) plus
            // the chromatic-aberration offset; rows never move vertically.
            int halo = (int)MathF.Ceiling(_constants.GlitchMaxShift * _constants.GlitchAmount + _constants.ChromaticAberration);
            inputRects[0] = new(Safe((long)outputRect.Left - halo), outputRect.Top, Safe((long)outputRect.Right + halo), outputRect.Bottom);
        }

        private static int Safe(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);

        [StructLayout(LayoutKind.Sequential)]
        private struct Constants
        {
            public float GlitchAmount, GlitchBandSize, GlitchMaxShift, GlitchSeed;
            public float ChromaticAberration, ScanlineAmount, ScanlineSpacing, VignetteAmount;
            public float Saturation, HueRotate, Brightness, Padding0;
            public float Left, Top, Right, Bottom;
        }
    }
}
