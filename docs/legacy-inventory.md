# legacy-inventory — 旧 `boss-look-preset` 機能棚卸し

> フェーズ1 成果物（実装指示書 v2 §8-1）。旧ツール `boss-look-preset`（v0.10.1 / Editor 専用 / 19ファイル・約5,100行）を **READ ONLY** で全読し、機能を1つ残らず列挙して **継承 / 落とす / 保留** を判定する。
> このドキュメントは判定の **提案** であり、特に「🔴 落とす」候補は **ユーザーの承認を得てから確定**する（黙って消さない）。コードはまだ書いていない。

---

## 0. 旧ツールの要約

- 正体: HDRI 環境光ベイク + 3点ライトリグ + ポストプロセスを **8ステップのウィザード**で半自動セットアップする Unity Editor 拡張。STYLY の WebAR / アプリ向けの「ルック」整形ツール。
- パイプライン: Built-in RP / URP を `GraphicsSettings.defaultRenderPipeline` で自動判定。
- 構成: 単一 `ScriptableObject`（`BOSSLookPreset`）に全設定を保存。`Ops/*` が静的ヘルパ群、`BOSSLookWizard` が IMGUI UI。
- 状態機械: `NotCreated → Active → Finalized(AR化)`。
- すべて **Editor 時のみ**（Runtime コンポーネントは一切なし）。

### 新ツール (`boss-look-dev`) との関係（再設計の方向）
旧ツールは「STYLY WebAR 向け・単一プリセット・ステップウィザード・Editor 専用」。新ツールは指示書 §3 の **4層アーキテクチャ**（A データモデル / B 合成エンジン / C パイプラインアダプタ / D リゾルバ）＋ **Runtime 層（VR↔MR）**＋ **Target=SelfApp/STYLY** へ再設計する。旧コードは **コピーせず**、ドメインロジックを理解した上で各層へ **明示移植**する。

---

## 1. 凡例

**判定**
- 🟢 **継承** — 新ツールに持ち込む（多くは再設計・移植先の層を併記）。
- 🔴 **落とす** — 新 v1 に持ち込まない候補。**理由を明記し、要ユーザー承認**。
- 🟡 **保留** — 持ち込み方／要否が設計上の論点。フェーズが進んでから確定。

**移植先レイヤー（指示書 §3.4）**
- `A` データモデル / `B` 合成・適用エンジン / `C` パイプラインアダプタ / `D` リゾルバ / `R` Runtime（VR↔MR・SelfApp）/ `E` Editor UI / `共通コア` パイプライン非依存 / `インフラ` ビルド構成等

---

## 2. 全機能リスト（モジュール別）

### Module A — 環境光 / ベイク（`EnvironmentOps` ほか）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| A1 | プリセット作成/ロード（SO＋フォルダ生成・既定値書込） | `CreateOrLoadPreset` | 🟢 継承 | A/E | 新ではルックデータモデル(A)の保存に再設計。フォルダ規約は踏襲。 |
| A2 | HDRI スカイボックス生成（Panoramic/Cubemap 自動判定） | `SetupSkybox` | 🟢 継承 | 共通コア | §4.0「HDRI/IBL は VR リアルの土台」。VR=有効、AR/MR=無効と context で分岐。 |
| A3 | スカイボックス 回転/露出/環境光強度 ライブ適用 | `ApplyEnvironmentLive` | 🟢 継承 | 共通コア/B | ライブプレビュー(§4.1)の土台。 |
| A4 | Lighting Settings 生成/更新（Progressive・autoGenerate OFF・bakedGI） | `CreateOrUpdateLightingSettings` | 🟢 継承 | 共通コア | §4.0「全ベイク必須」。ベイクプリセット(§4.1)の核。 |
| A5 | Static フラグ付与 ContributeGI ＋ 対象解決（ManualList/LayerMask/Tag） | `ApplyStaticFlags`/`ResolveStaticTargets` | 🟢 継承 | 共通コア | ベイクの前提。 |
| A6 | ライトプローブグリッド生成（spacing/縦層/collider skip） | `CreateOrUpdateProbeGroup` | 🟢 継承 | 共通コア | §4.0 light probe。 |
| A7 | リフレクションプローブ生成（Baked/box projection/解像度/HDRI or NeutralReplace） | `CreateOrUpdateReflectionProbe` | 🟢 継承 | 共通コア | §4.0「reflection probe は質感のため必須」＋ §4.1 probe アシスト。 |
| A8 | ベイク実行/キャンセル（BakeAsync）＋ CanBake ガード | `StartBake`/`CancelBake`/`CanBake` | 🟢 継承 | 共通コア | |
| A9 | AR化(Finalize=skybox外し) / 解除(Unfinalize) ＋ 状態機械 | `Finalize`/`Unfinalize` | 🟢 継承（必須） | A/E（STYLY）, R（SelfApp） | **STYLY モバイルAR は静的でランタイムC#不可（§3.5）なため、エディタ時の「skybox付きでベイク→skybox外して実世界を見せる」AR化は必須**。旧ロジックをそのまま明示移植。Pico SelfApp では同じ考え方を VR↔MR State トグル（camera clear flags 差分・§5.1）として実現。**保留なのは内部設計（`NotCreated/Active/Finalized` 状態機械をそのまま使うか Target+State 体系へ整理するか）だけで、機能自体は残す**。→ §4-(b)。 |
| A10 | おまかせセットアップ（Step1-5 一括＋シーンバウンド自動） | `AutoSetupAll` | 🟢 継承 | B/E | §4.1「良い既定値/クイックスタート」の主役。最重要。 |
| A11 | シーン全体バウンドからプローブ範囲自動算出 | `AutoSetupAll` 内 / `TrySetProbeAreaFromSelection` | 🟢 継承 | B | |

### Module B — ライト（`LightRigOps` / `HdriSunOps`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| B1 | 3点照明リグ（Key/Fill/Back・Spot/Area・Kelvin・key:fill比） | `BuildThreePoint` | 🟢 継承 | 共通コア | §4.1 ライティングリグ studio。土台。 |
| B2 | 太陽リグ（Directional＋sky fill・高度/方位・昼/朝夕/曇りプリセット） | `BuildSun`/`ApplySunPreset` | 🟢 継承 | 共通コア | |
| B3 | シーリンググリッドリグ（rows×cols・Spot/Point/Area・全Baked） | `BuildCeilingGrid` | 🟢 継承 | 共通コア | 広い室内向け。 |
| B4 | HDRI から太陽向き推定（最輝点探索・readable一時化） | `HdriSunOps` | 🟢 継承 | B | HDRI-IBL アシストと相性良。 |
| B5 | 既存 Directional ライト 退避/復元/削除（scenePath 再リンク） | `Stash/Restore/Delete` | 🟢 継承 | 共通コア | リグ導入時の事故防止。 |
| B6 | リグタイプ切替時の不一致ライト掃除 | `ClearMismatchedLights` | 🟢 継承 | 共通コア | |
| B7 | 色温度有効化（GraphicsSettings linear/colorTemp） | `EnableColorTemperature` | 🟢 継承 | 共通コア/E | validator でも警告(§4.3)。 |

### Module C — ポスト（`PostProcessOps` / `…Builtin` / `…URP`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| C1 | RP 自動判定でバックエンド切替（PPv2 / URP Volume） | `PostProcessOps` dispatcher | 🟢 継承 | C | 指示書 §3.3 の color アダプタそのもの。 |
| C2 | PPv2 セットアップ（Profile＋Volume＋Layer・Resources自動探索） | `PostProcessOpsBuiltin.Setup` | 🟢 継承 | C(BuiltIn) | |
| C3 | URP Volume セットアップ（Volume＋Profile・camera post ON） | `PostProcessOpsURP.Setup` | 🟢 継承 | C(URP) | |
| C4 | カラーグレード（露出/コントラスト/彩度/フィルタ/色温度/ティント）ライブ | `ApplyEffectToggles`/`ReapplyProfile` | 🟢 継承 | B/C | §4.0「color レイヤーの主役」。v1 レイヤー lighting＋color。 |
| C5 | Bloom（強度・threshold・mobile/HDR分岐） | `EnsureEffect<Bloom>` | 🟢 継承 | C | 安く効く。 |
| C6 | Vignette | `EnsureEffect<Vignette>` | 🟢 継承 | C | |
| C7 | STYLY モバイル LDR グレードモード（Gamma 対応・ACES回避） | `ApplyEffectToggles` mobile 分岐 | 🟡 保留 | C(BuiltIn)/R | 指示書 §3.5「STYLY の color はベイク＋STYLY側方式に委ねる前提」。boss-look-dev の color 層を STYLY にそのまま乗せない設計と、旧 LDR 層の扱いを擦り合わせ要（→ §3-(c)）。 |
| C8 | プロファイルのサブアセット永続化修正（AddObjectToAsset）＋ CleanProfile | `EnsureEffect`/`CleanProfile` | 🟢 継承 | C | 既知バグ修正の実績。移植時に必ず引き継ぐ。 |
| C9 | ポスト除去（両系統クリーン） | `Remove` | 🟢 継承 | C | |
| C10 | **Depth of Field** | `EnsureEffect<DepthOfField>` | 🔴 落とす | — | §4.0「heavy screen-space は避ける」。モバイルXR・ステレオで重く不自然。→ §3-(a) 要承認。 |
| C11 | **Motion Blur** | `EnsureEffect<MotionBlur>` | 🔴 落とす | — | XR では酔いの原因で通常無効。同上。→ §3-(a) 要承認。 |
| C12 | **Ambient Occlusion（SSAO）を有効効果として提供（既定ON）** | `postAOEnabled`/`EnsureEffect<AmbientOcclusion>` | 🔴 落とす（効果としては） | E(validator) | §4.0「SSAO は避ける」§4.3「Pico/Quest で SSAO を警告」。**効果としては落とすが、validator の検出項目としては継承**（E12参照）。→ §3-(a) 要承認。 |

### Module D — 仕上げ（`GroundShadowOps` / `FogOps`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| D1 | AR グラウンドシャドウ（透明シャドウキャッチャー・シェーダ自動生成 Built-in/URP別・足元配置/サイズ） | `GroundShadowOps` | 🟡 保留 | C/B（AR/STYLY context） | AR の接地感に有効だが、MR(Pico) は occlusion/spatial mesh(v1.x) が本筋。AR(STYLY)向けに残すか、MR では非対応とするか要判断（→ §3-(d)）。URP シェーダは §7 のシェーダ移植注意に該当。 |
| D2 | 影の濃さ ライブ調整 | `ApplyOpacity` | 🟡 保留 | C/B | D1 に従属。 |
| D3 | 距離フォグ（mode/color/density/start-end） | `FogOps.Apply` | 🟢 継承 | 共通コア/A | §5.1「VR=fog あり / MR=オフ」が State 差分パラメータの実例。atmosphere レイヤー。 |
| D4 | フォグ色を ambient probe から自動算出 | `SuggestColorFromEnvironment` | 🟢 継承 | B | 良い既定値アシスト。 |

### Module F — 品質（`QualityOps`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| F1 | アンチエイリアス（None/FXAA/SMAA/MSAA 2/4/8x・RP別ルーティング） | `ApplyAntiAliasing` | 🟢 継承 | C/共通コア | §4.0「post AA より MSAA(4x目安)」。リアル質感レバー。 |
| F2 | 影品質（距離/カスケード/解像度/ソフトシャドウ・RP別） | `ApplyShadows` | 🟢 継承 | C/共通コア | |
| F3 | 影距離の自動提案（被写体/シーンスケール） | `SuggestShadowDistance` | 🟢 継承 | B | |
| (F4) | レンダースケール / adaptive resolution | （旧ツールに無し） | — | — | §4.0 で要求。新規実装（棚卸し対象外・参考）。 |

### Module E — 診断（`LintOps`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| E1 | 色空間チェック（Linear/Gamma × 出力ターゲット） | `CheckColorSpace` | 🟢 継承 | E(validator) | §4.3 ターゲット別事故検出。Target=STYLY/SelfApp に再マップ。 |
| E2 | Camera HDR チェック | `CheckCameraHdr` | 🟢 継承 | E(validator) | |
| E3 | 色温度設定チェック | `CheckColorTemperature` | 🟢 継承 | E(validator) | |
| E4 | プローブ未生成チェック | `CheckProbes` | 🟢 継承 | E(validator) | §4.3「reflection probe 欠落・未ベイク」検出。 |
| E5 | 純白アルベド検出＋0.85修正 | `CheckWhiteAlbedo` | 🟢 継承 | E(validator)/B | §4.1 マテリアル底上げと連動。 |
| E6 | 強すぎ Emission 検出 | `CheckStrongEmission` | 🟢 継承 | E(validator) | |
| E7 | ミップマップ無効テクスチャ検出＋修正 | `CheckMipmaps` | 🟢 継承 | E(validator) | |
| E8 | UV2(ライトマップUV)欠落検出＋GenerateUV修正 | `CheckMissingUv2` | 🟢 継承 | E(validator) | |
| (E9) | 新規: STYLY で PPv2/カスタムC#/非Default layer 検出 | （旧に無し） | — | E(validator) | §4.3 で要求（参考）。 |
| (E10) | 新規: Pico/Quest で SSAO/HDR/Decal/重 screen-space 検出 | （旧に無し・C12をここへ昇格） | — | E(validator) | §4.3。SSAO 検出は C12 の validator 継承分。 |
| (E11) | 新規: MR 状態に fog 残存検出 | （旧に無し） | — | E(validator) | §4.3。 |

### Module G — ★ルック / ライブラリ（`LookStyle*` / `LookBakeOps`）

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| G1 | 組み込みルック4種（シネマティック/明るい物販/ムーディ/ビビッド） | `LookStyleLibrary.BuiltIn` | 🟢 継承 | A/D | §4.4 lookbook の初期資産＋§4.1 良い既定値。 |
| G2 | ルック適用（強さスライダでニュートラルから線形ブレンド） | `LookStyleOps.Apply` | 🟢 継承 | B/D | §4.4「ブレンド可能」の原型。新では2プリセット間ブレンドへ拡張(§8-3)。 |
| G3 | カスタムルック 保存/読込/削除（Looks/ .asset） | `Capture`/`SaveCustom`/`LoadCustom` | 🟢 継承 | A/D | §4.4 保存・整理・読込。 |
| G4 | STYLY モバイル: ライティング/環境光への焼き込み層 | `LookBakeOps` | 🟡 保留 | B/C | C7 と同じ論点。STYLY の color を「ベイク＋STYLY側方式」に委ねる新方針との整合（→ §3-(c)）。 |
| G5 | 出力ターゲット切替（GeneralLinear / STYLYMobileGamma） | `OutputTarget` enum | 🟢 継承（再設計） | A | 新の **Target = SelfApp / STYLY**（§3.2）へ発展。STYLY=Built-in固定・カスタムC#不可・PPv1 制約を付与。 |

### 横断・インフラ

| # | 機能 | 旧実装 | 判定 | 移植先 | 理由 / 備考 |
|---|---|---|---|---|---|
| X1 | RenderPipelineDetector（Built-in/URP/HDRP/Unknown 判定） | `RenderPipelineDetector` | 🟢 継承 | C/インフラ | §7「currentRenderPipeline で判定→アダプタ選択」。HDRP は検出のみ（アダプタ無し・警告）。 |
| X2 | SceneRelinkOps（名前規約で asset→scene 参照を再リンク） | `SceneRelinkOps` | 🟢 継承 | E/共通コア | ドメインリロード対策の実績ロジック。 |
| X3 | シーン整理（生成物を `BOSS Look [name]` コンテナに集約） | `OrganizeUnderContainer` | 🟢 継承 | E | ヒエラルキー整頓。 |
| X4 | asmdef ＋ versionDefines（PPv2/URP/SRPCore ガード） | `*.asmdef` | 🟢 継承（再設計） | インフラ | §7・§10 の分割 asmdef（Core/URP/BuiltIn/Runtime/Pico/Meta/Editor）へ拡張。 |
| X5 | カラフル UI ヘルパ（Card/役割色ボタン/Pill/Hint） | `BOSSLookUI` | 🟢 継承 | E | §11 UI/UX 要件（カラフル・色分け）の土台。 |
| X6 | ステップ式ウィザード（8ステップ・✓完了・自動ジャンプ・Next/Back） | `BOSSLookWizard` | 🟡 保留 | E | 新は「リアルなルックを速く作る」ループ(§4)中心。8ステップ直列が新UIに合うか要再設計（→ §3-(e)）。 |
| X7 | 最後に使ったプリセット記憶（EditorPrefs）＋単一自動ロード | `TryAutoLoadPreset` | 🟢 継承 | E | |
| X8 | シーン名/場所からの自動命名 | `ApplySceneBasedDefaults` | 🟢 継承 | E | |
| X9 | プローブ範囲のシーンビュー ギズモ編集（BoxBoundsHandle） | `OnSceneGUI` | 🟢 継承 | E | |
| X10 | UPM 構成（package.json/README/CHANGELOG/LICENSE） | ルート | 🟢 継承（新規作成） | インフラ | id=`com.daratabos.boss-look-dev`、git URL は新リポジトリ。 |

### 新ツールで新規追加（旧ツールに存在しない・棚卸し対象外、参考）
指示書で要求される主な新規: ④層アーキテクチャ、Runtime 層（VR/MR 2状態・State トグル・SelfApp の `BossLookDevState`＋transition）、Target別リグ・エミッタ（SelfApp/STYLY）、ハンドオフ手順書 自動生成、before/after トグル、スナップショット/履歴、2プリセット間ブレンド、material アダプタ（Standard / URP-Lit）、`IPassthroughStateProvider`（任意）、レンダースケール/adaptive resolution、location key の余地（v1未実装）。

---

## 3. 🔴「落とす」決定 — ✅ ユーザー承認済み（2026-06-16）

> 指示書 §8-1・§12：機能を落とす判断は **必ず理由を日本語で説明し、ユーザーの許可を得てから実行**する。
> **本節 (a) はユーザーが「提案どおり落とす」を承認済み。** DoF・Motion Blur は v1 のポスト効果から除外し、SSAO は効果としては除外して validator 検出（E10）へ降格する。

### (a) ポスト3効果を v1 から外す候補：Depth of Field / Motion Blur / SSAO(AO)
- **対象**: C10 Depth of Field、C11 Motion Blur、C12 Ambient Occlusion（SSAO）を **選択可能なポスト効果としては提供しない**。
- **理由**:
  - 指示書 §4.0 が明確に「**SSAO・Decal・heavy screen-space は避ける**」「post AA より MSAA」と方針づけている。DoF / Motion Blur / SSAO はいずれも heavy screen-space で、モバイル級（Pico/Quest URP・STYLY モバイル）では負荷・画質ともに割に合わない。
  - VR/MR では DoF・Motion Blur はステレオ視・酔いの観点で通常無効にする効果。XR ルックツールが既定で抱えるべきレバーではない。
  - §4.3 はむしろ「**Pico/Quest で SSAO を作ったら警告する**」=検出対象としており、提供する効果と矛盾する。
- **ただし SSAO は validator のチェック項目（E10）としては継承**する（「事故設定を作った時点で警告」§4.3）。DoF/Motion Blur は v1 では完全に対象外。
- **代替**: リアルな陰影は §4.0 の全ベイク＋AO（Lightmapper の `settings.ao`、=A4 で継承済み）で出す。SSAO のリアルタイム代替はベイク AO。
- **承認のお願い**: 「DoF・Motion Blur を v1 のポスト効果から外す／SSAO は効果としては外し validator 検出に降格」で問題ないか。要れば §9 非ゴール準拠で「任意・既定オフ」として v1.x 送りにもできます。

---

## 4. 🟡「保留」論点 — 設計判断が要るもの

確定ではなく、フェーズ2以降で設計を詰める論点。落とすわけではない。

- **(b) AR化 / 状態機械（A9）**: 機能自体は 🟢 継承・必須（上記 A9）。**STYLY モバイルAR は静的・ランタイムC#不可なので、エディタ時の「skybox付きでベイク→skybox を外して実世界を見せる」AR化は確実に残す**。論点は内部設計のみ:（i）旧の `NotCreated/Active/Finalized` 状態機械をそのまま使うか、Target(STYLY/SelfApp)＋State 体系へ整理し直すか。（ii）Pico SelfApp 側は同じ skybox 表示/非表示を **VR↔MR State トグル**（camera clear flags=Skybox/passthrough 透過の差分・§5.1）で実現するため、STYLY のエディタ時 Finalize と SelfApp のランタイム State を **同一データ（skybox 表示フラグ）から両方吐けるよう**揃える。
- **(c) STYLY モバイル LDR グレード層（C7）＋ ライティング焼き込み（G4=`LookBakeOps`）**: 旧は「Gamma で LDR ポスト＋環境光焼き込み」の2層で color を効かせた。新方針 §3.5 は「STYLY では color grading を **STYLY 側の方式に委ねる**、boss-look-dev の color 層(PPv2前提)はそのまま乗せない」。→ 焼き込み層(`LookBakeOps`)は **STYLY 向けの数少ない有効手段**なので残す価値が高い一方、LDR PPv2 層は STYLY モバイルでは外す可能性。擦り合わせて確定する。
- **(d) AR グラウンドシャドウ（D1/D2）**: AR(STYLY) の接地感には有効。MR(Pico) は occlusion/spatial mesh（v1.x）が本筋で、シャドウキャッチャーは passthrough と相性が読めない。→ **AR/STYLY context 限定で継承**し、MR では非提供（or v1.x）とする案。URP シェーダ自動生成は §7 のシェーダ移植注意に沿って URP-HLSL で再確認。
- **(e) 8ステップ直列ウィザード（X6）**: 旧は「環境光を 0→7 で順に組む」導線。新は §4「リアルなルックを速く作って即見えて焼ける」ループ＋ §11 のカラフル・直感 UI が主目的。直列ウィザードのままが最適か、レイヤー(lighting/color/atmosphere)＋ Target ＋ State を軸にしたタブ/カード構成に作り替えるかを設計時に決める。

---

## 5. 申し送り（フェーズ2へ）

- 🟢 継承は **コピーせず明示移植**。共通コア（A2–A8, B1–B7, D3–D4, F1–F3）はパイプライン非依存の1実装に集約（指示書 §3.3）。
- color/material はアダプタ層(C)へ、validator(E1–E8)は Editor 層へ、ルック資産(G1–G3)はデータモデル(A)＋リゾルバ(D)へ。
- C8 の永続化バグ修正は移植時に **必ず**引き継ぐ（実績のある回避策）。
- 🟡 保留 4件（b–e）と 🔴 落とす候補（a）は、本ドキュメントへのユーザー承認後にフェーズ2の足場設計へ反映する。
- 旧リポジトリ `boss-look-preset` は本フェーズ中、**一切書き込んでいない**（READ ONLY 厳守）。
