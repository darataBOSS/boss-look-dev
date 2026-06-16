// URP material uplift (editor authoring). Guarded so Built-in-only projects
// compile. PBR property names are URP-Lit specific (spec §3.3, §7).
#if BOSS_LOOK_DEV_HAS_URP && BOSS_LOOK_DEV_HAS_SRPCORE
using System.Collections.Generic;
using Boss.LookDev.Editor.Ops;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Adapters
{
    /// <summary>(C) URP-Lit material uplift (spec §4.1 "マテリアル底上げ"): nudges
    /// PBR values toward physically-sane, mobile-friendly ranges. Conservative and
    /// reversible-in-spirit — never fabricates textures.</summary>
    public sealed class UrpMaterialAuthoringAdapter : IMaterialAdapter
    {
        public LookPipeline Pipeline => LookPipeline.URP;

        public int NormalizePbr(ILookContextScope scope)
        {
            int touched = 0;
            var seen = new HashSet<Material>();
            foreach (var mr in SceneBindingOps.FindAll<MeshRenderer>())
            {
                foreach (var m in mr.sharedMaterials)
                {
                    if (m == null || !seen.Add(m)) continue;
                    var path = AssetDatabase.GetAssetPath(m);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets")) continue;
                    if (m.shader == null || !m.shader.name.Contains("Universal Render Pipeline")) continue;

                    bool changed = false;
                    Undo.RecordObject(m, "Uplift Material");

                    // Pure-white albedo amplifies GI into blow-out; real white ≈ 0.8.
                    if (m.HasProperty("_BaseColor"))
                    {
                        var c = m.GetColor("_BaseColor");
                        if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f)
                        {
                            m.SetColor("_BaseColor", new Color(0.85f, 0.85f, 0.85f, c.a));
                            changed = true;
                        }
                    }

                    // Mirror-smooth surfaces look wrong without real-time reflections.
                    if (m.HasProperty("_Smoothness"))
                    {
                        float s = m.GetFloat("_Smoothness");
                        if (s > 0.98f) { m.SetFloat("_Smoothness", 0.9f); changed = true; }
                    }

                    // Turn on the detail keyword if a detail normal is assigned but inactive.
                    if (m.HasProperty("_DetailNormalMap") && m.GetTexture("_DetailNormalMap") != null
                        && !m.IsKeywordEnabled("_DETAIL_MULX2"))
                    {
                        m.EnableKeyword("_DETAIL_MULX2");
                        changed = true;
                    }

                    if (!m.enableInstancing) { m.enableInstancing = true; changed = true; }

                    if (changed) { EditorUtility.SetDirty(m); touched++; }
                }
            }
            if (touched > 0) AssetDatabase.SaveAssets();
            return touched;
        }
    }
}
#endif
