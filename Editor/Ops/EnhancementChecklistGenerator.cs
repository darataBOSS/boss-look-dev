using System.IO;
using System.Text;
using UnityEditor;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (Editor) Generates a Japanese "look enhancement checklist" — the things the
    /// tool can't do but that elevate the look (shaders / VFX / textures), so the
    /// visual author can hand a concrete list to themselves / the engineer in the
    /// content project. Tuned for underwater since that's the driving content, but
    /// useful generally. Complements the VR↔MR handoff doc.
    /// </summary>
    public static class EnhancementChecklistGenerator
    {
        public static string Generate(LookDefinition look)
        {
            string folder = SceneBindingOps.AssetFolder(look);
            string path = $"{folder}/{look.lookName}_強化チェックリスト.md";
            File.WriteAllText(path, Build(look));
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static string Build(LookDefinition look)
        {
            bool hasCaustics = look.lighting.rig.causticsCookie != null;
            var sb = new StringBuilder();

            sb.AppendLine($"# {look.lookName} — ルック強化チェックリスト");
            sb.AppendLine();
            sb.AppendLine("boss-look-dev（ライティング/ベイク/カラー/フォグ/背景/State）で作れる土台の **上に**、");
            sb.AppendLine("プロジェクト側で用意するとさらに質感が上がる項目です（多くはシェーダ/VFX/テクスチャ）。");
            sb.AppendLine("モバイル XR（Pico/Quest）前提で、軽いものを優先しています。");
            sb.AppendLine();

            sb.AppendLine("## ツールで済んでいること（土台）");
            sb.AppendLine("- HDRI/環境光・全ベイク（lightmap/プローブ/リフレクション）・ライトリグ");
            sb.AppendLine("- カラーグレード（Bloom/Vignette/Tonemapping/LUT 等）");
            sb.AppendLine("- フォグ（空気感・奥行き）");
            sb.AppendLine("- 背景・環境光（単色/グラデ/スカイボックス）— Look に保存済み");
            sb.AppendLine("- VR↔MR の State 切替（ハンドオフ手順書あり）");
            sb.AppendLine();

            sb.AppendLine("## プロジェクト側で用意するとより良くなるもの");
            sb.AppendLine();
            sb.AppendLine("### 1. コースティクス（水中の光の揺らぎ）★最重要・コスパ最強");
            if (hasCaustics)
                sb.AppendLine("- cookie は **割り当て済み**（リグの主ライト）。あとは **UV スクロールでアニメ**させる（シェーダ or 小スクリプトで cookie/サンプルをスクロール）。");
            else
                sb.AppendLine("- caustics のループテクスチャを用意し、リグの主ライトに cookie として割り当て（ツールの「コースティクス」スロット）。さらに UV スクロールでアニメ。");
            sb.AppendLine("- 床・オブジェクトに揺らぐ光網が出る。モバイルでも軽い。**入れると一気に水中になる**。");
            sb.AppendLine();
            sb.AppendLine("### 2. ゴッドレイ / 光芒");
            sb.AppendLine("- 本格 volumetric はモバイルで重い → **半透明の光シャフトメッシュ/ビルボードを加算でスクロール**、または light shaft パーティクル。");
            sb.AppendLine("- 上（水面）からの差し込みを数本。やり過ぎ注意。");
            sb.AppendLine();
            sb.AppendLine("### 3. 浮遊パーティクル（マリンスノー / 塵 / 泡）");
            sb.AppendLine("- 細かい粒子をゆっくりドリフト → スケール感・没入感・生命感。軽い。");
            sb.AppendLine("- 泡は上昇、マリンスノーは下降など方向で差を付ける。");
            sb.AppendLine();
            sb.AppendLine("### 4. PBR マテリアル / 濡れ表現");
            sb.AppendLine("- albedo/normal/roughness を入れる（グレー単色は避ける）。濡れ面は **smoothness 高め**＋detail normal。");
            sb.AppendLine("- 岩・砂・金属で質感差を付けると説得力が大きく上がる。ツールの「マテリアル底上げ」で正規化も。");
            sb.AppendLine();
            sb.AppendLine("### 5. height / depth fog（下ほど濃い）");
            sb.AppendLine("- RenderSettings の距離フォグに加え、**高さ方向のフォグ**（シェーダ/カスタム）でレイヤー感・深さ。");
            sb.AppendLine();
            sb.AppendLine("### 6. リフレクション / 環境の映り込み");
            sb.AppendLine("- 濡れ・金属に青い環境が映るよう reflection probe を適切に配置・ベイク（ツールで生成可）。");
            sb.AppendLine();
            sb.AppendLine("### 7. 微弱な水中ゆらぎ（任意）");
            sb.AppendLine("- 画面の屈折/ゆらぎは雰囲気が出るが、**VR は強いと酔う → ごく弱く**。フルスクリーン歪みは慎重に。");
            sb.AppendLine();
            sb.AppendLine("### 8. 生命感（コンテンツ）");
            sb.AppendLine("- 魚群・海藻の揺れ・泡・遠景のシルエットなど。");
            sb.AppendLine();

            sb.AppendLine("## モバイル XR で避けるもの");
            sb.AppendLine("- volumetric fog / SSAO / Depth of Field / Motion Blur / 重い screen-space。");
            sb.AppendLine("- 軽量3点セット：**コースティクス cookie ＋ 加算ライトシャフト ＋ パーティクル**。");
            sb.AppendLine();
            sb.AppendLine("## 最終チェック");
            sb.AppendLine("- ツールの「事故チェック（検証）」で未ベイク/白アルベド/UV2/色空間 等を確認。");
            sb.AppendLine("- **実機（Pico/Quest）で必ず確認**（モニタはステレオ・色域・スケールで嘘をつく）。");
            return sb.ToString();
        }
    }
}
