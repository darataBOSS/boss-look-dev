using System;
using UnityEngine;

namespace Boss.LookDev.Editor
{
    /// <summary>
    /// A named starting-point preset that stamps the look-feel fields (color /
    /// atmosphere / background / rig mood) onto a LookDefinition. Deliberately
    /// does NOT touch structural data (HDRI, probes, bake settings, generated
    /// objects) — a preset is a re-skin, not a rebuild.
    /// </summary>
    public sealed class LookPreset
    {
        public string name;
        public string description;
        public Action<LookDefinition> apply;
    }

    /// <summary>
    /// Built-in presets, shown in the window's dedicated "プリセット" section so the
    /// tool stays generic while offering one-click starting points (incl. the
    /// deep-sea / underwater looks the tool was built for).
    /// </summary>
    public static class LookPresetLibrary
    {
        public static LookPreset[] BuiltIn => new[]
        {
            Neutral(), DeepSea(), BrightWater(), Cinematic(),
        };

        private static LookPreset Neutral() => new LookPreset
        {
            name = "ニュートラル",
            description = "素の状態に戻す（色・フォグ・背景の作り込みをリセット）。",
            apply = look =>
            {
                var c = look.color;
                c.enabled = true; c.exposure = 0; c.contrast = 0; c.saturation = 0;
                c.colorFilter = Color.white; c.temperature = 0; c.tint = 0; c.hueShift = 0;
                c.tonemap = TonemapMode.ACES; c.bloom = true; c.bloomIntensity = 0.4f;
                c.vignette = true; c.vignetteIntensity = 0.25f; c.lut = null;
                look.atmosphere.enabled = false;
                look.background.enabled = false;
                look.lighting.rig.useCaustics = false;
            },
        };

        private static LookPreset DeepSea() => new LookPreset
        {
            name = "深海（神秘・暗め）",
            description = "光は上からわずか、濃い青、強いフォグ。深く神秘的な水中。背景(スカイボックス)はそのまま維持 — 暗い水中背景にしたい場合は『3. 背景・環境光』を ON に。",
            apply = look =>
            {
                var r = look.lighting.rig;
                r.rigType = RigType.Sun; r.sunElevation = 75; r.sunAzimuth = 20;
                r.sunIntensity = 1.0f; r.sunKelvin = 11000; r.skyFill = true; r.skyFillRatio = 3f; r.skyFillKelvin = 13000;
                r.useCaustics = true; // underwater — reveal caustics controls
                var c = look.color;
                c.enabled = true; c.exposure = -0.2f; c.contrast = 14; c.saturation = 4;
                c.temperature = -28; c.tint = -6; c.colorFilter = new Color(0.70f, 0.92f, 1f);
                c.bloom = true; c.bloomIntensity = 0.45f; c.vignette = true; c.vignetteIntensity = 0.45f;
                var a = look.atmosphere;
                a.enabled = true; a.fogMode = FogMode.ExponentialSquared; a.fogColor = new Color(0.02f, 0.10f, 0.14f); a.fogDensity = 0.10f;
                var b = look.background;
                b.enabled = false; b.mode = BackgroundMode.Gradient; // keep user skybox; opt-in via card 3
                b.gradientTop = new Color(0.06f, 0.22f, 0.30f); b.gradientBottom = new Color(0.005f, 0.03f, 0.06f); b.gradientExponent = 1.3f;
                b.ambientMode = LookAmbientMode.Gradient;
                b.ambientSky = new Color(0.08f, 0.20f, 0.26f); b.ambientEquator = new Color(0.04f, 0.12f, 0.16f); b.ambientGround = new Color(0.01f, 0.04f, 0.06f);
                b.ambientIntensity = 1.0f;
            },
        };

        private static LookPreset BrightWater() => new LookPreset
        {
            name = "海中（明るい・透明感）",
            description = "光量多め、フォグ薄め、シアン寄りで抜け感のある綺麗な海中。背景(スカイボックス)はそのまま維持 — 水中背景にしたい場合は『3. 背景・環境光』を ON に。",
            apply = look =>
            {
                var r = look.lighting.rig;
                r.rigType = RigType.Sun; r.sunElevation = 70; r.sunAzimuth = 20;
                r.sunIntensity = 1.5f; r.sunKelvin = 8800; r.skyFill = true; r.skyFillRatio = 2f; r.skyFillKelvin = 12000;
                r.useCaustics = true; // underwater — reveal caustics controls
                var c = look.color;
                c.enabled = true; c.exposure = 0.05f; c.contrast = 10; c.saturation = 10;
                c.temperature = -22; c.tint = -5; c.colorFilter = new Color(0.78f, 0.95f, 1f);
                c.bloom = true; c.bloomIntensity = 0.45f; c.vignette = true; c.vignetteIntensity = 0.32f;
                var a = look.atmosphere;
                a.enabled = true; a.fogMode = FogMode.ExponentialSquared; a.fogColor = new Color(0.06f, 0.22f, 0.26f); a.fogDensity = 0.06f;
                var b = look.background;
                b.enabled = false; b.mode = BackgroundMode.Gradient; // keep user skybox; opt-in via card 3
                b.gradientTop = new Color(0.12f, 0.35f, 0.42f); b.gradientBottom = new Color(0.01f, 0.06f, 0.10f); b.gradientExponent = 1.2f;
                b.ambientMode = LookAmbientMode.Gradient;
                b.ambientSky = new Color(0.14f, 0.30f, 0.36f); b.ambientEquator = new Color(0.08f, 0.20f, 0.25f); b.ambientGround = new Color(0.02f, 0.07f, 0.10f);
                b.ambientIntensity = 1.1f;
            },
        };

        private static LookPreset Cinematic() => new LookPreset
        {
            name = "シネマティック（汎用）",
            description = "やや沈めた露出・高コントラスト・寒色寄り。水中以外の汎用。",
            apply = look =>
            {
                var r = look.lighting.rig;
                r.rigType = RigType.ThreePoint; r.keyKelvin = 6500; r.keyIntensity = 2.4f; r.keyFillRatio = 3f;
                var c = look.color;
                c.enabled = true; c.exposure = 0.2f; c.contrast = 16; c.saturation = 8;
                c.temperature = -10; c.tint = 0; c.colorFilter = new Color(0.9f, 0.96f, 1f);
                c.bloom = true; c.bloomIntensity = 0.5f; c.vignette = true; c.vignetteIntensity = 0.35f;
                var a = look.atmosphere;
                a.enabled = true; a.fogMode = FogMode.ExponentialSquared; a.fogColor = new Color(0.5f, 0.6f, 0.7f); a.fogDensity = 0.02f;
                look.background.enabled = false; // keep skybox
                look.lighting.rig.useCaustics = false;
            },
        };
    }
}
