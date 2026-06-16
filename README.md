# BOSS Look Dev

Unity Editor / Runtime パッケージ。**ライティングを土台に、リアルな質感とルックを作り込む**ための look-dev ツール。AR / VR / MR・Built-in RP / URP の双方に対応する。

- 主ターゲット: **Pico 4 Ultra**（VR/MR・URP・スタンドアロン）と **STYLY モバイルAR**（Built-in RP）。いずれもモバイル級・ベイク前提・オーサリング中心。
- メニュー: `BOSS > Look Dev`
- 最低対応 Unity: **2022.3.24f1**

> 旧ツール [`boss-look-preset`](https://github.com/darataBOSS/boss-look-preset) の機能とドメインロジックを **参照のみ**で引き継ぎ、構造は再設計したもの。旧ツールへは一切書き込まない。

## インストール

Package Manager の `+` → **Add package from git URL**:

```
https://github.com/darataBOSS/boss-look-dev.git
```

URP / Post Processing Stack v2 は**ハード依存にしていません**。ホストプロジェクトのパイプラインに合わせて、対応アダプタが自動で立ち上がります（未インストールでもコンパイルは通ります）。

## アーキテクチャ（4層 ＋ Runtime）

| 層 | asmdef | 役割 |
|---|---|---|
| (A) データモデル | `Boss.LookDev.Core` | `LookDefinition`（context/pipeline/target・lighting/color/atmosphere/overrides・VR↔MR 2状態）と全インターフェース。パイプライン非依存。 |
| (B) 合成・適用 | `Boss.LookDev.Editor` ほか | レイヤーをシーンへ適用。良い既定値・ベイク・validator・lookbook。 |
| (C) アダプタ | `Boss.LookDev.BuiltIn` / `.URP` | color（PPv2 / URP Volume）・material。versionDefine ガード。 |
| (D) リゾルバ | `Boss.LookDev.Runtime` | 手動選択 ＋ VR↔MR リゾルバ。location ベースは後付け同 seam。 |
| (R) Runtime | `Boss.LookDev.Runtime` / `.Pico` / `.Meta` | `BossLookDevState`（SelfApp の状態トグル）・passthrough 観測 seam。 |

詳細は [docs/architecture.md](docs/architecture.md)、旧ツールからの継承判定は [docs/legacy-inventory.md](docs/legacy-inventory.md)。

## 主な機能（v0.6.0 / v1 スコープ達成）

`BOSS > Look Dev` ウィンドウから:

- **クイックスタート**: HDRI を入れて1ボタンで土台一式（スカイボックス/IBL → ライト → プローブ → カラー）
- **ライティング（土台）**: HDRI 環境光・ベイク（Progressive Lightmapper・モバイル/高品質プリセット）・ライトプローブ・リフレクションプローブ・ライトリグ（3点 / 太陽 / 天井グリッド）・HDRI 太陽向き推定・マテリアル底上げ
- **背景（カメラ）**: VR=スカイボックス表示 / AR=透過 を切替（ベイクは保持）
- **カラー**: グレード・Bloom・Vignette ＋ 詳細設定（threshold/scatter/tint・hue・Tonemapping・LUT 等）。Built-in=PPv2 / URP=Volume を自動切替
- **フォグ（空気感）** / **AR グラウンドシャドウ**（AR 時）
- **VR↔MR 切り替え（State）**: 共有ベイクのまま 2 状態をトグル。SelfApp=`BossLookDevState`＋スムーズ transition、STYLY=宣言的リグ。**日本語ハンドオフ手順書を自動生成**
- **ルックの比較・ブレンド**（2プリセット間ブレンド / before-after スナップショット）
- **事故チェック（検証）**: 実機で事故りやすい設定を警告

**ターゲット**: `SelfApp`（Pico/Quest 等の自前アプリ）/ `STYLY`（PlayMaker・Built-in 固定）。STYLY ではカラーをライティング焼き込み＋STYLY 側方式に委譲。

非ゴール（v1.x 以降）: MR オクルージョン、Meta ベンダー実装、STYLY スムーズ transition、location-based preset、in-VR オーサリング、クロスパイプライン移植、ランタイム light 推定。

## ライセンス

MIT
