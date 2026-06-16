# Changelog

All notable changes to this project will be documented in this file.

## [0.7.0] - 2026-06-16

### Added — スタイライズ背景・環境光 / コースティクス / 強化チェックリスト（深海プリセット駆動）
- **BackgroundSection**: 背景（Skybox / 単色 / 縦グラデ）＋環境光（Skybox / Flat / Gradient=Trilight）を Look に保存。`BackgroundOps` が適用、縦グラデは生成スカイボックスシェーダ（Built-in/URP 両対応）。深海等のスタイライズ VR が **1アセットで自己完結・再現可能**に。
- ライトリグに **caustics cookie スロット**（主ライトへ静的割り当て。スクロールアニメは強化チェックリスト参照）。
- **EnhancementChecklistGenerator**: 「用意するとより海らしくなる」プロジェクト側タスク（コースティクス/ゴッドレイ/パーティクル/PBR/height fog/反射/快適性）を日本語 Markdown で自動生成。
- ウィンドウ: 「背景・環境光」カード刷新、リグに caustics スロット、検証カードに強化チェックリスト生成ボタン。
- QuickStart: HDRI 未設定時は既存シーンのスカイボックスにフォールバック。
- docs/usage.md（使い方ガイド）。
- 検証: 両環境 CS エラー0、URP で深海ルック（グラデ背景・環境光・寒色グレード・フォグ）を実走しスクショ確認。

## [0.6.0] - 2026-06-16

### Added — ポスト詳細設定 (カラー「詳細設定」折りたたみ)
- ColorSection に詳細ノブ追加: Bloom(threshold/scatter/tint)・Vignette(smoothness/roundness/color)・hueShift・Tonemapping 切替(None/Neutral/ACES)・LUT(カラールックアップ)+強度。
- URP/PPv2 両アダプタにマッピング。Tonemapping は SelfApp/HDR のみ。LUT は URP=常時(ColorLookup) / PPv2=LDR(STYLY)のみ(ldrLut)。`roundness` は PPv2 のみ(URP Vignette に無いため URP では非適用)。
- ウィンドウのカラーカードに「詳細設定」折りたたみ (SelfApp ポスト時のみ表示)。DoF/MotionBlur/SSAO は方針どおり非対応。
- 背景 (カメラ) カード追加: ベイク後の背景表示を **VR=スカイボックス表示 / AR=透過 (alpha0)** で切替 (ライティングは保持)。`LightingBakeOps.SetCameraBackground`。MR は State 切替で自動、本カードは単体プレビュー/静的AR用。
- 検証: test-URP・test-builtin で CS エラー0、URP 実行スモーク合格 (詳細ノブ込みで Volume+プロファイル6コンポーネント生成)。

## [0.5.0] - 2026-06-16

### Added — フェーズ5: VR↔MR ランタイム切り替え (v1 スコープ完成)
- Runtime `BossLookDevState` を実装: `OnEnable` で State の差分 (camera clear flags / fog / ambient / reflection) を適用、SelfApp はスムーズ transition (duration + curve) でブレンド。共有ベイク前提 (色グレード差分は後日)。
- `StateRigEmitter` (Target 別リグ生成): SelfApp = `<name> Switch` 配下に VR_State/MR_State を作り各 `BossLookDevState` を付与・片方のみアクティブ。STYLY = 宣言的リグ (BossLookDevState なし)。
- `HandoffDocGenerator`: 「こうすれば VR↔MR が切り替わる」を**日本語 Markdown で自動生成**。SelfApp=`SetActive`、STYLY=PlayMaker Activate Game Object に文面分岐。State 差分表も出力。
- ウィンドウに「VR↔MR 切り替え (State)」カード追加 (2状態の差分編集・transition・リグ生成・手順書生成)。
- 検証: test-URP・test-builtin で CS エラー0。実行スモーク合格 (SelfApp=2状態+コンポーネント+片方アクティブ、手順書生成、STYLY=コンポーネント除去)。
- バージョンを 0.5.0 に。

### v1 スコープ達成 (§9)
Pipeline: Built-in + URP / Context: VR + AR + VR↔MR / レイヤー: lighting + color (+ atmosphere + AR グラウンドシャドウ) / リアル制作コア・validator・lookbook (ブレンド+before-after) / Target = SelfApp・STYLY とリグ・エミッタ + ハンドオフ手順書 / VR/MR 2状態 (共有ベイク) + State トグル。

## [0.4.0] - 2026-06-16

### Added — フェーズ4: Built-in アダプタ + STYLY 対応
- Built-in color アダプタ `BuiltInColorAuthoringAdapter` (PPv2: Profile + PostProcessVolume + PostProcessLayer、Bloom/ColorGrading/Vignette、サブアセット永続化修正を移植)。STYLY=LDR グレード / SelfApp=HDR+ACES に分岐。
- Built-in material アダプタ `BuiltInMaterialAuthoringAdapter` (Standard シェーダの PBR 底上げ)。
- STYLY color 委譲: `LookBakeOps` (color 意図をスカイボックス tint/露出・環境光へ焼き込み、Gamma/ポスト無しでも残る) を移植。STYLY ターゲットでは PPv2 を出さず焼き込みを使用 (window / quickstart を分岐)。
- validator に STYLY 事故チェック追加 (Built-in 限定 / カスタムC#・PPv2 不可・Default layer の注意)。
- `AdapterRegistry` に Built-in color/material を登録。ウィンドウのカラーカードを STYLY/SelfApp で分岐。
- 検証: test-URP・test-builtin で CS エラー0。test-builtin に PPv2 を導入し実行スモーク合格 (Built-in PPv2 アダプタが Volume+Layer+エフェクト3個を永続生成、material アダプタ、STYLY 焼き込みすべて動作)。

## [0.3.0] - 2026-06-16

### Added — フェーズ3 完了 (increment 3b)
- 天井グリッドリグ (`RigType.CeilingGrid` / `GridLightKind`、行×列の下向き全 Baked ライト)。
- HDRI 太陽向き推定 `HdriSunOps` (equirectangular の最輝点から太陽リグの高度/方位を推定)。
- マテリアル底上げ: URP material アダプタ `UrpMaterialAuthoringAdapter` (白アルベド抑制 / 鏡面緩和 / detail keyword / GPU instancing)。`IMaterialAdapter` を登録制に。
- AR グラウンドシャドウ `GroundShadowOps` (透明シャドウキャッチャー + パイプライン別シェーダ自動生成)、`GroundShadowSection`。AR コンテキスト限定で UI 表示。
- ウィンドウに上記を統合 (グリッド params / 太陽向き合わせ / マテリアル底上げ / AR グラウンドシャドウ カード)。
- 検証: test-URP・test-builtin で CS エラー0。URP 実行スモーク合格 (グリッド6灯・グラウンドシャドウ+URPシェーダ生成・material アダプタ動作)。

## [0.2.0] - 2026-06-16

### Added — フェーズ3 (increment 3a): リアル制作コア + URP アダプタ
- データモデル拡張: `LightingSection` に bake / 環境HDRI / probe / reflection / `LightRigConfig`、列挙 `RigType`・`RigLightKind`・`ReflectionBakeMode`・`BakeBackend`。
- 共通コア制作ops（パイプライン非依存・Editor）: `LightingBakeOps`（HDRIスカイボックス/IBL・Lighting Settings・static flag・ライトプローブ・リフレクションプローブ・ベイク）、`LightRigAuthoring`（3点 / 太陽リグ）、`SceneBindingOps`（コンテナ集約・名前規約リンク）。
- URP color アダプタ実装 `UrpColorAuthoringAdapter`（Volume + Profile、Tonemapping/ColorAdjustments/WhiteBalance/Bloom/Vignette、サブアセット永続化修正を移植）。Editor asmdef 内に versionDefine ガードで配置（循環依存回避）。
- `QuickStartOps`（良い既定値の一括セットアップ + ベイクプリセット）、`LookValidator`（検証スケルトン）、`LookBlendOps`（2プリセット間ブレンド + before/after スナップショット）。
- カラフルなオーサリングウィンドウ刷新（クイックスタート / ライティング / カラー / アトモスフィア / ルックブック / 検証のカード式・§11）。
- 検証: test-URP(Unity6/URP17) と test-builtin(2022.3) で CS エラー0。さらに URP で実行スモークテスト合格（リグ3灯・probe・reflection・Volume+profile・コンテナ集約まで動作）。

## [0.1.0] - 2026-06-16

### Added — フェーズ2: 共通コアの足場
- 4層アーキテクチャの骨格を作成（指示書 §3.4 / §8-2）。
- データモデル骨格 `LookDefinition`（context / pipeline / target / lighting・color・atmosphere・overrides の optional セクション / VR・MR の汎用2状態 `ContextStateSet`）。
- (C) パイプラインアダプタ / (D) リゾルバ / モジュール / Runtime seam のインターフェース定義。
- 分割 asmdef ＋ Version Defines: `Boss.LookDev.Core` / `.Runtime` / `.BuiltIn` / `.URP` / `.Pico` / `.Meta` / `.Editor`。
- 空アダプタ（BuiltIn / URP）・空ベンダー seam（Pico / Meta）。コアはパイプラインパッケージに非依存。
- `docs/legacy-inventory.md`（フェーズ1 棚卸し） / `docs/architecture.md`（設計）。

### Notes
- 旧 `boss-look-preset` は READ ONLY 参照のみ（移植はフェーズ3以降、コピーせず明示移植）。
- 検証: URP プロジェクトと Built-in プロジェクト双方でのコンパイル確認は Unity 上で実施する。
