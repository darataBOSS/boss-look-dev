using Boss.LookDev.Runtime;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor
{
    /// <summary>
    /// Makes the VR↔MR state object self-explanatory in the Inspector (no separate
    /// handoff doc needed): a clear HelpBox tells whoever opens it exactly how to
    /// switch. Engineer-facing details also live in docs/vr-mr-switching.md.
    /// </summary>
    [CustomEditor(typeof(BossLookDevState))]
    public class BossLookDevStateEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "VR↔MR 切り替え — この GameObject の アクティブ / 非アクティブ を切り替えるだけ。\n\n" +
                "・有効になった瞬間、この State のルック（背景・フォグ・環境光・反射）が適用されます" +
                "（SelfApp はスムーズにブレンド）。\n" +
                "・VR_State / MR_State は常に片方だけ有効にしてください。\n" +
                "・切替方法: C# の SetActive(true/false) / Timeline の Activation Track / " +
                "PlayMaker の Activate Game Object（STYLY）。\n" +
                "・passthrough（没入モード）自体の ON/OFF は Pico/Quest SDK 側で同じタイミングに合わせます。",
                MessageType.Info);
            EditorGUILayout.Space(4);
            DrawDefaultInspector();
        }
    }
}
