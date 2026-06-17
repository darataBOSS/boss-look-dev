using System;
using System.Collections.Generic;
using Boss.LookDev.Editor.Ops;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor
{
    /// <summary>
    /// Phase-3 authoring window (spec §4, §11): lighting-first, colorful, card-based.
    /// Drives the common-core lighting/bake ops and the active pipeline's color
    /// adapter. "Quick start → looks real → bake" in one place, plus blend,
    /// before/after snapshot, and the validator.
    /// </summary>
    public class BossLookDevWindow : EditorWindow
    {
        [SerializeField] private LookDefinition look;
        private Vector2 _scroll;

        // Lookbook / blend state
        private LookDefinition _blendA, _blendB;
        private float _blendT = 0.5f;
        private string _snapshot;
        private bool _showAdvancedColor;

        // Validator
        private List<LookIssue> _issues;

        [MenuItem("BOSS/Look Dev")]
        public static void Open()
        {
            var w = GetWindow<BossLookDevWindow>("BOSS Look Dev");
            w.minSize = new Vector2(460f, 560f);
            w.Show();
        }

        private void OnInspectorUpdate()
        {
            if (Lightmapping.isRunning) Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            if (look == null) return;

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawPresets();
            DrawQuickStart();
            DrawLighting();
            DrawBackground();
            DrawColor();
            DrawAtmosphere();
            if (look.targetContext == LookContext.AR) DrawGroundShadow();
            DrawStates();
            DrawLookbook();
            DrawValidate();
            EditorGUILayout.EndScrollView();
        }

        // ---------------- Header ----------------

        private void DrawHeader()
        {
            EditorGUILayout.LabelField("BOSS Look Dev", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("v0.9.2 — lighting-first / AR・VR・MR / Built-in・URP", EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            look = (LookDefinition)EditorGUILayout.ObjectField("Look", look, typeof(LookDefinition), false);
            if (EditorGUI.EndChangeCheck()) _issues = null;

            if (look == null)
            {
                EditorGUILayout.HelpBox(
                    "Look Definition = ルック設定一式を保存するアセット (旧ツールのプリセット相当)。\n" +
                    "下のボタンで新規作成するか、既存をスロットにドラッグしてください。",
                    MessageType.Info);
                if (Button("＋ 新規 Look を作成", BossLookDevPalette.Generate, 28f))
                    CreateNewLook();
                return;
            }

            var active = RenderPipelineDetector.Active;

            // Pipeline is auto-detected (read-only).
            ColorChip("Pipeline (自動)", RenderPipelineDetector.DisplayName, BossLookDevPalette.ForPipeline(active));

            // Target / Context are editable here (color-coded dropdowns).
            EditorGUI.BeginChangeCheck();
            look.target = (LookTarget)ColoredEnumPopup("Target (出力先)", look.target,
                look.target == LookTarget.STYLY ? BossLookDevPalette.MR : BossLookDevPalette.VR);
            look.targetContext = (LookContext)ColoredEnumPopup("Context (モダリティ)", look.targetContext,
                BossLookDevPalette.ForContext(look.targetContext));
            if (EditorGUI.EndChangeCheck())
            {
                // STYLY is Built-in only (spec §3.2/§3.3): keep targetPipeline consistent.
                if (look.target == LookTarget.STYLY) look.targetPipeline = LookPipeline.BuiltIn;
                EditorUtility.SetDirty(look);
                _issues = null;
            }

            // Guidance / mismatch warnings.
            if (look.target == LookTarget.STYLY && active == LookPipeline.URP)
                EditorGUILayout.HelpBox(
                    "Target=STYLY は Built-in 専用です。現在は URP プロジェクトのため、STYLY 向け制作は Built-in プロジェクトで行ってください。",
                    MessageType.Warning);
            else if (look.IsPipelineMismatch(active))
                EditorGUILayout.HelpBox(
                    $"対象パイプライン ({look.targetPipeline}) が現在 ({active}) と不一致。プリセットは移植不可 (警告のみ)。",
                    MessageType.Warning);

            if (look.targetContext != LookContext.VR)
                BossHint("AR / MR ではスカイボックス・IBL・フォグは通常無効。実環境が背後に来る前提でニュートラル/スタイライズに作ります。");

            EditorGUILayout.Space(4);
        }

        // ---------------- Presets (starting points) ----------------

        private void DrawPresets()
        {
            using (Card("★ プリセット（出発点を選ぶ）", BossLookDevPalette.Finalize))
            {
                BossHint("ルックの出発点をワンクリック適用（色・フォグ・背景・ライト調を上書き。HDRI / ベイク / 生成物は変更しません）。深海コンテンツ向けの『海中』系もここ。適用後、下の番号順で微調整。");
                foreach (var p in LookPresetLibrary.BuiltIn)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (Button(p.name, BossLookDevPalette.Auto, 24f, 190f))
                        {
                            Undo.RecordObject(look, "Apply Preset");
                            p.apply(look);
                            EditorUtility.SetDirty(look);
                            ApplyLookLive();
                            ShowNotification(new GUIContent($"「{p.name}」を適用"));
                        }
                        BossHint(p.description);
                    }
                }
            }
        }

        /// <summary>Re-applies the look's feel to the scene live (background/ambient,
        /// fog, color, rig) — used after applying a preset.</summary>
        private void ApplyLookLive()
        {
            if (look == null) return;
            if (look.background.enabled) BackgroundOps.Apply(look);
            var a = look.atmosphere;
            RenderSettings.fog = a.enabled; RenderSettings.fogMode = a.fogMode;
            RenderSettings.fogColor = a.fogColor; RenderSettings.fogDensity = a.fogDensity;
            RenderSettings.fogStartDistance = a.fogStartDistance; RenderSettings.fogEndDistance = a.fogEndDistance;
            if (look.target == LookTarget.STYLY) LookBakeOps.ApplyToLighting(look);
            else { var ca = AdapterRegistry.GetActiveColorAdapter(); if (ca != null) ca.Apply(new EditorLookScope(look)); }
            if (GameObject.Find(SceneBindingOps.RigRootName(look)) != null)
                LightRigAuthoring.CreateOrUpdateRig(look, QuickStartOps.ResolveSubject());
            LightingBakeOps.ApplyEnvironmentLive(look);
        }

        // ---------------- Quick start ----------------

        private void DrawQuickStart()
        {
            using (Card("1. ⚡ クイックスタート（おすすめ最初）", BossLookDevPalette.Generate))
            {
                EditorGUILayout.HelpBox("HDRI を指定して1ボタン。スカイボックス〜ライト〜プローブ〜カラーまで一括し、ベイク直前まで持っていきます。", MessageType.None);
                look.lighting.hdri = (Texture)EditorGUILayout.ObjectField("HDRI", look.lighting.hdri, typeof(Texture), false);

                using (new EditorGUI.DisabledScope(look.lighting.hdri == null))
                    if (Button("⚡ クイックスタート実行", BossLookDevPalette.Generate, 28f))
                    {
                        var report = QuickStartOps.Run(look);
                        Debug.Log("[BOSS Look Dev] Quick Start:\n" + report);
                        EditorUtility.DisplayDialog("BOSS Look Dev — クイックスタート", report, "OK");
                    }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("ベイクプリセット", GUILayout.Width(110));
                    if (Button("📱 モバイル", BossLookDevPalette.Auto)) QuickStartOps.ApplyBakePreset(look, false);
                    if (Button("✨ 高品質", BossLookDevPalette.Auto)) QuickStartOps.ApplyBakePreset(look, true);
                }
            }
        }

        // ---------------- Lighting ----------------

        private void DrawLighting()
        {
            using (Card("2. ライティング（土台）", BossLookDevPalette.Lighting))
            {
                BossHint("リアルさの土台。HDRI で環境光 → ベイク → プローブ/リフレクション → ライトリグ。ここを固めてから色を乗せます。");
                var l = look.lighting;

                EditorGUILayout.LabelField("環境光 / HDRI", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                l.environmentIntensity = EditorGUILayout.Slider("環境光強度", l.environmentIntensity, 0f, 8f);
                l.skyboxExposure = EditorGUILayout.Slider("スカイボックス露出", l.skyboxExposure, 0f, 8f);
                l.skyboxRotation = EditorGUILayout.Slider("スカイボックス回転", l.skyboxRotation, 0f, 360f);
                l.skyboxHaze = EditorGUILayout.Slider("遠景の霞み (skybox→フォグ色)", l.skyboxHaze, 0f, 1f);
                if (EditorGUI.EndChangeCheck())
                {
                    LightingBakeOps.ApplyEnvironmentLive(look); // live preview
                    EditorUtility.SetDirty(look);
                }
                using (new EditorGUI.DisabledScope(l.hdri == null))
                    if (Button("スカイボックスを生成 / 更新", BossLookDevPalette.Generate))
                        LightingBakeOps.SetupSkybox(look);
                if (l.skyboxHaze > 0f)
                    BossHint("遠景の霞み: HDRI を暗く＋フォグ色に寄せて遠くを霞ませます（IBLも少し暗くなる＝深海向き）。色は『5. フォグ』の「環境から自動」でHDRIに合わせると自然。");

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("ベイク設定", EditorStyles.boldLabel);
                l.bakeBackend = (BakeBackend)EditorGUILayout.EnumPopup("バックエンド", l.bakeBackend);
                l.lightmapResolution = EditorGUILayout.FloatField("Lightmap 解像度", l.lightmapResolution);
                l.atlasSize = EditorGUILayout.IntPopup("Atlas Size", l.atlasSize,
                    new[] { "512", "1024", "2048", "4096" }, new[] { 512, 1024, 2048, 4096 });
                l.directSamples = EditorGUILayout.IntField("Direct Samples", l.directSamples);
                l.indirectSamples = EditorGUILayout.IntField("Indirect Samples", l.indirectSamples);
                l.bounces = EditorGUILayout.IntSlider("Bounces", l.bounces, 0, 4);
                l.ambientOcclusion = EditorGUILayout.Toggle("Ambient Occlusion (bake)", l.ambientOcclusion);
                if (Button("Lighting Settings を生成 / 更新", BossLookDevPalette.Generate))
                    LightingBakeOps.CreateOrUpdateLightingSettings(look);

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Static (ベイク対象)", EditorStyles.boldLabel);
                l.staticMode = (StaticTargetMode)EditorGUILayout.EnumPopup("Static モード", l.staticMode);
                if (l.staticMode == StaticTargetMode.ExcludeLayers)
                {
                    l.dynamicLayers = LayerMaskField("動的レイヤー (除外: アバター/魚)", l.dynamicLayers);
                    BossHint("選んだレイヤーは Static にせず動的のまま → ライトプローブで光ります（アバター/魚を入れる）。");
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("プローブ / リフレクション（メイン＝室外/海）", EditorStyles.boldLabel);
                l.probeArea.center = EditorGUILayout.Vector3Field("Probe 中心", l.probeArea.center);
                l.probeArea.size = EditorGUILayout.Vector3Field("Probe サイズ", l.probeArea.size);
                if (Button("シーン全体から Probe 範囲を設定", BossLookDevPalette.Auto))
                    if (QuickStartOps.TryComputeSceneBounds(out var b)) { b.Expand(0.5f); l.probeArea = b; EditorUtility.SetDirty(look); }
                l.probeSpacing = EditorGUILayout.FloatField("Probe 間隔 (m)", l.probeSpacing);
                l.verticalLayers = EditorGUILayout.IntSlider("縦レイヤー", l.verticalLayers, 1, 6);
                l.reflectionMode = (ReflectionBakeMode)EditorGUILayout.EnumPopup("Reflection モード", l.reflectionMode);
                l.reflectionResolution = EditorGUILayout.IntPopup("Reflection 解像度", Mathf.ClosestPowerOfTwo(l.reflectionResolution),
                    new[] { "64", "128", "256", "512" }, new[] { 64, 128, 256, 512 });
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Button("ライトプローブ生成", BossLookDevPalette.Generate)) LightingBakeOps.CreateOrUpdateProbeGroup(look);
                    if (Button("リフレクション生成", BossLookDevPalette.Generate)) LightingBakeOps.CreateOrUpdateReflectionProbe(look);
                }

                EditorGUILayout.Space(4);
                l.useInteriorZone = EditorGUILayout.Toggle("室内ゾーンを使う（部屋＋動くアバター）", l.useInteriorZone);
                if (l.useInteriorZone)
                {
                    BossHint("室内用のライトプローブ＋リフレクションを追加。アバターは位置で『室内=暖色 / 室外=青』に自動で切り替わります（ランタイムのコード不要）。");
                    l.interiorProbeArea.center = EditorGUILayout.Vector3Field("室内 中心", l.interiorProbeArea.center);
                    l.interiorProbeArea.size = EditorGUILayout.Vector3Field("室内 サイズ", l.interiorProbeArea.size);
                    if (Button("選択中から室内範囲を設定", BossLookDevPalette.Auto))
                        if (TrySelectionBounds(out var ib)) { l.interiorProbeArea = ib; EditorUtility.SetDirty(look); }
                    l.interiorProbeSpacing = EditorGUILayout.FloatField("室内 Probe 間隔 (m)", l.interiorProbeSpacing);
                    l.interiorVerticalLayers = EditorGUILayout.IntSlider("室内 縦レイヤー", l.interiorVerticalLayers, 1, 6);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (Button("室内ライトプローブ生成", BossLookDevPalette.Generate)) LightingBakeOps.CreateOrUpdateInteriorProbeGroup(look);
                        if (Button("室内リフレクション生成", BossLookDevPalette.Generate)) LightingBakeOps.CreateOrUpdateInteriorReflectionProbe(look);
                    }
                }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("マテリアル底上げ", EditorStyles.boldLabel);
                if (Button("PBR を正規化 (白アルベド抑制 / 鏡面緩和 / instancing)", BossLookDevPalette.Auto))
                {
                    var ma = AdapterRegistry.GetActiveMaterialAdapter();
                    if (ma != null)
                    {
                        int n = ma.NormalizePbr(new EditorLookScope(look));
                        ShowNotification(new GUIContent($"{n} マテリアルを底上げしました"));
                    }
                    else ShowNotification(new GUIContent("マテリアルアダプタ未対応 (Built-in はフェーズ4)"));
                }

                EditorGUILayout.Space(4);
                DrawRig();

                EditorGUILayout.Space(6);
                DrawBake();
            }
        }

        private void DrawRig()
        {
            var rig = look.lighting.rig;
            EditorGUILayout.LabelField("ライトリグ", EditorStyles.boldLabel);
            rig.rigType = (RigType)EditorGUILayout.EnumPopup("リグタイプ", rig.rigType);

            switch (rig.rigType)
            {
                case RigType.ThreePoint:
                    rig.rigLightKind = (RigLightKind)EditorGUILayout.EnumPopup("ライト種別", rig.rigLightKind);
                    rig.keyIntensity = EditorGUILayout.FloatField("Key 強度", rig.keyIntensity);
                    rig.keyFillRatio = EditorGUILayout.Slider("Key:Fill 比", rig.keyFillRatio, 1f, 8f);
                    rig.keyKelvin = EditorGUILayout.FloatField("Key 色温度 (K)", rig.keyKelvin);
                    rig.fillKelvin = EditorGUILayout.FloatField("Fill 色温度 (K)", rig.fillKelvin);
                    rig.backKelvin = EditorGUILayout.FloatField("Back 色温度 (K)", rig.backKelvin);
                    if (rig.rigLightKind == RigLightKind.Spot)
                        rig.spotAngle = EditorGUILayout.Slider("Spot 角度", rig.spotAngle, 1f, 179f);
                    else
                        rig.areaSize = EditorGUILayout.Vector2Field("Area サイズ", rig.areaSize);
                    break;

                case RigType.Sun:
                    rig.sunElevation = EditorGUILayout.Slider("太陽高度", rig.sunElevation, 0f, 90f);
                    rig.sunAzimuth = EditorGUILayout.Slider("方位角", rig.sunAzimuth, 0f, 360f);
                    rig.sunIntensity = EditorGUILayout.FloatField("太陽強度", rig.sunIntensity);
                    rig.sunKelvin = EditorGUILayout.FloatField("太陽色温度 (K)", rig.sunKelvin);
                    rig.skyFill = EditorGUILayout.Toggle("空フィル", rig.skyFill);
                    using (new EditorGUI.DisabledScope(!rig.skyFill))
                        rig.skyFillRatio = EditorGUILayout.Slider("太陽:フィル比", rig.skyFillRatio, 2f, 10f);
                    using (new EditorGUI.DisabledScope(!HdriSunOps.CanAlign(look)))
                        if (Button("🧭 HDRI の太陽に向きを合わせる", BossLookDevPalette.Auto))
                        {
                            if (HdriSunOps.AlignSunToHdri(look, out string msg))
                            {
                                if (GameObject.Find(SceneBindingOps.RigRootName(look)) != null)
                                    LightRigAuthoring.CreateOrUpdateRig(look, QuickStartOps.ResolveSubject());
                                ShowNotification(new GUIContent("太陽の向きを合わせました"));
                            }
                            Debug.Log("[BOSS Look Dev] " + msg);
                        }
                    break;

                case RigType.CeilingGrid:
                    EditorGUILayout.HelpBox("プローブ範囲を天井とみなし、下向きライトを格子状に配置します (全 Baked)。", MessageType.None);
                    rig.gridLightKind = (GridLightKind)EditorGUILayout.EnumPopup("ライト種別", rig.gridLightKind);
                    rig.gridRows = EditorGUILayout.IntSlider("行数 (奥行)", rig.gridRows, 1, 8);
                    rig.gridColumns = EditorGUILayout.IntSlider("列数 (横)", rig.gridColumns, 1, 8);
                    rig.gridIntensity = EditorGUILayout.FloatField("強度 (1灯)", rig.gridIntensity);
                    rig.gridKelvin = EditorGUILayout.FloatField("色温度 (K)", rig.gridKelvin);
                    if (rig.gridLightKind == GridLightKind.Spot)
                        rig.gridSpotAngle = EditorGUILayout.Slider("Spot 角度", rig.gridSpotAngle, 1f, 179f);
                    EditorGUILayout.LabelField($"合計 {rig.gridRows * rig.gridColumns} 灯", EditorStyles.miniLabel);
                    break;
            }

            if (rig.rigType != RigType.CeilingGrid)
            {
                rig.useCaustics = EditorGUILayout.Toggle("🌊 水中表現 (コースティクス)", rig.useCaustics);
                if (rig.useCaustics)
                {
                    rig.causticsCookie = (Texture)EditorGUILayout.ObjectField("　Cookie", rig.causticsCookie, typeof(Texture), false);
                    using (new EditorGUI.DisabledScope(rig.causticsCookie == null))
                        rig.causticsCookieSize = EditorGUILayout.FloatField("　Cookie サイズ", rig.causticsCookieSize);
                    BossHint("主ライト（太陽/キー）に caustics の cookie を静的に割り当てます。テクスチャは自分で用意。揺らぎのアニメ（スクロール）はプロジェクト側のシェーダ/スクリプト — 「8. 事故チェック・強化チェックリスト」で生成されるリストを参照。");
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (Button("リグを生成 / 更新", BossLookDevPalette.Generate))
                    LightRigAuthoring.CreateOrUpdateRig(look, QuickStartOps.ResolveSubject());
                if (Button("削除", BossLookDevPalette.Danger, 22f, 64f))
                    LightRigAuthoring.DeleteRig(look);
            }
            if (!LightRigAuthoring.ColorTemperatureSupported)
                EditorGUILayout.HelpBox("色温度 (Kelvin) が無効です。クイックスタート/診断で有効化できます。", MessageType.Warning);
        }

        private void DrawBake()
        {
            bool canBake = LightingBakeOps.CanBake(look, out string reason);
            if (!canBake) EditorGUILayout.HelpBox(reason, MessageType.Warning);

            bool baked = LightingBakeOps.HasBakedData();
            if (baked && !Lightmapping.isRunning)
                EditorGUILayout.HelpBox(
                    "ベイク済みです。ライティングを調整したい時は、まず『ベイクをクリア』してリアルタイム表示に戻し、調整 → 再ベイク、が分かりやすいです（焼かれたままだと変化が見えません）。",
                    MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!canBake || Lightmapping.isRunning))
                    if (Button("🔥 ベイクを実行", BossLookDevPalette.Bake, 28f))
                        LightingBakeOps.StartBake(look);
                using (new EditorGUI.DisabledScope(!baked || Lightmapping.isRunning))
                    if (Button("ベイクをクリア（再調整用）", BossLookDevPalette.Danger, 28f))
                    {
                        LightingBakeOps.ClearBake();
                        ShowNotification(new GUIContent("ベイクをクリアしました"));
                    }
            }

            if (Lightmapping.isRunning)
            {
                var rect = GUILayoutUtility.GetRect(18f, 22f, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(rect, Lightmapping.buildProgress, $"Baking... {Lightmapping.buildProgress * 100f:F1}%");
                if (Button("キャンセル", BossLookDevPalette.Danger)) LightingBakeOps.CancelBake();
            }
        }

        // ---------------- Background (skybox show / transparent) ----------------

        private void DrawBackground()
        {
            using (Card("3. 背景・環境光", BossLookDevPalette.ForContext(look.targetContext)))
            {
                BossHint("ベイク後の『見せ方』。VR=スカイボックス、深海等のスタイライズVR=単色/グラデ、AR=透過(単色のα=0)。Look に保存され再現可能。");
                var bg = look.background;

                // Quick switches (the common cases — incl. AR skybox-off)
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Button("VR: スカイボックス表示", BossLookDevPalette.VR))
                    {
                        bg.enabled = true; bg.mode = BackgroundMode.Skybox;
                        EditorUtility.SetDirty(look); BackgroundOps.Apply(look);
                    }
                    if (Button("AR: 背景を透過", BossLookDevPalette.AR))
                    {
                        bg.enabled = true; bg.mode = BackgroundMode.SolidColor; bg.solidColor = new Color(0f, 0f, 0f, 0f);
                        EditorUtility.SetDirty(look); BackgroundOps.Apply(look);
                    }
                }
                string status = !bg.enabled ? "管理OFF（シーン既定のまま）"
                    : bg.mode == BackgroundMode.Skybox ? "スカイボックス表示 (VR)"
                    : bg.mode == BackgroundMode.SolidColor ? (bg.solidColor.a < 0.01f ? "透過 (AR)" : "単色")
                    : "縦グラデ";
                EditorGUILayout.LabelField("現在: " + status, EditorStyles.miniBoldLabel);
                EditorGUILayout.Space(2);

                EditorGUI.BeginChangeCheck();
                bg.enabled = EditorGUILayout.Toggle("背景・環境光をツールで上書きする（OFF＝今のまま）", bg.enabled);
                if (!bg.enabled)
                    BossHint("OFF：ツールは背景・環境光に触りません（今のスカイボックス／環境光のまま）。深海の縦グラデ背景・単色・AR透過・色付き環境光にしたい時だけ ON にしてください。");
                using (new EditorGUI.DisabledScope(!bg.enabled))
                {
                    EditorGUILayout.LabelField("背景 (カメラ)", EditorStyles.miniBoldLabel);
                    bg.mode = (BackgroundMode)EditorGUILayout.EnumPopup("背景モード", bg.mode);
                    switch (bg.mode)
                    {
                        case BackgroundMode.Skybox:
                            BossHint("HDRI / CG スカイボックスを表示（VR）。※下の単色／グラデの色はこのモードでは無効です。");
                            break;
                        case BackgroundMode.SolidColor:
                            bg.solidColor = EditorGUILayout.ColorField(new GUIContent("単色 (α=0 で透過 / AR)"), bg.solidColor, true, true, false);
                            break;
                        case BackgroundMode.Gradient:
                            bg.gradientTop = EditorGUILayout.ColorField("上 (浅い / 水面側)", bg.gradientTop);
                            bg.gradientBottom = EditorGUILayout.ColorField("下 (深い)", bg.gradientBottom);
                            bg.gradientExponent = EditorGUILayout.Slider("グラデ強さ", bg.gradientExponent, 0.2f, 4f);
                            break;
                    }
                    EditorGUILayout.LabelField("環境光", EditorStyles.miniBoldLabel);
                    bg.ambientMode = (LookAmbientMode)EditorGUILayout.EnumPopup("環境光モード", bg.ambientMode);
                    switch (bg.ambientMode)
                    {
                        case LookAmbientMode.Skybox:
                            BossHint("スカイボックス(HDRI)から環境光。※下の環境色はこのモードでは無効です。");
                            break;
                        case LookAmbientMode.Flat:
                            bg.ambientFlat = EditorGUILayout.ColorField("環境色", bg.ambientFlat);
                            break;
                        case LookAmbientMode.Gradient:
                            bg.ambientSky = EditorGUILayout.ColorField("上", bg.ambientSky);
                            bg.ambientEquator = EditorGUILayout.ColorField("中", bg.ambientEquator);
                            bg.ambientGround = EditorGUILayout.ColorField("下", bg.ambientGround);
                            break;
                    }
                    bg.ambientIntensity = EditorGUILayout.Slider("環境光強度", bg.ambientIntensity, 0f, 8f);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(look);
                    if (bg.enabled) BackgroundOps.Apply(look); // live
                }
                using (new EditorGUI.DisabledScope(!bg.enabled))
                    if (Button("背景・環境光を適用", BossLookDevPalette.Generate))
                        BackgroundOps.Apply(look);
                BossHint("深海VR例: 背景=グラデ(上=明るい青/下=濃紺)・環境光=グラデ・フォグ濃いめ＋寒色グレード。MR は State 切替で自動。");
            }
        }

        // ---------------- Color ----------------

        private void DrawColor()
        {
            using (Card("4. カラー", BossLookDevPalette.Color_))
            {
                BossHint("ライティングの上に乗せる色味の最終調整 (露出・コントラスト・彩度・色温度・Bloom・Vignette)。");
                bool styly = look.target == LookTarget.STYLY;
                var active = RenderPipelineDetector.Active;
                var adapter = styly ? null : AdapterRegistry.GetColorAdapter(active);

                if (styly)
                {
                    EditorGUILayout.HelpBox(
                        "STYLY: ポストは STYLY 側 (PPv1 / PlayMaker) に委譲。ここでの色は『ライティング/環境光への焼き込み』で反映され、Gamma でもベイク結果として残ります。",
                        MessageType.Info);
                }
                else if (adapter == null)
                {
                    EditorGUILayout.HelpBox(
                        active == LookPipeline.BuiltIn
                            ? "Built-in の color アダプタ (PPv2) が見つかりません。com.unity.postprocessing を追加してください。"
                            : "color アダプタ未検出。URP をインストールしてください。",
                        MessageType.Info);
                    return;
                }

                var c = look.color;
                c.enabled = EditorGUILayout.Toggle("カラー有効", c.enabled);
                EditorGUI.BeginChangeCheck();
                using (new EditorGUI.DisabledScope(!c.enabled))
                {
                    c.exposure = EditorGUILayout.Slider("露出 (EV)", c.exposure, -3f, 3f);
                    c.contrast = EditorGUILayout.Slider("コントラスト", c.contrast, -100f, 100f);
                    c.saturation = EditorGUILayout.Slider("彩度", c.saturation, -100f, 100f);
                    c.colorFilter = EditorGUILayout.ColorField("カラーフィルター", c.colorFilter);
                    c.temperature = EditorGUILayout.Slider("色温度 (寒⇔暖)", c.temperature, -100f, 100f);
                    c.tint = EditorGUILayout.Slider("ティント", c.tint, -100f, 100f);
                    c.bloom = EditorGUILayout.Toggle("Bloom", c.bloom);
                    using (new EditorGUI.DisabledScope(!c.bloom))
                        c.bloomIntensity = EditorGUILayout.Slider("　Bloom 強度", c.bloomIntensity, 0f, 2f);
                    c.vignette = EditorGUILayout.Toggle("Vignette", c.vignette);
                    using (new EditorGUI.DisabledScope(!c.vignette))
                        c.vignetteIntensity = EditorGUILayout.Slider("　Vignette 強度", c.vignetteIntensity, 0f, 1f);

                    if (!styly)
                    {
                        _showAdvancedColor = EditorGUILayout.Foldout(_showAdvancedColor, "詳細設定", true);
                        if (_showAdvancedColor)
                        {
                            EditorGUILayout.LabelField("Bloom", EditorStyles.miniBoldLabel);
                            c.bloomThreshold = EditorGUILayout.FloatField("　Threshold", c.bloomThreshold);
                            c.bloomScatter = EditorGUILayout.Slider("　Scatter (拡散)", c.bloomScatter, 0f, 1f);
                            c.bloomTint = EditorGUILayout.ColorField("　Tint", c.bloomTint);
                            EditorGUILayout.LabelField("Vignette", EditorStyles.miniBoldLabel);
                            c.vignetteSmoothness = EditorGUILayout.Slider("　Smoothness", c.vignetteSmoothness, 0f, 1f);
                            c.vignetteRoundness = EditorGUILayout.Slider("　Roundness", c.vignetteRoundness, 0f, 1f);
                            c.vignetteColor = EditorGUILayout.ColorField("　色", c.vignetteColor);
                            EditorGUILayout.LabelField("グレード", EditorStyles.miniBoldLabel);
                            c.hueShift = EditorGUILayout.Slider("　Hue シフト", c.hueShift, -180f, 180f);
                            c.tonemap = (TonemapMode)EditorGUILayout.EnumPopup("　Tonemapping", c.tonemap);
                            c.lut = (Texture)EditorGUILayout.ObjectField("　LUT", c.lut, typeof(Texture), false);
                            using (new EditorGUI.DisabledScope(c.lut == null))
                                c.lutContribution = EditorGUILayout.Slider("　LUT 強度", c.lutContribution, 0f, 1f);
                            BossHint("Tonemapping は SelfApp/HDR のみ。LUT は URP=常時 / PPv2=LDR(STYLY)のみ。DoF・Motion Blur・SSAO はモバイルXR方針で非対応。");
                        }
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(look);
                    if (styly) LookBakeOps.ApplyToLighting(look);          // live bake-into-lighting
                    else adapter.Apply(new EditorLookScope(look));          // live grade
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (styly)
                    {
                        if (Button("ライティングに焼き込む", BossLookDevPalette.Generate))
                            LookBakeOps.ApplyToLighting(look);
                        if (Button("リセット", BossLookDevPalette.Danger, 22f, 80f))
                            LookBakeOps.ResetLightingTint(look);
                    }
                    else
                    {
                        if (Button("カラーをセットアップ / 更新", BossLookDevPalette.Generate))
                            adapter.Setup(new EditorLookScope(look));
                        if (Button("外す", BossLookDevPalette.Danger, 22f, 64f))
                            adapter.Remove(new EditorLookScope(look));
                    }
                }
            }
        }

        // ---------------- Atmosphere ----------------

        private void DrawAtmosphere()
        {
            using (Card("5. フォグ（空気感・奥行き）", BossLookDevPalette.Atmosphere))
            {
                BossHint("遠くを霞ませて奥行き・空気感を出す『霧』。VR 向け。AR / MR では通常オフにします。");
                var a = look.atmosphere;
                EditorGUI.BeginChangeCheck();
                a.enabled = EditorGUILayout.Toggle("フォグ有効", a.enabled);
                using (new EditorGUI.DisabledScope(!a.enabled))
                {
                    a.fogMode = (FogMode)EditorGUILayout.EnumPopup("モード", a.fogMode);
                    a.fogColor = EditorGUILayout.ColorField("色", a.fogColor);
                    if (a.fogMode == FogMode.Linear)
                    {
                        a.fogStartDistance = EditorGUILayout.FloatField("開始距離", a.fogStartDistance);
                        a.fogEndDistance = EditorGUILayout.FloatField("終了距離", a.fogEndDistance);
                    }
                    else a.fogDensity = EditorGUILayout.Slider("濃度", a.fogDensity, 0f, 0.15f);
                }
                if (EditorGUI.EndChangeCheck())
                {
                    AtmosphereOps.Apply(look);
                    LightingBakeOps.ApplySkyboxHaze(look); // haze tint follows fog color
                    EditorUtility.SetDirty(look);
                }
                using (new EditorGUI.DisabledScope(!a.enabled))
                    if (Button("フォグ色を環境(HDRI)から自動", BossLookDevPalette.Auto))
                    {
                        AtmosphereOps.AutoFogColorFromEnvironment(look);
                        ShowNotification(new GUIContent("フォグ色をHDRIに合わせました"));
                    }
                BossHint("距離フォグ＝『遠くが霞む』グラデ。色をHDRIに合わせると遠景(skybox)と繋がって自然。遠景も霞ませるには『2. ライティング』の“遠景の霞み”を上げる。");
            }
        }

        // ---------------- VR↔MR state switching ----------------

        private void DrawStates()
        {
            using (Card("7. VR↔MR 切り替え (State)", BossLookDevPalette.MR))
            {
                BossHint("体験中に VR↔MR を切り替える仕組み。2状態は同じベイクを共有し、skybox / フォグ / 環境光などの差分だけを切り替えます。");
                var s = look.states;
                EditorGUI.BeginChangeCheck();
                s.enabled = EditorGUILayout.Toggle("State 切り替えを使う", s.enabled);
                using (new EditorGUI.DisabledScope(!s.enabled))
                {
                    DrawStatePair("状態A", s.stateA);
                    EditorGUILayout.Space(2);
                    DrawStatePair("状態B", s.stateB);

                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField("トランジション (SelfApp)", EditorStyles.boldLabel);
                    s.transition.smooth = EditorGUILayout.Toggle("スムーズ切替", s.transition.smooth);
                    using (new EditorGUI.DisabledScope(!s.transition.smooth))
                    {
                        s.transition.durationSeconds = EditorGUILayout.Slider("時間 (秒)", s.transition.durationSeconds, 0f, 5f);
                        s.transition.curve = EditorGUILayout.CurveField("カーブ", s.transition.curve);
                    }
                }
                if (EditorGUI.EndChangeCheck()) EditorUtility.SetDirty(look);

                using (new EditorGUI.DisabledScope(!s.enabled))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (Button($"State リグを生成 (Target={look.target})", BossLookDevPalette.Generate))
                        {
                            StateRigEmitter.Emit(look);
                            ShowNotification(new GUIContent("State リグを生成しました"));
                        }
                        if (Button("リグ削除", BossLookDevPalette.Danger, 22f, 80f))
                            StateRigEmitter.DeleteRig(look);
                    }
                    if (look.target == LookTarget.STYLY)
                        EditorGUILayout.HelpBox("STYLY: 宣言的リグ (BossLookDevState なし)。PlayMaker の Activate Game Object で VR_State / MR_State をトグルしてください。", MessageType.Info);
                    else
                        EditorGUILayout.HelpBox("SelfApp: 各 State に BossLookDevState が付きます。VR_State / MR_State を SetActive で切り替えるだけ（常に片方だけ有効）。各 State の Inspector に切替方法を表示します。", MessageType.Info);
                    BossHint("エンジニア向けの説明は Git の docs/vr-mr-switching.md にあります。");
                }
            }
        }

        private void DrawStatePair(string label, ContextStatePair pair)
        {
            EditorGUILayout.LabelField($"{label}: {pair.stateName} ({pair.context})", EditorStyles.boldLabel);
            pair.stateName = EditorGUILayout.TextField("名前", pair.stateName);
            pair.context = (LookContext)EditorGUILayout.EnumPopup("コンテキスト", pair.context);
            var d = pair.delta;
            d.skyboxVisible = EditorGUILayout.Toggle("スカイボックス表示 (VR) / 透過 (MR)", d.skyboxVisible);
            d.fogEnabled = EditorGUILayout.Toggle("フォグ", d.fogEnabled);
            d.ambientIntensity = EditorGUILayout.Slider("環境光強度", d.ambientIntensity, 0f, 8f);
            d.reflectionIntensity = EditorGUILayout.Slider("リフレクション強度", d.reflectionIntensity, 0f, 1f);
        }

        // ---------------- Ground shadow (AR only) ----------------

        private void DrawGroundShadow()
        {
            using (Card("6. AR グラウンドシャドウ", BossLookDevPalette.AR))
            {
                var g = look.groundShadow;
                EditorGUILayout.HelpBox("影だけを受ける透明な床を被写体の足元に生成し、AR で『浮いて見える』のを防ぎます。選択中オブジェクトを被写体として使います。", MessageType.None);
                EditorGUI.BeginChangeCheck();
                g.opacity = EditorGUILayout.Slider("影の濃さ", g.opacity, 0f, 1f);
                if (EditorGUI.EndChangeCheck()) GroundShadowOps.ApplyOpacity(look);
                g.sizeMultiplier = EditorGUILayout.Slider("サイズ倍率", g.sizeMultiplier, 1f, 6f);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Button("生成 / 更新", BossLookDevPalette.Generate)) GroundShadowOps.CreateOrUpdate(look);
                    if (Button("除去", BossLookDevPalette.Danger, 22f, 64f)) GroundShadowOps.Remove(look);
                }
            }
        }

        // ---------------- Lookbook (blend + snapshot) ----------------

        private void DrawLookbook()
        {
            using (Card("ルックの比較・ブレンド", BossLookDevPalette.Overrides))
            {
                BossHint("複数の Look を混ぜたり (A↔B)、今の状態を保存して変更前後を見比べる (before / after) ためのツールです。");
                EditorGUILayout.LabelField("2つの Look を混ぜる (A → B)", EditorStyles.boldLabel);
                _blendA = (LookDefinition)EditorGUILayout.ObjectField("A", _blendA, typeof(LookDefinition), false);
                _blendB = (LookDefinition)EditorGUILayout.ObjectField("B", _blendB, typeof(LookDefinition), false);
                _blendT = EditorGUILayout.Slider("ブレンド (A→B)", _blendT, 0f, 1f);
                using (new EditorGUI.DisabledScope(_blendA == null || _blendB == null))
                    if (Button("ブレンドをこの Look に適用", BossLookDevPalette.Auto))
                    {
                        LookBlendOps.BlendInto(look, _blendA, _blendB, _blendT);
                        var ad = AdapterRegistry.GetActiveColorAdapter();
                        ad?.Apply(new EditorLookScope(look));
                        LightingBakeOps.ApplyEnvironmentLive(look);
                    }

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("スナップショット (before / after)", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (Button("📸 スナップショット保存", BossLookDevPalette.Generate))
                        _snapshot = LookBlendOps.Capture(look);
                    using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_snapshot)))
                        if (Button("↩ 戻す (after→before)", BossLookDevPalette.Auto))
                        {
                            LookBlendOps.Restore(look, _snapshot);
                            var ad = AdapterRegistry.GetActiveColorAdapter();
                            ad?.Apply(new EditorLookScope(look));
                            LightingBakeOps.ApplyEnvironmentLive(look);
                        }
                }
                BossHint(string.IsNullOrEmpty(_snapshot) ? "現在の状態を保存して、変更後に見比べられます。" : "スナップショットあり。『戻す』で保存時点に戻ります。");
            }
        }

        // ---------------- Validate ----------------

        private void DrawValidate()
        {
            using (Card("8. 事故チェック・強化チェックリスト", BossLookDevPalette.Hold))
            {
                BossHint("実機で事故りやすい設定を自動チェック: 未ベイク / 純白アルベド / UV2 欠落 / 色空間 / カメラHDR / 色温度 / プローブ欠落 など。納品前に自分で気付くため。");
                if (Button("🔍 チェックを実行", BossLookDevPalette.Auto, 26f))
                    _issues = LookValidator.Run(look);

                if (Button("📄 海の質感 強化チェックリストを生成 (日本語)", BossLookDevPalette.Auto))
                {
                    var p = EnhancementChecklistGenerator.Generate(look);
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(p);
                    if (asset != null) EditorGUIUtility.PingObject(asset);
                    ShowNotification(new GUIContent("強化チェックリストを生成しました"));
                }

                if (_issues == null) return;
                foreach (var issue in _issues)
                {
                    var mt = issue.severity == LookIssueSeverity.Error ? MessageType.Error
                        : issue.severity == LookIssueSeverity.Warning ? MessageType.Warning : MessageType.Info;
                    EditorGUILayout.HelpBox(issue.message, mt);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (issue.targets != null && issue.targets.Count > 0 &&
                            GUILayout.Button($"対象を選択 ({issue.targets.Count})", GUILayout.Width(150)))
                            Selection.objects = issue.targets.ToArray();
                        if (issue.fix != null && Button(issue.fixLabel, BossLookDevPalette.Generate))
                        {
                            issue.fix();
                            _issues = LookValidator.Run(look);
                            GUIUtility.ExitGUI();
                        }
                    }
                }
            }
        }

        // ---------------- Create ----------------

        private void CreateNewLook()
        {
            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string defaultName = string.IsNullOrEmpty(sceneName) ? "NewLook" : sceneName + "_Look";
            string path = EditorUtility.SaveFilePanelInProject(
                "新規 Look を作成", defaultName, "asset", "Look Definition の保存先を選んでください");
            if (string.IsNullOrEmpty(path)) return;

            var created = ScriptableObject.CreateInstance<LookDefinition>();
            created.lookName = System.IO.Path.GetFileNameWithoutExtension(path);
            created.targetPipeline = RenderPipelineDetector.Active; // seed to current pipeline
            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();

            look = created;
            _issues = null;
            Selection.activeObject = created;
            EditorGUIUtility.PingObject(created);
        }

        // ---------------- UI helpers ----------------

        private static IDisposable Card(string title, Color accent)
        {
            var scope = new EditorGUILayout.VerticalScope(EditorStyles.helpBox);
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3f, 16f, GUILayout.Width(3f));
                EditorGUI.DrawRect(bar, accent);
                GUILayout.Space(4f);
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            }
            GUILayout.Space(3f);
            return scope;
        }

        private static bool Button(string label, Color tint, float height = 22f, float width = 0f)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = tint;
            bool pressed = width > 0f
                ? GUILayout.Button(label, GUILayout.Height(height), GUILayout.Width(width))
                : GUILayout.Button(label, GUILayout.Height(height));
            GUI.backgroundColor = prev;
            return pressed;
        }

        private static Enum ColoredEnumPopup(string label, Enum selected, Color accent)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var bar = GUILayoutUtility.GetRect(3f, 16f, GUILayout.Width(3f));
                EditorGUI.DrawRect(bar, accent);
                GUILayout.Space(4f);
                var prev = GUI.backgroundColor;
                GUI.backgroundColor = accent;
                var result = EditorGUILayout.EnumPopup(label, selected);
                GUI.backgroundColor = prev;
                return result;
            }
        }

        private static void ColorChip(string label, string value, Color accent)
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Width(150)))
            {
                var bar = GUILayoutUtility.GetRect(3f, 14f, GUILayout.Width(3f));
                EditorGUI.DrawRect(bar, accent);
                GUILayout.Space(3f);
                var prev = GUI.color; GUI.color = accent;
                EditorGUILayout.LabelField($"{label}: {value}", EditorStyles.miniBoldLabel);
                GUI.color = prev;
            }
        }

        private static void BossHint(string text)
        {
            var prev = GUI.color; GUI.color = new Color(1f, 1f, 1f, 0.6f);
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);
            GUI.color = prev;
        }

        private static bool TrySelectionBounds(out Bounds bounds)
        {
            bounds = default; bool any = false;
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                {
                    if (r == null) continue;
                    if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
                }
            }
            return any;
        }

        /// <summary>A LayerMask field that lists only named layers (no internal APIs).</summary>
        private static LayerMask LayerMaskField(string label, LayerMask mask)
        {
            var layers = new List<string>();
            var indices = new List<int>();
            for (int i = 0; i < 32; i++)
            {
                var n = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(n)) { layers.Add(n); indices.Add(i); }
            }
            int compact = 0;
            for (int i = 0; i < indices.Count; i++)
                if ((mask.value & (1 << indices[i])) != 0) compact |= (1 << i);
            int newCompact = EditorGUILayout.MaskField(label, compact, layers.ToArray());
            int newMask = 0;
            for (int i = 0; i < indices.Count; i++)
                if ((newCompact & (1 << i)) != 0) newMask |= (1 << indices[i]);
            return newMask;
        }
    }
}
