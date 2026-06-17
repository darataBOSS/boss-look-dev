# Changelog

All notable changes to this project will be documented in this file.

## [0.9.3] - 2026-06-17

### Removed — コースティクスをツールから撤去
- 水中表現（コースティクス等）はプロジェクト側のシェーダ/VFXで設定する方針に統一。ツールはライティング/ベイク/カラー/フォグ/背景/State の土台に専念。
- データ: `LightRigConfig` から `useCaustics` / `causticsCookie` / `causticsCookieSize` を削除。
- ロジック: `LightRigAuthoring` の cookie 割り当て処理（`ApplyCausticsCookie` と呼び出し）を削除。
- プリセット: 各プリセットの caustics 設定行を削除（深海/海中など）。
- UI: 「2.ライティング」リグのコースティクス トグル欄を削除。
- ドキュメント: 強化チェックリストのコースティクス項目を「プロジェクト側で用意」する手順として記載（cookie＋UVスクロール or 専用シェーダ）。
- 検証: 両環境（URP / Built-in）CSエラー0、全 LookDev DLL 再ビルド確認。

## [0.9.2] - 2026-06-16

### Changed — 背景・環境光カードの分かりやすさ改善
- トグルを「背景・環境光をツールで上書きする（OFF＝今のまま）」に改名。**OFF時に「ツールは触らない（今のスカイボックス/環境光のまま）」と明示**。
- 背景/環境光モードが Skybox のときは「下の色は無効」と注記（色を動かしても変わらない理由を明示）。
- OFF 時は「適用」ボタンも無効化。

## [0.9.1] - 2026-06-16

### Added — 遠景の霞み（HDRI使用時の距離グラデ）
- **フォグ色を環境(HDRI)から自動取得** (`AtmosphereOps.SuggestFogColorFromEnvironment` / 「5.フォグ」に自動ボタン): 環境光プローブをサンプルし霞色をHDRIに合わせる→地形の距離フォグとHDRI遠景が色で繋がり連続グラデに。
- **遠景の霞み (skyboxHaze)**: HDRIスカイボックスを暗く＋フォグ色にtintして遠景を霞ませる(画像加工なし)。「2.ライティング」にスライダ。深海プリセット=0.6/海中=0.4で自動設定。`LightingBakeOps.ApplySkyboxHaze`。
- 検証: 両環境CSエラー0、haze(露出ダウン＋色tint)と環境フォグ色取得を実走確認。

## [0.9.0] - 2026-06-16

### Added — 室内/室外ゾーン対応（深海の観測ルーム向け）
- **2ゾーンのプローブ**: 既存の「メイン(室外/海)」に加え、**室内ゾーン**のライトプローブ＋リフレクションプローブを生成可能に（`useInteriorZone`）。動くアバターが室内=暖色/室外=青へ**位置で自動補間**（ランタイム切替コード不要）。
- **Static 対象の選択**: `StaticTargetMode`(All / ExcludeLayers)。ExcludeLayers で動的レイヤー(アバター/魚)を Static から除外→ライトプローブで光る。`ApplyStaticFlagsToScene`→`ApplyStaticFlags(look,...)` に変更。
- ウィンドウ: 「Static(ベイク対象)」「室内ゾーン」UI、LayerMask フィールド、選択範囲からの室内 Bounds 設定。
- docs/underwater-room.md（観測ルーム手順書）。
- 検証: 両環境 CSエラー0、URP で Static除外＋室内/室外プローブ生成を実走確認。

## [0.8.4] - 2026-06-16

### Fixed
- **プリセットがスカイボックスを差し替えない**: 海プリセット(深海/海中)が背景を縦グラデに強制置換し、VR でスカイボックスが消える問題を修正。プリセットは色/フォグ/光/コースティクスのみを適用し、背景(スカイボックス)はユーザーの設定を維持。水中の暗い背景が欲しい場合は「3. 背景・環境光」で明示的に ON にする方式に（プリセットはグラデ値を用意するが既定 OFF）。

## [0.8.3] - 2026-06-16

### Fixed
- **コースティクスの設定経路を復活**: 0.8.2 で通常UIから完全に消してしまい設定できなくなっていた問題を修正。リグ欄に「🌊 水中表現 (コースティクス)」トグルを追加（既定 OFF＝通常はクリーン）。ON で Cookie 欄が出る。**海プリセット（深海 / 海中）を選ぶと自動で ON＋展開**され、設定方法が分かるように（非海プリセットは OFF に戻す）。`LightRigConfig.useCaustics` 追加、リグ適用は useCaustics 連動。

## [0.8.2] - 2026-06-16

### Changed — UX（フィードバック反映）
- **海専用UI（コースティクス）を通常画面から撤去**: プリセット領域の任意機能という位置づけにし、デフォルトのライト/リグ UI をクリーンに（混乱回避）。データ/適用ロジックは将来のプリセット用に温存。
- **VR↔MR の手順書(.md)自動生成を廃止**。代わりに見れば分かる形へ:
  - `BossLookDevState` に**カスタム Inspector** を追加し、切替方法を直接表示（`BossLookDevStateEditor`）。
  - エンジニア向け説明を **`docs/vr-mr-switching.md`**（Git）に集約。README からリンク。

## [0.8.1] - 2026-06-16

### Fixed
- **AR 背景透過で Lighting の Skybox を実際に外す**: 単色(SolidColor)モード/「AR: 背景を透過」で `RenderSettings.skybox` を null にするように（従来はカメラの clear flags のみで、Lighting タブに skybox が残り STYLY 等で AR にならない懸念があった）。Skybox モードに戻すと HDRI スカイボックスを復元。ベイク済み ambient/reflection は保持。

### Added
- **ベイクのクリア**: 「2. ライティング」のベイク欄に「ベイクをクリア（再調整用）」ボタン＋ベイク済み状態表示。`Lightmapping.Clear()` でリアルタイム表示に戻し、調整 → 再ベイク、というワークフローに。焼かれたままで変化が見えない問題を解消。

## [0.8.0] - 2026-06-16

### Changed — UX 改善（フィードバック反映）
- **プリセットを独立セクション化**: ウィンドウ上部に「★ プリセット（出発点を選ぶ）」を新設。`LookPresetLibrary` の組み込みプリセット（ニュートラル / 深海(神秘) / 海中(明るい) / シネマティック）をワンクリック適用。設定スライダ群とは別の場所に分離し、深海等のプリセットが見つけやすく。
- **カードに番号付け**（1 クイックスタート → 2 ライティング → 3 背景・環境光 → 4 カラー → 5 フォグ → 6 ARグラウンドシャドウ → 7 VR↔MR → 8 検証）。旧ツールの番号制に近い分かりやすさ。
- **AR の背景透過を明確化**: 「背景・環境光」カード先頭に「VR: スカイボックス表示」「AR: 背景を透過」ボタンと現在状態表示を追加。AR でスカイボックスを消す操作がワンクリックで分かるように。

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
