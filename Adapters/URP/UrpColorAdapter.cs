// Reserved: Boss.LookDev.URP assembly (spec §10).
//
// Editor-time authoring adapters live in Boss.LookDev.Editor (guarded) to avoid a
// circular dependency with the editor ops — see UrpColorAuthoringAdapter /
// UrpMaterialAuthoringAdapter. This assembly is kept as the home for future
// *runtime* URP adapters (e.g. live VR↔MR color-grade blending at play time),
// which would reference URP runtime types directly. No types yet.
