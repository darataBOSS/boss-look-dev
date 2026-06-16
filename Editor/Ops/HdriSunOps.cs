using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B) Finds the brightest direction in an equirectangular HDRI and points the
    /// Sun rig at it, so cast shadows line up with the environment's own sun
    /// (spec §4.1 HDRI-IBL assist). Best-effort: equirectangular Texture2D only;
    /// the result is a good starting point to fine-tune with the sliders. Ported
    /// from the legacy HdriSunOps.
    /// </summary>
    public static class HdriSunOps
    {
        public static bool CanAlign(LookDefinition look) =>
            look != null && look.lighting.hdri is Texture2D && look.lighting.rig.rigType == RigType.Sun;

        public static bool AlignSunToHdri(LookDefinition look, out string message)
        {
            message = null;
            if (look == null || !(look.lighting.hdri is Texture2D tex))
            {
                message = "equirectangular な Texture2D の HDRI が必要です。";
                return false;
            }

            string path = AssetDatabase.GetAssetPath(tex);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            bool restoreReadable = false, restoreCompression = false;
            var prevCompression = TextureImporterCompression.Compressed;

            try
            {
                if (importer != null)
                {
                    if (!importer.isReadable) { importer.isReadable = true; restoreReadable = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed)
                    {
                        prevCompression = importer.textureCompression;
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        restoreCompression = true;
                    }
                    if (restoreReadable || restoreCompression) importer.SaveAndReimport();
                }

                if (!FindBrightestDirection(tex, look.lighting.skyboxRotation, out Vector3 sunDir))
                {
                    message = "テクスチャを読み取れませんでした (Read/Write を確認)。";
                    return false;
                }

                float elevation = Mathf.Asin(Mathf.Clamp(sunDir.y, -1f, 1f)) * Mathf.Rad2Deg;
                float azimuth = Mathf.Atan2(-sunDir.x, -sunDir.z) * Mathf.Rad2Deg;
                if (azimuth < 0f) azimuth += 360f;

                look.lighting.rig.sunElevation = Mathf.Clamp(elevation, 0f, 90f);
                look.lighting.rig.sunAzimuth = azimuth;
                EditorUtility.SetDirty(look);

                message = $"太陽の向きを HDRI から推定 (高度 {elevation:F0}° / 方位 {azimuth:F0}°)。スライダで微調整できます。";
                return true;
            }
            finally
            {
                if (importer != null && (restoreReadable || restoreCompression))
                {
                    if (restoreReadable) importer.isReadable = false;
                    if (restoreCompression) importer.textureCompression = prevCompression;
                    importer.SaveAndReimport();
                }
            }
        }

        private static bool FindBrightestDirection(Texture2D tex, float skyboxRotationDeg, out Vector3 dir)
        {
            dir = Vector3.up;
            Color[] pixels;
            try { pixels = tex.GetPixels(); } catch { return false; }
            if (pixels == null || pixels.Length == 0) return false;

            int w = tex.width, h = tex.height;
            int stride = Mathf.Max(1, Mathf.Max(w, h) / 512);
            float best = -1f; int bestX = 0, bestY = 0;
            for (int y = 0; y < h; y += stride)
            for (int x = 0; x < w; x += stride)
            {
                Color c = pixels[y * w + x];
                float lum = c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;
                if (lum > best) { best = lum; bestX = x; bestY = y; }
            }

            float u = (bestX + 0.5f) / w, v = (bestY + 0.5f) / h;
            float lon = (u - 0.5f) * 2f * Mathf.PI + skyboxRotationDeg * Mathf.Deg2Rad;
            float lat = (v - 0.5f) * Mathf.PI;
            float cosLat = Mathf.Cos(lat);
            dir = new Vector3(cosLat * Mathf.Sin(lon), Mathf.Sin(lat), cosLat * Mathf.Cos(lon)).normalized;
            return true;
        }
    }
}
