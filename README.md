# Sa_Glitch for YMM4 — 0.1.0

グリッチ(横帯のブロックずれ)・色収差・スキャンライン・ビネット・彩度/色相/明るさ調整を1つにまとめた、YukkuriMovieMaker4用の画像処理エフェクトです。線検出や意味推定は一切行わない「見た目重視」の単一パスDirect2Dカスタムシェーダーです。

## 1. アルゴリズム

1. 画面を`グリッチ帯サイズ`px単位の横帯に分割する。各帯は、帯番号と`シード`から決まる疑似乱数(`hash11(x) = frac(sin(x) * 43758.5453123)`)によって「動くかどうか」と「どちら向きにずれるか」が決まる(帯が変わるたびにパターンが変わり、シードが同じなら再生のたびに同じ結果になる)。
2. 動く帯は`グリッチ最大変位 × グリッチ強度`pxまで横にずれた位置から色をサンプリングする。
3. `色収差`が0より大きい場合、赤チャンネルは+方向、青チャンネルは-方向にさらにずらしてサンプリングし、縁が色づいたように見せる。
4. サンプリングした色をHSVに変換し、`彩度`と`色相`を適用してからRGBへ戻す。
5. `明るさ`を掛け、`スキャンライン強度`に応じて縦方向のcos波で明滅させ、`ビネット強度`に応じて画面中心からの距離で暗くする。
6. すべてのパラメーターが無変化値(強度0・彩度100%・色相0・明るさ100%など)のときは元画素をそのまま返す(高速パス)。

画像外の参照は透明にせず、`inputBounds`で元画像の範囲にクランプしてサンプリングする(大きくずらしても縁が黒くならず、端の画素が伸びる)。

## 2. UI

| 順序 | 名前 | 範囲 | 初期値 |
|---:|---|---|---|
| 0 | グリッチ強度 | 0–100% | 25 |
| 1 | グリッチ帯サイズ | 1–128 px | 12 |
| 2 | グリッチ最大変位 | 0–200 px | 60 |
| 3 | シード | 0–9999 | 0 |
| 4 | アニメーション速度 | 0–100 | 0 |
| 5 | 色収差 | 0–80 px | 6 |
| 6 | スキャンライン強度 | 0–100% | 15 |
| 7 | スキャンライン間隔 | 1–20 px | 3 |
| 8 | ビネット強度 | 0–100% | 20 |
| 9 | 彩度 | 0–300% | 100 |
| 10 | 色相 | -180–180° | 0 |
| 11 | 明るさ | 0–200% | 100 |

`アニメーション速度`はシェーダー定数ではなく、C#側(`SaGlitchProcessor`)で`シード + アニメーション速度 × フレーム番号 × 0.05`を実効シードとして毎フレーム計算し、GPUへ渡す前に折り込んでいる(`GlitchMath.EffectiveSeed`)。0のままなら常に同じグリッチパターン、大きくするほど毎フレームでパターンが変わる。

## 3. 実装ファイル

- `Shaders/GlitchChroma.hlsl`: 唯一のピクセルシェーダー。帯ハッシュ・色収差・HSV彩度/色相・スキャンライン・ビネットをすべてここで計算する。
- `GlitchChromaEffect.cs`: `D2D1CustomShaderEffectBase`のC#ラッパー。定数バッファのレイアウトと、グリッチ変位ぶんの入力ハロ(`MapOutputRectToInputRects`)を管理する。
- `SaGlitchEffect.cs`: YMM4のプロパティ(UI)定義。
- `SaGlitchProcessor.cs`: `IVideoEffectProcessor`実装。フレームごとにアニメーション値を評価してシェーダー定数へ反映する。
- `GlitchMath.cs`: シード合成とハッシュ関数の純粋なC#実装(GPU不要でテストできるようにHLSL側と同じ式を再実装したもの)。
- `ShaderResourceLoader.cs`: コンパイル済み`.cso`の埋め込みリソース読み込み。
- `prototype/glitch_reference.py`: NumPyによるプレビュー用近似リファレンス実装(下記参照)。

## 4. ビルド・インストール

Sa_aohueのYMM4版と同じ手順です。GitHub Actionsの「YMM4 build」が公式YMM4 LiteのDLLを取得し、Windows SDKのfxcで`GlitchChroma.hlsl`をコンパイルします。成功した実行のArtifactsから`SaGlitchYmm`をダウンロードできます。

ローカルでビルドする場合(Windows、.NET 10 SDK、Windows SDKのfxc.exeが必要):

```powershell
dotnet build SaGlitchYmm.csproj -c Release "-p:YMM4DirPath=C:\path\to\YMM4\" "-p:FxcPath=C:\path\to\fxc.exe" "-p:D2DIncludePath=C:\path\to\sdk\um"
./check-shaders.ps1 -FxcPath C:\path\to\fxc.exe
```

生成された`SaGlitchYmm.dll`をYMM4の`user/plugin/SaGlitchYmm/`へコピーし、YMM4を再起動すると「フィルタ」カテゴリに`Sa_Glitch`が追加されます。

## 5. 検証

- `dotnet run --project tests/KernelTests.csproj -c Release`: `GlitchMath.EffectiveSeed`(シード合成式)と`GlitchMath.Hash11`(帯の乱数が常に[0,1)に収まること)を検証する、GPU不要の純粋な数値テスト。
- `python prototype/glitch_reference.py --selfcheck`: NumPyリファレンス実装の自己整合性(無変化パラメーターで完全パススルーになること、`hash11`の値域、出力形状・値域、シード違いで模様が変わること)を検証する。
- `check-shaders.ps1`: コンパイル済みシェーダーの定数バッファのバイトオフセットと、Direct2Dの座標入力(`SCENE_POSITION`/`TEXCOORD`)を検証する。
- **YMM4実機での描画、プロジェクトの保存・再読込、アニメーション、GPU別の動作確認は未実施です。** 上記はいずれもオフラインの数値・レイアウト検証であり、実際にYMM4上でエフェクトを適用した見た目を保証するものではありません(この開発環境にはWindows/YMM4がなく、実機確認ができません)。

## 6. `prototype/glitch_reference.py`について

このシェーダーとほぼ同じ計算(帯ハッシュ・色収差・HSV彩度/色相・スキャンライン・ビネット)をNumPyで再実装し、静止画にかけたプレビューを生成するためのスクリプトです。GPU側は双線形サンプリング(`D2DSampleInputAtPosition`)、このスクリプトは最近傍サンプリングという違いがあるため、**ピクセル完全一致のリファレンスではなく近似のプレビュー生成ツール**です(Sa_aohueの`check_native.py`のような厳密な数値対応は取っていません)。

```bash
pip install numpy pillow
python prototype/glitch_reference.py input.png output_dir            # 全プリセットを書き出す
python prototype/glitch_reference.py input.png output_dir --preset chaos --preset datamosh
```

用意しているプリセット: `default` / `subtle_vhs` / `datamosh` / `retro_crt` / `psychedelic` / `chaos`。

## 7. 残っている問題

- YMM4実機・実プロジェクトでの動作確認は未実施(ビルド環境にWindowsがないため)。
- 双線形サンプリングを前提にしたシェーダーと、最近傍サンプリングのPythonリファレンスは完全には一致しない。
- アルファ・プリマルチプライは簡略化しており(不透明な画像を想定)、透明部分の縁の挙動はSa_aohueほど厳密には検証していない。
- グリッチの乱数はハッシュベースの疑似乱数であり、暗号的な強度や統計的検定は行っていない(見た目の面白さのみを目的とする)。
