using Boss.LookDev.Runtime;
using UnityEditor;
using UnityEngine;

namespace Boss.LookDev.Editor.Ops
{
    /// <summary>
    /// (Editor / emitter) Generates the VR↔MR switch rig (spec §5.1) as a switch
    /// root with two state objects. Toggling which is active (SetActive) swaps the
    /// look. Branches by Target:
    /// - SelfApp: each state gets a BossLookDevState runtime component that applies
    ///   its delta + smooth transition on enable. Engineer calls SetActive.
    /// - STYLY: declarative containers only (no custom C#); the engineer toggles
    ///   them with PlayMaker's Activate Game Object and drives globals via PlayMaker
    ///   actions. See the generated handoff doc.
    /// </summary>
    public static class StateRigEmitter
    {
        public static string SwitchName(LookDefinition look) => $"{look.lookName} Switch";

        public static GameObject Emit(LookDefinition look)
        {
            return look.target == LookTarget.STYLY ? EmitStyly(look) : EmitSelfApp(look);
        }

        private static GameObject EnsureSwitchRoot(LookDefinition look)
        {
            var name = SwitchName(look);
            var go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, "Create State Switch");
            }
            SceneBindingOps.Parent(go, look);
            return go;
        }

        private static GameObject EnsureStateChild(GameObject root, string stateName)
        {
            var t = root.transform.Find(stateName);
            if (t != null) return t.gameObject;
            var go = new GameObject(stateName);
            Undo.RegisterCreatedObjectUndo(go, "Create State Object");
            go.transform.SetParent(root.transform, false);
            return go;
        }

        private static GameObject EmitSelfApp(LookDefinition look)
        {
            var root = EnsureSwitchRoot(look);
            var a = EnsureStateChild(root, look.states.stateA.stateName);
            var b = EnsureStateChild(root, look.states.stateB.stateName);

            BindState(a, look, false);
            BindState(b, look, true);

            a.SetActive(true);
            b.SetActive(false); // exactly one active
            EditorUtility.SetDirty(look);
            return root;
        }

        private static void BindState(GameObject go, LookDefinition look, bool secondary)
        {
            var state = go.GetComponent<BossLookDevState>();
            if (state == null) state = Undo.AddComponent<BossLookDevState>(go);
            state.look = look;
            state.isSecondaryState = secondary;
            EditorUtility.SetDirty(state);
        }

        private static GameObject EmitStyly(LookDefinition look)
        {
            var root = EnsureSwitchRoot(look);
            var a = EnsureStateChild(root, look.states.stateA.stateName);
            var b = EnsureStateChild(root, look.states.stateB.stateName);

            // STYLY runs no custom C#: strip BossLookDevState if present.
            foreach (var go in new[] { a, b })
            {
                var state = go.GetComponent<BossLookDevState>();
                if (state != null) Undo.DestroyObjectImmediate(state);
            }

            a.SetActive(true);
            b.SetActive(false);
            return root;
        }

        public static void DeleteRig(LookDefinition look)
        {
            var go = GameObject.Find(SwitchName(look));
            if (go != null) Undo.DestroyObjectImmediate(go);
        }
    }
}
