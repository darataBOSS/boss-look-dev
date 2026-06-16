using System.IO;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// The look is a project asset, but it generates objects that live in the
    /// scene (probe group, rig, reflection probe, volume). Asset→scene references
    /// don't survive editor reloads, so — like the legacy SceneRelinkOps — we find
    /// generated objects by a stable naming convention instead of storing refs.
    /// Everything is gathered under one container per look.
    /// </summary>
    public static class SceneBindingOps
    {
        public static string ContainerName(LookDefinition look) => $"BOSS Look Dev [{look.lookName}]";

        // Generated object names (stable keys for re-finding).
        public static string ProbeGroupName(LookDefinition look) => $"{look.lookName} Light Probes";
        public static string ReflectionProbeName(LookDefinition look) => $"{look.lookName} Reflection Probe";
        public static string InteriorProbeGroupName(LookDefinition look) => $"{look.lookName} Interior Light Probes";
        public static string InteriorReflectionProbeName(LookDefinition look) => $"{look.lookName} Interior Reflection Probe";
        public static string RigRootName(LookDefinition look) => $"{look.lookName} Light Rig";
        public static string VolumeName(LookDefinition look) => $"{look.lookName} Volume";

        /// <summary>Folder the look asset lives in; generated assets go beside it.</summary>
        public static string AssetFolder(LookDefinition look)
        {
            var path = AssetDatabase.GetAssetPath(look);
            if (string.IsNullOrEmpty(path)) return "Assets";
            var dir = Path.GetDirectoryName(path);
            return string.IsNullOrEmpty(dir) ? "Assets" : dir.Replace('\\', '/');
        }

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            var parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            var leaf = Path.GetFileName(folderPath);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, leaf);
            }
        }

        public static Transform GetOrCreateContainer(LookDefinition look)
        {
            var name = ContainerName(look);
            var existing = GameObject.Find(name);
            if (existing == null)
            {
                existing = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(existing, "Create BOSS Look Dev container");
            }
            return existing.transform;
        }

        /// <summary>Re-parents a generated object under the container, keeping its
        /// world position so nothing visually shifts.</summary>
        public static void Parent(GameObject go, LookDefinition look)
        {
            if (go == null || look == null) return;
            var container = GetOrCreateContainer(look);
            if (container == null || go.transform.parent == container || go == container.gameObject) return;
            Undo.SetTransformParent(go.transform, container, "Organize under BOSS Look Dev container");
        }

        public static T FindComponentInScene<T>(string objectName) where T : Component
        {
            var go = GameObject.Find(objectName);
            return go != null ? go.GetComponent<T>() : null;
        }

        public static T[] FindAll<T>() where T : Object
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#else
            return Object.FindObjectsOfType<T>();
#endif
        }
    }
}
