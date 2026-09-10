using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace SaGlitchYmm;

internal sealed class SaGlitchProcessor : IVideoEffectProcessor
{
    private readonly SaGlitchEffect _item;
    private readonly GlitchChromaEffect? _effect;
    private ID2D1Image? _input;

    public SaGlitchProcessor(IGraphicsDevicesAndContext devices, SaGlitchEffect item)
    {
        _item = item;
        var effect = new GlitchChromaEffect(devices);
        if (!effect.IsEnabled)
        {
            effect.Dispose();
            return;
        }
        _effect = effect;
    }

    public ID2D1Image Output => _effect?.Output ?? _input ?? throw new InvalidOperationException("入力が未設定です。");

    public void SetInput(ID2D1Image? input)
    {
        _input = input;
        _effect?.SetInput(0, input, true);
    }

    public void ClearInput()
    {
        _input = null;
        _effect?.SetInput(0, null, true);
    }

    public DrawDescription Update(EffectDescription effectDescription)
    {
        if (_effect is null) return effectDescription.DrawDescription;
        var frame = effectDescription.ItemPosition.Frame;
        var length = effectDescription.ItemDuration.Frame;
        var fps = effectDescription.FPS;

        _effect.GlitchAmount = (float)(_item.GlitchAmount.GetValue(frame, length, fps) / 100.0);
        _effect.GlitchBandSize = (float)Math.Round(_item.GlitchBandSize.GetValue(frame, length, fps));
        _effect.GlitchMaxShift = (float)Math.Round(_item.GlitchMaxShift.GetValue(frame, length, fps));
        float seed = (float)Math.Round(_item.GlitchSeed.GetValue(frame, length, fps));
        float animationSpeed = (float)_item.AnimationSpeed.GetValue(frame, length, fps);
        _effect.GlitchSeed = GlitchMath.EffectiveSeed(seed, animationSpeed, frame);
        _effect.ChromaticAberration = (float)_item.ChromaticAberration.GetValue(frame, length, fps);
        _effect.ScanlineAmount = (float)(_item.ScanlineAmount.GetValue(frame, length, fps) / 100.0);
        _effect.ScanlineSpacing = (float)_item.ScanlineSpacing.GetValue(frame, length, fps);
        _effect.VignetteAmount = (float)(_item.VignetteAmount.GetValue(frame, length, fps) / 100.0);
        _effect.Saturation = (float)(_item.Saturation.GetValue(frame, length, fps) / 100.0);
        _effect.HueRotate = (float)_item.HueRotate.GetValue(frame, length, fps);
        _effect.Brightness = (float)(_item.Brightness.GetValue(frame, length, fps) / 100.0);

        return effectDescription.DrawDescription;
    }

    public void Dispose()
    {
        ClearInput();
        _effect?.Dispose();
    }
}
