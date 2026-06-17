using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B / common core) Pipeline-agnostic lighting + bake authoring (spec §3.3,
    /// §4.0): HDRI skybox / IBL, Progressive Lightmapper settings, static flags,
    /// light probe grid, reflection probe, and the bake itself. Ported from the
    /// legacy EnvironmentOps, driven by LookDefinition.lighting. No adapter needed
    /// — this is the realism foundation shared across Built-in and URP.
    /// </summary>
    public static class LightingBakeOps
    {
        // ---------------- Skybox / IBL ----------------

        public static void SetupSkybox(LookDefinition look)
        {
            var lighting = look.lighting;
            if (lighting.hdri == null)
            {
                Debug.LogWarning("[BOSS Look Dev] HDRI が未設定です (lighting.hdri)。");
                return;
            }

            string folder = SceneBindingOps.AssetFolder(look);
            string matPath = $"{folder}/{look.lookName}_Skybox.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            bool isCubemap = lighting.hdri is Cubemap;
            var shader = isCubemap ? Shader.Find("Skybox/Cubemap") : Shader.Find("Skybox/Panoramic");
            if (shader == null) { Debug.LogError("[BOSS Look Dev] Skybox シェーダが見つかりません。"); return; }

            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            else if (mat.shader != shader) mat.shader = shader;

            if (isCubemap) mat.SetTexture("_Tex", lighting.hdri);
            else mat.SetTexture("_MainTex", lighting.hdri);
            ApplySkyboxParams(mat, lighting);

            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();

            RenderSettings.skybox = mat;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.ambientIntensity = lighting.environmentIntensity;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(lighting.environmentIntensity);
            ApplySkyboxHaze(look);
            DynamicGI.UpdateEnvironment();
        }

        private static void ApplySkyboxParams(Material mat, LightingSection lighting)
        {
            if (mat == null) return;
            if (mat.HasProperty("_Rotation")) mat.SetFloat("_Rotation", lighting.skyboxRotation);
            if (mat.HasProperty("_Exposure")) mat.SetFloat("_Exposure", lighting.skyboxExposure);
        }

        /// <summary>Distance haze for the visible skybox: darkens + tints the HDRI
        /// toward the fog (haze) color so the far backdrop fades into the murk —
        /// without editing the source image. Note: this also dims the IBL (one
        /// skybox), which is usually desirable underwater. skyboxHaze 0 = neutral.</summary>
        public static void ApplySkyboxHaze(LookDefinition look)
        {
            var mat = RenderSettings.skybox;
            if (mat == null) return;
            float h = Mathf.Clamp01(look.lighting.skyboxHaze);
            if (mat.HasProperty("_Exposure"))
                mat.SetFloat("_Exposure", look.lighting.skyboxExposure * Mathf.Lerp(1f, 0.3f, h));
            if (mat.HasProperty("_Tint"))
            {
                var haze = look.atmosphere.fogColor;
                var hazeTint = new Color(Mathf.Clamp01(haze.r) * 0.6f, Mathf.Clamp01(haze.g) * 0.6f, Mathf.Clamp01(haze.b) * 0.6f, 1f);
                mat.SetColor("_Tint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 1f), hazeTint, h));
            }
            EditorUtility.SetDirty(mat);
        }

        /// <summary>Live preview: drive rotation / exposure / intensity (and haze)
        /// without regenerating the material, for slider dragging (spec §4.1).</summary>
        public static void ApplyEnvironmentLive(LookDefinition look)
        {
            var mat = RenderSettings.skybox;
            if (mat == null) return;
            ApplySkyboxParams(mat, look.lighting);
            ApplySkyboxHaze(look);
            RenderSettings.ambientIntensity = look.lighting.environmentIntensity;
            RenderSettings.reflectionIntensity = Mathf.Clamp01(look.lighting.environmentIntensity);
            EditorUtility.SetDirty(mat);
            DynamicGI.UpdateEnvironment();
        }

        // ---------------- Camera background (VR show / AR transparent) ----------------

        /// <summary>
        /// Sets how the baked skybox is *displayed* (the bake itself always uses it
        /// as the IBL source). VR = show the HDRI skybox as the CG background;
        /// AR = transparent so the real world / passthrough shows through. Baked
        /// lighting (ambient/reflection) is kept either way — only the camera's
        /// background changes. For MR this is driven per-state by BossLookDevState;
        /// this op is for single-context preview / static AR.
        /// </summary>
        public static void SetCameraBackground(bool skyboxVisible)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[BOSS Look Dev] Main Camera が見つかりません。");
                return;
            }
            Undo.RecordObject(cam, "Set Camera Background");
            if (skyboxVisible)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
            else
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f); // transparent for AR compositing
            }
            EditorUtility.SetDirty(cam);
        }

        public static bool IsSkyboxShown()
        {
            var cam = Camera.main;
            return cam != null && cam.clearFlags == CameraClearFlags.Skybox;
        }

        // ---------------- Lighting settings ----------------

        public static LightingSettings CreateOrUpdateLightingSettings(LookDefinition look)
        {
            var lighting = look.lighting;
            string folder = SceneBindingOps.AssetFolder(look);
            string path = $"{folder}/{look.lookName}_LightingSettings.lighting";
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (settings == null)
            {
                settings = new LightingSettings { name = $"{look.lookName}_LightingSettings" };
                AssetDatabase.CreateAsset(settings, path);
            }

            settings.autoGenerate = false;
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.lightmapResolution = lighting.lightmapResolution;
            settings.lightmapMaxSize = lighting.atlasSize;
            settings.directSampleCount = lighting.directSamples;
            settings.indirectSampleCount = lighting.indirectSamples;
            settings.maxBounces = lighting.bounces;
            settings.lightmapper = lighting.bakeBackend == BakeBackend.ProgressiveCPU
                ? LightingSettings.Lightmapper.ProgressiveCPU
                : LightingSettings.Lightmapper.ProgressiveGPU;
            settings.compressLightmaps = lighting.compressLightmaps;
            settings.ao = lighting.ambientOcclusion;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            Lightmapping.lightingSettings = settings;
#if !UNITY_6000_0_OR_NEWER
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
#endif
            return settings;
        }

        // ---------------- Static flags ----------------

        /// <summary>Marks scene mesh renderers as ContributeGI per the look's static
        /// policy (All, or ExcludeLayers to keep avatars/fish dynamic for probes),
        /// and returns those missing lightmap UV2.</summary>
        public static int ApplyStaticFlags(LookDefinition look, out List<GameObject> missingUv2)
        {
            missingUv2 = new List<GameObject>();
            var l = look.lighting;
            int touched = 0;
            foreach (var mr in SceneBindingOps.FindAll<MeshRenderer>())
            {
                if (mr == null) continue;
                var go = mr.gameObject;
                if (l.staticMode == StaticTargetMode.ExcludeLayers && (l.dynamicLayers.value & (1 << go.layer)) != 0)
                    continue; // keep dynamic (lit by light probes)

                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                flags |= StaticEditorFlags.ContributeGI;
                GameObjectUtility.SetStaticEditorFlags(go, flags);

                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null && (mf.sharedMesh.uv2 == null || mf.sharedMesh.uv2.Length == 0))
                    missingUv2.Add(go);
                touched++;
            }
            return touched;
        }

        // ---------------- Light probes ----------------

        public static LightProbeGroup CreateOrUpdateProbeGroup(LookDefinition look) =>
            CreateProbeGroup(look, SceneBindingOps.ProbeGroupName(look),
                look.lighting.probeArea, look.lighting.probeSpacing, look.lighting.verticalLayers);

        /// <summary>Interior (room) light probes — for moving avatars inside the room.</summary>
        public static LightProbeGroup CreateOrUpdateInteriorProbeGroup(LookDefinition look) =>
            CreateProbeGroup(look, SceneBindingOps.InteriorProbeGroupName(look),
                look.lighting.interiorProbeArea, look.lighting.interiorProbeSpacing, look.lighting.interiorVerticalLayers);

        private static LightProbeGroup CreateProbeGroup(LookDefinition look, string name, Bounds area, float spacing, int layers)
        {
            var group = SceneBindingOps.FindComponentInScene<LightProbeGroup>(name);
            if (group == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create Light Probe Group");
                group = go.AddComponent<LightProbeGroup>();
            }
            group.probePositions = GenerateProbeGrid(area, spacing, layers, group.transform.position).ToArray();
            SceneBindingOps.Parent(group.gameObject, look);
            EditorUtility.SetDirty(group);
            return group;
        }

        private static List<Vector3> GenerateProbeGrid(Bounds area, float spacing, int layers, Vector3 groupOrigin)
        {
            var positions = new List<Vector3>();
            spacing = Mathf.Max(0.1f, spacing);
            layers = Mathf.Max(1, layers);
            Vector3 origin = area.min;
            Vector3 size = area.size;
            int xCount = Mathf.Max(2, Mathf.CeilToInt(size.x / spacing) + 1);
            int zCount = Mathf.Max(2, Mathf.CeilToInt(size.z / spacing) + 1);
            float yStep = layers <= 1 ? 0f : size.y / (layers - 1);

            for (int yi = 0; yi < layers; yi++)
            {
                float y = origin.y + yi * yStep;
                for (int xi = 0; xi < xCount; xi++)
                {
                    float x = origin.x + Mathf.Min(xi * spacing, size.x);
                    for (int zi = 0; zi < zCount; zi++)
                    {
                        float z = origin.z + Mathf.Min(zi * spacing, size.z);
                        positions.Add(new Vector3(x, y, z) - groupOrigin);
                    }
                }
            }
            return positions;
        }

        // ---------------- Reflection probe ----------------

        public static ReflectionProbe CreateOrUpdateReflectionProbe(LookDefinition look) =>
            CreateReflectionProbe(look, SceneBindingOps.ReflectionProbeName(look), look.lighting.probeArea);

        /// <summary>Interior (room) reflection probe — room surfaces reflect the room,
        /// not the sea.</summary>
        public static ReflectionProbe CreateOrUpdateInteriorReflectionProbe(LookDefinition look) =>
            CreateReflectionProbe(look, SceneBindingOps.InteriorReflectionProbeName(look), look.lighting.interiorProbeArea);

        private static ReflectionProbe CreateReflectionProbe(LookDefinition look, string name, Bounds area)
        {
            var lighting = look.lighting;
            var probe = SceneBindingOps.FindComponentInScene<ReflectionProbe>(name);
            if (probe == null)
            {
                var go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create Reflection Probe");
                probe = go.AddComponent<ReflectionProbe>();
            }

            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(lighting.reflectionResolution), 16, 2048);
            probe.hdr = true;
            probe.center = area.center - probe.transform.position;
            probe.size = area.size + Vector3.one * lighting.reflectionPadding * 2f;

            if (lighting.reflectionMode == ReflectionBakeMode.NeutralReplace)
            {
                probe.clearFlags = ReflectionProbeClearFlags.SolidColor;
                probe.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            }
            else
            {
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            }

            SceneBindingOps.Parent(probe.gameObject, look);
            EditorUtility.SetDirty(probe);
            return probe;
        }

        // ---------------- Bake ----------------

        public static bool CanBake(LookDefinition look, out string reason)
        {
            if (RenderSettings.skybox == null)
            {
                reason = "スカイボックスが未設定です。先に HDRI を設定してください。";
                return false;
            }
            reason = null;
            return true;
        }

        public static void StartBake(LookDefinition look)
        {
            if (!CanBake(look, out string reason))
            {
                EditorUtility.DisplayDialog("BOSS Look Dev", reason, "OK");
                return;
            }
            CreateOrUpdateLightingSettings(look);
#if !UNITY_6000_0_OR_NEWER
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
#endif
            Lightmapping.BakeAsync();
        }

        public static void CancelBake()
        {
            if (Lightmapping.isRunning) Lightmapping.Cancel();
        }

        public static bool HasBakedData() => Lightmapping.lightingDataAsset != null;

        /// <summary>Clears the scene's baked lightmaps so the realtime (unbaked)
        /// state is visible again — the right move before re-tweaking lighting,
        /// otherwise stale baked results hide your changes. Re-bake when done.</summary>
        public static void ClearBake()
        {
            if (Lightmapping.isRunning) Lightmapping.Cancel();
            Lightmapping.Clear();
        }
    }
}
