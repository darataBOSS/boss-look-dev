using System.Collections.Generic;
using Boss.LookDev.Editor.Ops;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Adapters
{
    /// <summary>(C) Built-in Standard-shader material uplift (spec §4.1). Same
    /// conservative, mobile-friendly nudges as the URP adapter but with Standard
    /// shader property names. No version define needed — the Standard shader is
    /// always present in Built-in RP.</summary>
    public sealed class BuiltInMaterialAuthoringAdapter : IMaterialAdapter
    {
        public LookPipeline Pipeline => LookPipeline.BuiltIn;

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
                    if (m.shader == null || !m.shader.name.Contains("Standard")) continue;

                    bool changed = false;
                    Undo.RecordObject(m, "Uplift Material");

                    if (m.HasProperty("_Color"))
                    {
                        var c = m.GetColor("_Color");
                        if (c.r > 0.95f && c.g > 0.95f && c.b > 0.95f)
                        {
                            m.SetColor("_Color", new Color(0.85f, 0.85f, 0.85f, c.a));
                            changed = true;
                        }
                    }

                    if (m.HasProperty("_Glossiness"))
                    {
                        float s = m.GetFloat("_Glossiness");
                        if (s > 0.98f) { m.SetFloat("_Glossiness", 0.9f); changed = true; }
                    }

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
