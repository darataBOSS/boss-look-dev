# VR ↔ MR 切り替え（エンジニア向け）

boss-look-dev で作ったコンテンツで、体験中に **VR（フルCG背景）↔ MR（passthrough 透過）** を切り替える方法。

## 仕組み（ひとことで）

ルック担当が「**State リグ**」を生成しています。ヒエラルキーはこの形：

```
<Look名> Switch
 ├─ VR_State   ← 有効なとき VR のルック（スカイボックス表示など）
 └─ MR_State   ← 有効なとき MR のルック（背景透過・フォグオフなど）
```

- **切り替え = この2つの GameObject のアクティブを入れ替えるだけ**（常に片方だけ有効）。
- ベイク（GI / lightmap）は2状態で**共有**。切り替わるのは実行時パラメータのみ（カメラの背景＝skybox/透過、フォグ、環境光、反射）。

## 切り替え方

### SelfApp（自前アプリ：Pico / Quest など）
各 State に `BossLookDevState` が付いており、有効化された瞬間にそのルックを適用＋スムーズにブレンドします。エンジニアは**アクティブを切り替えるだけ**：

```csharp
// MR にする
vrState.SetActive(false);
mrState.SetActive(true);

// VR にする
mrState.SetActive(false);
vrState.SetActive(true);
```

- Timeline の **Activation Track** でも同じことができます。
- `BossLookDevState` の Inspector にも操作説明が出ます。

### STYLY（PlayMaker）
カスタム C# は動かないので、State は宣言的リグとして生成されています。**PlayMaker の Activate Game Object** で `VR_State` / `MR_State` をトグルしてください（常に片方だけ ON）。フォグ等のグローバル設定が要る場合は PlayMaker のアクションで。

## 重要：passthrough（没入モード）自体の切り替え

このトグルが切り替えるのは **コンテンツのルック**です。**passthrough の ON/OFF（VR↔MR の表示モードそのもの）は Pico / Quest の SDK 側で制御**してください。ルックのトグルと**同じタイミング**で呼べば、見た目と没入モードが揃います。

- Pico: PICO SDK の passthrough API
- Quest: Meta XR SDK の passthrough API

## まとめ

| | やること |
|---|---|
| SelfApp | `vrState/mrState.SetActive()` ＋ SDK の passthrough 切替を同時に |
| STYLY | PlayMaker の Activate Game Object でトグル（passthrough 可否は要確認） |
