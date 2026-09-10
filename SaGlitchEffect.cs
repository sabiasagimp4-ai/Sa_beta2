using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace SaGlitchYmm;

[VideoEffect("Sa_Glitch", ["フィルタ"], ["グリッチ", "色収差", "レトロ"], IsAviUtlSupported = false)]
public sealed class SaGlitchEffect : VideoEffectBase
{
    public override string Label => "Sa_Glitch";

    [Display(Name = "グリッチ強度", Description = "帯状のブロックがランダムに横へずれる強さ。0で無効", Order = 0)]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation GlitchAmount { get; } = new(25, 0, 100);

    [Display(Name = "グリッチ帯サイズ", Description = "横にずれる帯1本の高さ", Order = 1)]
    [AnimationSlider("F0", "px", 1, 128)]
    public Animation GlitchBandSize { get; } = new(12, 1, 128);

    [Display(Name = "グリッチ最大変位", Description = "帯が横へずれる最大量。グリッチ強度と掛け合わせて使う", Order = 2)]
    [AnimationSlider("F0", "px", 0, 200)]
    public Animation GlitchMaxShift { get; } = new(60, 0, 200);

    [Display(Name = "シード", Description = "グリッチの乱数の種。値を変えるとパターンが変わる", Order = 3)]
    [AnimationSlider("F0", "", 0, 9999)]
    public Animation GlitchSeed { get; } = new(0, 0, 9999);

    [Display(Name = "アニメーション速度", Description = "0ならフレームごとに同じパターン。大きいほど毎フレームでパターンが変わる", Order = 4)]
    [AnimationSlider("F1", "", 0, 100)]
    public Animation AnimationSpeed { get; } = new(0, 0, 100);

    [Display(Name = "色収差", Description = "赤チャンネルと青チャンネルを左右逆にずらす量", Order = 5)]
    [AnimationSlider("F1", "px", 0, 80)]
    public Animation ChromaticAberration { get; } = new(6, 0, 80);

    [Display(Name = "スキャンライン強度", Description = "走査線のような横縞の濃さ", Order = 6)]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation ScanlineAmount { get; } = new(15, 0, 100);

    [Display(Name = "スキャンライン間隔", Description = "走査線の間隔", Order = 7)]
    [AnimationSlider("F1", "px", 1, 20)]
    public Animation ScanlineSpacing { get; } = new(3, 1, 20);

    [Display(Name = "ビネット強度", Description = "画面端を暗くする強さ", Order = 8)]
    [AnimationSlider("F1", "%", 0, 100)]
    public Animation VignetteAmount { get; } = new(20, 0, 100);

    [Display(Name = "彩度", Description = "100%で変化なし。0%で無彩色", Order = 9)]
    [AnimationSlider("F1", "%", 0, 300)]
    public Animation Saturation { get; } = new(100, 0, 300);

    [Display(Name = "色相", Description = "色相を回転する角度", Order = 10)]
    [AnimationSlider("F1", "°", -180, 180)]
    public Animation HueRotate { get; } = new(0, -180, 180);

    [Display(Name = "明るさ", Description = "100%で変化なし", Order = 11)]
    [AnimationSlider("F1", "%", 0, 200)]
    public Animation Brightness { get; } = new(100, 0, 200);

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription) => [];

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices) => new SaGlitchProcessor(devices, this);

    protected override IEnumerable<IAnimatable> GetAnimatables() =>
        [GlitchAmount, GlitchBandSize, GlitchMaxShift, GlitchSeed, AnimationSpeed, ChromaticAberration,
         ScanlineAmount, ScanlineSpacing, VignetteAmount, Saturation, HueRotate, Brightness];
}
