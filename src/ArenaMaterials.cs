using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmarenaPrivate
{
    // The retained arena's obsolete bake produces black atlas islands on the floor
    // and walls. Keep its texture atlas, UVs, colors and emission; bypass that bake
    // on private copies of arena materials. Never edit shared game materials.
    internal sealed class ArenaMaterials
    {
        private sealed class Swap
        {
            public Renderer Renderer;
            public int RendererId;
            public Material[] Original;
            public Material[] Repaired;
            public int LightmapIndex;
            public bool Tunnel;
            public readonly List<Material> Owned = new List<Material>();
        }

        private readonly List<Swap> swaps = new List<Swap>();
        private readonly HashSet<int> visited = new HashSet<int>();
        private readonly GameObject connector;
        private float arenaBrightness = 0.75f, tunnelBrightness = 0.45f;

        public ArenaMaterials(GameObject connector) { this.connector = connector; }

        public void Apply(float arena, float tunnel)
        {
            try
            {
                bool tintChanged = arenaBrightness != arena || tunnelBrightness != tunnel;
                arenaBrightness = arena; tunnelBrightness = tunnel;
                // Zone changes can unload/reload Arena without leaving the room.
                for (int i = swaps.Count - 1; i >= 0; i--)
                    if (swaps[i].Renderer == null) ReleaseAt(i, false);
                if (connector != null) Apply(connector, true);
                Scene scene = SceneManager.GetSceneByName("Arena");
                if (scene.IsValid() && scene.isLoaded)
                    foreach (GameObject root in scene.GetRootGameObjects()) Apply(root, false);
                if (tintChanged) foreach (Swap swap in swaps) Tint(swap);
            }
            catch { Restore(); throw; }
        }

        private void Apply(GameObject root, bool tunnel)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (visited.Contains(renderer.GetInstanceID())) continue;
                Material[] original = renderer.sharedMaterials;
                Swap swap = new Swap { Renderer = renderer, RendererId = renderer.GetInstanceID(), LightmapIndex = renderer.lightmapIndex, Tunnel = tunnel, Original = original, Repaired = (Material[])original.Clone() };
                // Register before allocation/assignment so a partial failure can unwind.
                swaps.Add(swap);
                for (int i = 0; i < original.Length; i++)
                {
                    Material source = original[i];
                    if (source == null || (!source.IsKeywordEnabled("_USE_DAY_NIGHT_LIGHTMAP") && !(tunnel && source.HasProperty("_Cull")))) continue;
                    Material repaired = new Material(source);
                    swap.Owned.Add(repaired);
                    repaired.name = source.name + " (Magmarena private lighting)";
                    repaired.DisableKeyword("_USE_DAY_NIGHT_LIGHTMAP");
                    if (repaired.HasProperty("_UseDayNightLightmap")) repaired.SetFloat("_UseDayNightLightmap", 0f);
                    // A desktop/shoulder camera can cross the retained one-sided
                    // tunnel shell. Keep its existing roof/walls visible from both sides.
                    if (tunnel && repaired.HasProperty("_Cull")) repaired.SetFloat("_Cull", 0f);
                    swap.Repaired[i] = repaired;
                }
                if (swap.Owned.Count != 0)
                {
                    Tint(swap);
                    renderer.sharedMaterials = swap.Repaired;
                    // LIGHTMAP_ON is selected by Unity per renderer, independently of
                    // the game's day/night interpolation material keyword.
                    renderer.lightmapIndex = -1;
                }
                visited.Add(renderer.GetInstanceID());
            }
        }

        private void Tint(Swap swap)
        {
            float brightness = swap.Tunnel ? tunnelBrightness : arenaBrightness;
            for (int i = 0; i < swap.Original.Length; i++)
            {
                Material source = swap.Original[i], copy = swap.Repaired[i];
                if (source == null || copy == null || copy == source || !source.IsKeywordEnabled("_USE_DAY_NIGHT_LIGHTMAP")) continue;
                string property = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
                if (property == null) continue;
                Color color = source.GetColor(property);
                color.r *= brightness; color.g *= brightness; color.b *= brightness;
                copy.SetColor(property, color);
            }
        }

        public void Restore()
        {
            Exception firstError = null;
            for (int i = swaps.Count - 1; i >= 0; i--)
            {
                try { ReleaseAt(i, true); }
                catch (Exception ex) { if (firstError == null) firstError = ex; }
            }
            if (firstError != null) throw new InvalidOperationException("Some private lighting copies still need cleanup", firstError);
        }

        private void ReleaseAt(int index, bool restore)
        {
            Swap swap = swaps[index];
            if (restore && swap.Renderer != null && swap.Owned.Count != 0)
            {
                Material[] current = swap.Renderer.sharedMaterials;
                bool changed = false;
                for (int j = 0; j < current.Length && j < swap.Repaired.Length; j++)
                    if (current[j] == swap.Repaired[j] && swap.Repaired[j] != swap.Original[j])
                    { current[j] = swap.Original[j]; changed = true; }
                if (changed) swap.Renderer.sharedMaterials = current;
                if (swap.Renderer.lightmapIndex == -1) swap.Renderer.lightmapIndex = swap.LightmapIndex;
            }
            foreach (Material owned in swap.Owned) if (owned != null) UnityEngine.Object.Destroy(owned);
            visited.Remove(swap.RendererId);
            swaps.RemoveAt(index);
        }

        public void Describe(StringBuilder text)
        {
            int count = 0;
            foreach (Swap swap in swaps) count += swap.Owned.Count;
            text.AppendLine("Private lighting material copies: " + count);
            text.AppendLine("Visual brightness: arena=" + arenaBrightness + " tunnel=" + tunnelBrightness);
            LightmapData[] maps = LightmapSettings.lightmaps;
            for (int i = 0; i < maps.Length; i++)
                text.AppendLine("Lightmap " + i + ": color=" + Name(maps[i].lightmapColor) + " direction=" + Name(maps[i].lightmapDir));
            Scene scene = SceneManager.GetSceneByName("Arena");
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.gameObject.activeInHierarchy || !renderer.enabled) continue;
                    text.AppendLine("Arena visual: " + Diagnostics.Path(renderer.transform) + " map=" + renderer.lightmapIndex + " st=" + renderer.lightmapScaleOffset + " batched=" + renderer.isPartOfStaticBatch);
                    foreach (Material material in renderer.sharedMaterials)
                        if (material != null) text.AppendLine("  Material: " + material.name + " shader=" + Name(material.shader) + " baked=" + material.IsKeywordEnabled("_USE_DAY_NIGHT_LIGHTMAP"));
                }
        }

        private static string Name(UnityEngine.Object value) { return value == null ? "null" : value.name; }
    }
}
