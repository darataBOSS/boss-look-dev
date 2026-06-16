# architecture — boss-look-dev 設計（フェーズ2: 共通コアの足場）

> 実装指示書 v2 §3（背骨）・§8-2（足場）に基づく設計。フェーズ2は **骨格と境界の確定**が目的で、中身（実ロジック移植）はフェーズ3以降。
> 旧 `boss-look-preset` のロジックは [legacy-inventory.md](legacy-inventory.md) の判定に従い、コピーせず明示移植する。

---

## 1. 4層 ＋ Runtime（指示書 §3.4）

```
(A) Look データモデル        … Boss.LookDev.Core           （Editor+Runtime / 依存なし）
(B) 合成・適用エンジン       … Boss.LookDev.Editor 中心 + 各モジュール
(C) パイプラインアダプタ層   … Boss.LookDev.BuiltIn / Boss.LookDev.URP（versionDefine ガード）
(D) プリセット解決リゾルバ   … Boss.LookDev.Runtime（リゾルバ）+ Editor（手動選択UI）
(R) Runtime 層（VR↔MR）      … Boss.LookDev.Runtime + ベンダー Boss.LookDev.Pico / .Meta
```

- **共通コア（パイプライン非依存・1実装）**: ベイク（Progressive Lightmapper・lightmap・light probe・reflection probe 基本）、static flag、light mode、environment/HDRI skybox・ambient SH。→ `Core`（データ）＋ `Editor`（ベイク等 UnityEditor 依存処理）。
- **パイプライン固有（アダプタ）**: color（Built-in=PPv2 / URP=Volume）、material（Standard / URP-Lit）。→ `BuiltIn` / `URP`。
- プリセットのポータビリティは非対応（§3.3）。`LookDefinition.targetPipeline` を持ち、現在のパイプラインと不一致なら**警告のみ**。

---

## 2. asmdef 構成（§7・§10）

| asmdef | platforms | 参照 | versionDefine ガード | 役割 |
|---|---|---|---|---|
| `Boss.LookDev.Core` | Editor+Runtime | （なし） | なし | データモデル・全インターフェース。パイプラインパッケージに依存させない。 |
| `Boss.LookDev.Runtime` | Editor+Runtime | Core | なし | `BossLookDevState`、リゾルバ、`IContextStateProvider` 等の Runtime 実体。 |
| `Boss.LookDev.BuiltIn` | Editor+Runtime | Core | `BOSS_LOOK_DEV_HAS_PPV2` | Built-in 用 color/material アダプタ。UnityEditor 依存は `#if UNITY_EDITOR`。 |
| `Boss.LookDev.URP` | Editor+Runtime | Core | `BOSS_LOOK_DEV_HAS_URP` / `..._SRPCORE` | URP 用 color/material アダプタ。 |
| `Boss.LookDev.Pico` | Editor+Runtime | Core, Runtime | `BOSS_LOOK_DEV_HAS_PICO` | passthrough 観測（任意自動追従）。v1 は seam のみ。 |
| `Boss.LookDev.Meta` | Editor+Runtime | Core, Runtime | `BOSS_LOOK_DEV_HAS_META` | Quest 用ベンダー seam。v1.x（空）。 |
| `Boss.LookDev.Editor` | Editor | Core, Runtime, BuiltIn, URP | なし | ベイク/リグ/probe/validator/エミッタ/UI。アダプタ選択（C）と手動選択（D）。 |

**コンパイル安全（§7）**: 最適パイプラインが未インストールでも通す。
- 旧ツールで実績のある方式を踏襲: 各アダプタ asmdef は対象パッケージのアセンブリを名前参照し、`versionDefines` で `#if` ガード。パッケージ不在時は define が立たず該当コードは全て除外され、未解決参照は警告止まりでコンパイルは通る。
- `package.json` は URP/PPv2 を **ハード依存にしない**（ホストプロジェクトが選ぶ）。

> ⚠️ Pico/Meta の versionDefine 用パッケージ名は SDK 確認後に確定（asmdef 内 TODO）。v1 では空 seam なので未確定でも通る。

---

## 3. データモデル（A）骨格

`LookDefinition : ScriptableObject`
- **メタデータ**: `name` / `description` / `targetContext`(VR/AR/MR) / `targetPipeline`(BuiltIn/URP) / `target`(SelfApp/STYLY) / `locationKey`(予約・v1未使用)
- **lighting**（`LightingSection` 必須・土台）: bake 設定 + environment/HDRI + probes + light rig 設定
- **color**（`ColorSection` optional）: grade（露出/コントラスト/彩度/フィルタ/色温度/ティント）+ bloom + vignette + `bakeIntoLighting`（STYLY 用焼き込み層フラグ）
- **atmosphere**（`AtmosphereSection` optional）: fog
- **overrides**（`OverridesSection` optional）: material overrides（v1 は予約 seam）
- **states**（`ContextStateSet`）: 共有ベイクの上に **名前付き汎用2状態**（v1 出荷は VR_State / MR_State）＋ transition
- ブレンド可能（2プリセット間）はフェーズ3で B に実装

**`ContextStateSet`（§5.1）**
- 汎用2状態 `StateA` / `StateB`（`ContextStatePair`）。決め打ちせず将来 AR↔VR 等も同機械に乗る seam。
- 各 State は **共有ベイクを変えずに実行時に変えられる差分のみ**（`StateDelta`）:
  - `skyboxVisible`（camera clear flags = Skybox / passthrough 透過）
  - `fog`（VR あり / MR 通常オフ）
  - `exposure` / `ambient`（MR は passthrough の明るさに寄せる）
  - `reflection`（参照先・強さ）
  - `colorGradeDelta`
- `TransitionSettings`: duration + AnimationCurve（SelfApp のスムーズ切替・v1）

---

## 4. 🟡 保留4論点の確定（フェーズ2での決定）

### (b) AR化 / 状態機械 → **データ駆動の skybox 表示フラグに一本化**
- 旧の `NotCreated/Active/Finalized` という硬い状態機械は**廃止**。
- 「skybox を外して AR 化」は `StateDelta.skyboxVisible` という **1つのデータ**で表す。
  - **STYLY（静的AR）**: STYLY エミッタが**エディタ時に Finalize**（skybox 付きでベイク → skybox を外して実世界を見せる）を生成。旧 `Finalize/Unfinalize` ロジックをそのまま明示移植。**必須機能。**
  - **SelfApp（VR↔MR）**: `BossLookDevState` が**実行時に** camera clear flags を切替（Skybox⇔passthrough 透過）。
- → STYLY のエディタ時 AR化 と SelfApp のランタイム State を**同じ `skyboxVisible` から両方吐ける**。

### (c) STYLY の color → **焼き込み層は残す / PPv2 LDR 層は STYLY では出さない**
- `ColorSection.bakeIntoLighting`（旧 `LookBakeOps`）は **継承**。STYLY で color を効かせる数少ない確実な手段（環境光/skybox tint への焼き込み）。
- 旧の「STYLY モバイル LDR PPv2 グレード層」は、新方針（§3.5: STYLY の color は STYLY 側方式に委ねる）に従い **STYLY エミッタでは PPv2 を出さない**。
- SelfApp（Built-in / URP）は color アダプタ（PPv2 / Volume）をフル使用。

### (d) AR グラウンドシャドウ → **AR コンテキストのモジュールとして継承**
- `ILookModule` の1つ（context=AR で有効）。STYLY AR / SelfApp AR で接地感に有効なので残す。
- MR(Pico) は occlusion/spatial mesh（v1.x）が本筋のため、グラウンドシャドウは **MR では非提供**。
- URP シェーダ自動生成は §7 のシェーダ移植注意に沿って URP-HLSL を再確認のうえ移植。

### (e) UI → **レイヤー/Target/Context/State 軸へ再設計（カラフル・§11）**
- 旧の8ステップ直列ウィザードは**踏襲しない**。
- 軸: 上部に Target(SelfApp/STYLY) ・ Context(VR/AR/MR) ・ Pipeline(自動検出) ・ State(VR/MR) のバー。本体はレイヤー別カード（lighting / color / atmosphere / overrides）＋「良い既定値（クイックスタート）」「ベイク」「validator」「lookbook」。
- §11 準拠: レイヤー・コンテキスト・パイプライン・判定（継承緑/落とす赤/保留黄）・プリセット状態を**色分け**。`BossLookDevPalette` に集約。
- 実装はフェーズ3（Editor UI）。

---

## 5. インターフェース（C / D / モジュール / Runtime seam）

- **(C)** `IColorAdapter` / `IMaterialAdapter`：`Pipeline` プロパティ＋ Setup/Apply/Remove。`Editor` の `AdapterRegistry` が `GraphicsSettings.currentRenderPipeline` で選択（明示 Target 指定＋自動判定）。
- **(B)** `ILookModule`：`Id` / `Layer`（lighting/color/atmosphere/overrides）/ Apply。登録制。
- **(D)** `ILookResolver`：どのプリセット/状態を使うか決める差し替え可能リゾルバ。v1=`ManualLookResolver` ＋ `VrMrLookResolver`（passthrough 状態観測）。location ベースは後付け同 seam。
- **(R seam)** `IContextStateProvider`（現在の State＋変更イベント）/ `IPassthroughStateProvider`（ベンダー: Pico/Meta、SDK ガード。観測であって制御ではない）。

---

## 6. フェーズ2 完了条件（§8-2）

- [x] (A) データモデル骨格（context/pipeline/target/optional セクション/State ペア）
- [x] (C)/(D) インターフェース定義
- [x] 分割 asmdef ＋ Version Defines スキャフォールド
- [x] 空アダプタ（BuiltIn/URP）・空ベンダー（Pico/Meta）
- [x] **検証済み（2026-06-16）**: ローカルパッケージ参照（`file:` 絶対パス）で両プロジェクトに導入しコンパイル確認。
  - **test-URP**（Unity 6000.0.70f1 / URP 17.0.4）: 全7アセンブリ（Core/Runtime/BuiltIn/URP/Pico/Meta/Editor）ビルド成功・CS エラー 0。PPv2 不在のため BuiltIn は空アセンブリで通過（§7 実証）。
  - **test-builtin**（Unity 2022.3.24f1 / パイプラインパッケージ無し）: 全7アセンブリ ビルド成功・CS エラー 0。URP/PPv2 双方不在でも URP/BuiltIn アダプタが空で通過（§7 実証）。
  - テストプロジェクト: `/Users/styly_mac01/Documents/__UnityToolTestProjects/test-builtin` ・ `.../test-URP`。

---

## 7. エミッタ（Target 別・§5.1）— フェーズ5で実装

- `SelfApp エミッタ`：各 State に `BossLookDevState` を載せ、`OnEnable` で適用＋ transition ブレンド。エンジニアは `SetActive` のみ。
- `STYLY エミッタ`：STYLY 対応コンポーネントだけの宣言的リグ（PPv1 post / reflection / light / 必要なら skybox ドーム）。瞬時切替。
- 両者は同じ `ContextStateSet` を Target に応じて吐き分ける。**ハンドオフ手順書を日本語で自動生成**。
