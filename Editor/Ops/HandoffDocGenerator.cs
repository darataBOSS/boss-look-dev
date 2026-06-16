using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (Editor) Auto-generates the author→engineer handoff doc in Japanese
    /// (spec §5.1): "こうすれば VR↔MR が切り替わる". Branches by Target
    /// (SelfApp = SetActive / STYLY = PlayMaker Activate Game Object) and names the
    /// exact objects to toggle plus the globals that move with them.
    /// </summary>
    public static class HandoffDocGenerator
    {
        public static string Generate(LookDefinition look)
        {
            string folder = SceneBindingOps.AssetFolder(look);
            string path = $"{folder}/{look.lookName}_Handoff.md";
            File.WriteAllText(path, Build(look));
            AssetDatabase.ImportAsset(path);
            return path;
        }

        private static string Build(LookDefinition look)
        {
            var sb = new StringBuilder();
            string sw = StateRigEmitter.SwitchName(look);
            string a = look.states.stateA.stateName;
            string b = look.states.stateB.stateName;
            bool styly = look.target == LookTarget.STYLY;

            sb.AppendLine($"# {look.lookName} — VR↔MR 切り替え手順 (ハンドオフ)");
            sb.AppendLine();
            sb.AppendLine($"- 出力先 (Target): **{look.target}**");
            sb.AppendLine($"- コンテキスト: {look.targetContext} / パイプライン: {look.targetPipeline}");
            sb.AppendLine($"- 切り替え対象: `{sw}` 配下の `{a}` / `{b}`（常に**片方だけアクティブ**）");
            sb.AppendLine();
            sb.AppendLine("## 仕組み");
            sb.AppendLine($"ベイク（GI/lightmap）は2状態で**共有**。切り替えで変わるのは実行時パラメータのみ:");
            sb.AppendLine("- カメラ clear flags（VR=Skybox 表示 / MR=passthrough 透過）");
            sb.AppendLine("- フォグ（VR=あり / MR=オフ）");
            sb.AppendLine("- 環境光（ambient）・リフレクションの強さ");
            sb.AppendLine();

            if (!styly)
            {
                sb.AppendLine("## SelfApp（自前アプリ）での切り替え");
                sb.AppendLine($"各 State には `BossLookDevState` が載っており、`OnEnable` でそのルックを適用します"
                    + (look.states.transition.smooth ? $"（{look.states.transition.durationSeconds:0.##}s でスムーズにブレンド）。" : "（瞬時）。"));
                sb.AppendLine();
                sb.AppendLine("MR → VR に切り替える例（C#）:");
                sb.AppendLine("```csharp");
                sb.AppendLine($"{b}.SetActive(false);");
                sb.AppendLine($"{a}.SetActive(true);");
                sb.AppendLine("```");
                sb.AppendLine("VR → MR は逆にします。Timeline の Activation Track でも同じことができます。");
                sb.AppendLine();
                sb.AppendLine("> 没入モード自体（passthrough の on/off）は Pico/Quest SDK 側で制御してください。");
                sb.AppendLine("> このトグルは**コンテンツのルック**を切り替えるもので、passthrough と同じタイミングで合わせます。");
            }
            else
            {
                sb.AppendLine("## STYLY（PlayMaker）での切り替え");
                sb.AppendLine($"PlayMaker の **Activate Game Object** アクションで `{a}` / `{b}` をトグルします（常に片方だけ ON）。");
                sb.AppendLine($"- VR にする: `{a}` を Activate(true)、`{b}` を Activate(false)");
                sb.AppendLine($"- MR にする: `{b}` を Activate(true)、`{a}` を Activate(false)");
                sb.AppendLine();
                sb.AppendLine("グローバル設定（フォグ・スカイボックス表示など）は PlayMaker のアクションで切り替えてください:");
                sb.AppendLine("- フォグ: Set Fog / RenderSettings 系アクション");
                sb.AppendLine("- スカイボックス: カメラの Clear Flags（VR=Skybox / MR=Solid Color 透過）");
                sb.AppendLine();
                sb.AppendLine("> STYLY ではカスタム C# が動かないため `BossLookDevState` は生成していません（宣言的リグ）。");
                sb.AppendLine("> スムーズ化が必要なら Timeline を作って PlayMaker から再生してください（v1 は瞬時切替）。");
                sb.AppendLine("> STYLY が実行時に VR↔MR 表示切り替えを行えるかはプラットフォーム要確認。");
            }

            sb.AppendLine();
            sb.AppendLine("## 各 State の差分（参考）");
            AppendStateTable(sb, "VR 側", look.states.stateA);
            AppendStateTable(sb, "MR 側", look.states.stateB);
            return sb.ToString();
        }

        private static void AppendStateTable(StringBuilder sb, string label, ContextStatePair pair)
        {
            var d = pair.delta;
            sb.AppendLine($"- **{pair.stateName}**（{label} / {pair.context}）: "
                + $"skybox={(d.skyboxVisible ? "表示" : "透過")}, fog={(d.fogEnabled ? "あり" : "オフ")}, "
                + $"ambient={d.ambientIntensity:0.##}, reflection={d.reflectionIntensity:0.##}");
        }
    }
}
