using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (B / common core) Light rig authoring (spec §4.1) — studio three-point and
    /// outdoor sun. Ported from the legacy LightRigOps, driven by
    /// LookDefinition.lighting.rig. Pipeline-agnostic. Subject is passed in
    /// (selection / scene center), not stored on the asset.
    /// </summary>
    public static class LightRigAuthoring
    {
        public static bool ColorTemperatureSupported =>
            GraphicsSettings.lightsUseColorTemperature && GraphicsSettings.lightsUseLinearIntensity;

        public static void EnableColorTemperature()
        {
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
        }

        public static void CreateOrUpdateRig(LookDefinition look, Vector3 subjectPos)
        {
            var rigRootName = SceneBindingOps.RigRootName(look);
            var root = GameObject.Find(rigRootName);
            if (root == null)
            {
                root = new GameObject(rigRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create BOSS Light Rig");
            }

            ClearMismatched(root, look.lighting.rig.rigType);

            switch (look.lighting.rig.rigType)
            {
                case RigType.ThreePoint: BuildThreePoint(root, look.lighting.rig, subjectPos); break;
                case RigType.Sun: BuildSun(root, look.lighting.rig, subjectPos); break;
                case RigType.CeilingGrid: BuildCeilingGrid(root, look.lighting.rig, look.lighting.probeArea); break;
            }

            SceneBindingOps.Parent(root, look);
        }

        public static void DeleteRig(LookDefinition look)
        {
            var root = GameObject.Find(SceneBindingOps.RigRootName(look));
            if (root != null) Undo.DestroyObjectImmediate(root);
        }

        private static void ClearMismatched(GameObject root, RigType rigType)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var child = root.transform.GetChild(i).gameObject;
                string n = child.name;
                bool keep;
                switch (rigType)
                {
                    case RigType.ThreePoint: keep = n.EndsWith(" - Key") || n.EndsWith(" - Fill") || n.EndsWith(" - Back"); break;
                    case RigType.Sun: keep = n.EndsWith(" - Sun") || n.EndsWith(" - Sky Fill"); break;
                    case RigType.CeilingGrid: keep = n.Contains(" - Grid "); break;
                    default: keep = false; break;
                }
                if (!keep) Undo.DestroyObjectImmediate(child);
            }
        }

        private static Light MakeOrFind(GameObject parent, string label)
        {
            var name = $"{parent.name} - {label}";
            var t = parent.transform.Find(name);
            if (t != null) return t.GetComponent<Light>();
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {label} Light");
            go.transform.SetParent(parent.transform, true);
            return go.AddComponent<Light>();
        }

        // ---- Three-point ----

        private static void BuildThreePoint(GameObject root, LightRigConfig rig, Vector3 subjectPos)
        {
            const float radius = 4f;
            var key = MakeOrFind(root, "Key");
            var fill = MakeOrFind(root, "Fill");
            var back = MakeOrFind(root, "Back");

            ConfigureFocus(key, rig, subjectPos, AngleOffset(45f, 30f, radius),
                rig.keyIntensity, rig.keyKelvin, LightShadows.Soft);

            float fillIntensity = Mathf.Max(0.01f, rig.keyIntensity / Mathf.Max(0.1f, rig.keyFillRatio));
            ConfigureFocus(fill, rig, subjectPos, AngleOffset(-60f, 15f, radius),
                fillIntensity, rig.fillKelvin, LightShadows.None);

            ConfigureFocus(back, rig, subjectPos, AngleOffset(180f, 45f, radius),
                rig.keyIntensity * 0.6f, rig.backKelvin, LightShadows.None);

            ApplyCausticsCookie(key, rig);
        }

        /// <summary>Assigns the caustics cookie to a rig light (static). Animating
        /// the scroll is a project-side shader/script task (see the enhancement
        /// checklist). Passing a null cookie clears it.</summary>
        private static void ApplyCausticsCookie(Light light, LightRigConfig rig)
        {
            if (light == null) return;
            light.cookie = rig.useCaustics ? rig.causticsCookie : null;
            if (light.type == LightType.Directional && rig.useCaustics && rig.causticsCookie != null)
                light.cookieSize = Mathf.Max(0.01f, rig.causticsCookieSize);
        }

        private static Vector3 AngleOffset(float yawDeg, float pitchDeg, float radius)
        {
            float yaw = yawDeg * Mathf.Deg2Rad, pitch = pitchDeg * Mathf.Deg2Rad;
            return new Vector3(
                Mathf.Sin(yaw) * Mathf.Cos(pitch),
                Mathf.Sin(pitch),
                Mathf.Cos(yaw) * Mathf.Cos(pitch)) * radius;
        }

        private static void ConfigureFocus(Light light, LightRigConfig rig, Vector3 subjectPos,
            Vector3 offset, float intensity, float kelvin, LightShadows shadows)
        {
            if (light == null) return;
            Undo.RecordObject(light.transform, "Configure Light Transform");
            Undo.RecordObject(light, "Configure Light");

            light.transform.position = subjectPos + offset;
            light.transform.LookAt(subjectPos);
            light.type = rig.rigLightKind == RigLightKind.Spot ? LightType.Spot : LightType.Rectangle;
            light.lightmapBakeType = light.type == LightType.Spot ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
            light.shadows = shadows;
            light.intensity = intensity;
            light.useColorTemperature = true;
            light.colorTemperature = kelvin;
            light.color = Color.white;

            if (light.type == LightType.Spot)
            {
                light.spotAngle = rig.spotAngle;
                light.range = Mathf.Max(1f, offset.magnitude * 2f);
            }
            else
            {
                light.areaSize = rig.areaSize;
            }
        }

        // ---- Sun ----

        private static void BuildSun(GameObject root, LightRigConfig rig, Vector3 anchor)
        {
            var sun = MakeOrFind(root, "Sun");
            Undo.RecordObject(sun.transform, "Configure Sun");
            Undo.RecordObject(sun, "Configure Sun");
            sun.transform.position = anchor + Vector3.up * 8f;
            sun.transform.rotation = Quaternion.Euler(rig.sunElevation, rig.sunAzimuth, 0f);
            sun.type = LightType.Directional;
            sun.lightmapBakeType = LightmapBakeType.Mixed;
            sun.shadows = LightShadows.Soft;
            sun.intensity = rig.sunIntensity;
            sun.useColorTemperature = true;
            sun.colorTemperature = rig.sunKelvin;
            sun.color = Color.white;
            ApplyCausticsCookie(sun, rig);

            var existingFill = root.transform.Find($"{root.name} - Sky Fill");
            if (rig.skyFill)
            {
                var fill = MakeOrFind(root, "Sky Fill");
                Undo.RecordObject(fill.transform, "Configure Sky Fill");
                Undo.RecordObject(fill, "Configure Sky Fill");
                fill.transform.position = anchor + Vector3.up * 8f;
                fill.transform.rotation = Quaternion.Euler(45f, rig.sunAzimuth + 180f, 0f);
                fill.type = LightType.Directional;
                fill.lightmapBakeType = LightmapBakeType.Baked;
                fill.shadows = LightShadows.None;
                fill.intensity = rig.sunIntensity / Mathf.Max(1f, rig.skyFillRatio);
                fill.useColorTemperature = true;
                fill.colorTemperature = rig.skyFillKelvin;
                fill.color = Color.white;
            }
            else if (existingFill != null)
            {
                Undo.DestroyObjectImmediate(existingFill.gameObject);
            }
        }

        // ---- Ceiling grid (large interior) ----

        private static void BuildCeilingGrid(GameObject root, LightRigConfig rig, Bounds bounds)
        {
            // Rebuild fresh so row/column changes don't leave orphans.
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                var c = root.transform.GetChild(i).gameObject;
                if (c.name.Contains(" - Grid ")) Undo.DestroyObjectImmediate(c);
            }

            int rows = Mathf.Clamp(rig.gridRows, 1, 8);
            int cols = Mathf.Clamp(rig.gridColumns, 1, 8);
            float ceilingY = bounds.max.y - 0.05f;
            float cellX = bounds.size.x / cols;
            float cellZ = bounds.size.z / rows;

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                var light = MakeOrFind(root, $"Grid R{r}C{c}");
                Undo.RecordObject(light.transform, "Configure Grid Light");
                Undo.RecordObject(light, "Configure Grid Light");
                light.transform.position = new Vector3(
                    bounds.min.x + (c + 0.5f) * cellX, ceilingY, bounds.min.z + (r + 0.5f) * cellZ);
                light.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // straight down

                // All-Baked: a whole array of Mixed lights would be a realtime cost
                // disaster on mobile XR. Dynamic objects get lit via probes.
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.shadows = LightShadows.Soft;
                light.intensity = rig.gridIntensity;
                light.useColorTemperature = true;
                light.colorTemperature = rig.gridKelvin;
                light.color = Color.white;

                switch (rig.gridLightKind)
                {
                    case GridLightKind.Spot:
                        light.type = LightType.Spot;
                        light.spotAngle = rig.gridSpotAngle;
                        light.range = Mathf.Max(2f, bounds.size.y * 2f);
                        break;
                    case GridLightKind.Point:
                        light.type = LightType.Point;
                        light.range = Mathf.Max(2f, bounds.size.y * 1.5f);
                        break;
                    case GridLightKind.Area:
                        light.type = LightType.Rectangle;
                        light.areaSize = new Vector2(cellX * 0.6f, cellZ * 0.6f);
                        break;
                }
            }
        }
    }
}
