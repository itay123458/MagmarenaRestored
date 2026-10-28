using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MagmarenaPrivate
{
    internal sealed class DoorwayPatch : IActivationTarget
    {
        private sealed class RenderChange
        {
            public MeshFilter Filter;
            public Mesh Original, Replacement;
        }
        private readonly GameObject forest;
        private readonly MeshFilter originalDoorway;
        private readonly MeshRenderer doorwayMaterial;
        private readonly MeshCollider wall;
        private readonly Bounds region = new Bounds(new Vector3(-47.6f, 6.67f, -27.21f), new Vector3(5.92f, 4.52f, 4.34f));
        private readonly List<RenderChange> renderChanges = new List<RenderChange>();
        private readonly List<Mesh> generated = new List<Mesh>();
        private Mesh originalCollision, replacementCollision;
        private GameObject patchObject;
        private bool applied, collisionPending;
        public string Name { get { return "Original raised Magmarena doorway"; } }
        public bool IsAlive { get { return forest != null || applied; } }
        public bool Active { get { return applied; } set { if (value) Apply(); else Restore(); } }

        public DoorwayPatch(GameObject forest, MeshFilter originalDoorway, MeshRenderer doorwayMaterial, MeshCollider wall)
        {
            this.forest = forest; this.originalDoorway = originalDoorway; this.doorwayMaterial = doorwayMaterial; this.wall = wall;
            if (originalDoorway == null || originalDoorway.sharedMesh == null || doorwayMaterial == null || wall == null || wall.sharedMesh == null)
                throw new InvalidOperationException("Original doorway geometry is missing");
            if (!originalDoorway.sharedMesh.isReadable || !wall.sharedMesh.isReadable)
                throw new InvalidOperationException("Original doorway geometry cannot be read safely");
        }

        private void Apply()
        {
            if (applied) return;
            applied = true; // Makes partial writes visible to transaction rollback.
            try
            {
                bool changed;
                ForestLightmap lighting = new ForestLightmap();
                foreach (MeshFilter filter in forest.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.gameObject.name.IndexOf("combined by EdMeshCombiner", StringComparison.Ordinal) < 0) continue;
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.bounds.Intersects(region) || filter.sharedMesh == null) continue;
                    if (!filter.name.Contains("b8bde7b58cf818449b6f4a2dd617edde"))
                        lighting.Add(filter.sharedMesh,filter.transform,renderer,region);
                    Mesh trimmed = MeshRegion.Build(filter.sharedMesh, filter.transform, region, false, null, null, out changed);
                    if (!changed) { UnityEngine.Object.Destroy(trimmed); continue; }
                    generated.Add(trimmed);
                    renderChanges.Add(new RenderChange { Filter = filter, Original = filter.sharedMesh, Replacement = trimmed });
                }
                if (renderChanges.Count == 0) throw new InvalidOperationException("No batched doorway cap was found; Forest geometry was left unchanged");
                originalCollision = wall.sharedMesh;
                Mesh doorwayRegion = MeshRegion.Build(originalDoorway.sharedMesh, originalDoorway.transform, region, true, null, null, out changed);
                generated.Add(doorwayRegion);
                // The old rock threshold rises from 5.03m to 5.59m here. Remove
                // its part inside the new walking opening so it cannot protrude
                // through the flat 5.14m sleeve floor as a brown strip.
                Bounds walkingOpening = new Bounds(new Vector3(-47.6f, 6.67f, -26.295f), new Vector3(2.602f, 3.062f, 2.53f));
                Mesh doorway = MeshRegion.Build(doorwayRegion, originalDoorway.transform, walkingOpening, false, null, null, out changed);
                generated.Add(doorway);
                replacementCollision = MeshRegion.Build(originalCollision, wall.transform, region, false,
                    doorway, originalDoorway.transform, out changed);
                generated.Add(replacementCollision);
                if (doorway.vertexCount == 0) throw new InvalidOperationException("Original doorway patch is empty");
                int forestLightmap = lighting.Apply(doorway, originalDoorway.transform);

                foreach (RenderChange change in renderChanges) change.Filter.sharedMesh = change.Replacement;
                collisionPending = true; wall.sharedMesh = replacementCollision;
                patchObject = new GameObject("Magmarena Original Doorway Patch");
                patchObject.SetActive(false);
                SceneManager.MoveGameObjectToScene(patchObject, forest.scene);
                patchObject.transform.SetPositionAndRotation(originalDoorway.transform.position, originalDoorway.transform.rotation);
                patchObject.transform.localScale = originalDoorway.transform.lossyScale;
                patchObject.transform.SetParent(forest.transform, true);
                patchObject.AddComponent<MeshFilter>().sharedMesh = doorway;
                MeshRenderer patchRenderer = patchObject.AddComponent<MeshRenderer>();
                patchRenderer.sharedMaterials = doorwayMaterial.sharedMaterials;
                patchRenderer.lightmapIndex = forestLightmap;
                patchRenderer.lightmapScaleOffset = new Vector4(1,1,0,0);
                patchRenderer.shadowCastingMode = doorwayMaterial.shadowCastingMode;
                patchRenderer.receiveShadows = doorwayMaterial.receiveShadows;
                patchObject.SetActive(true);
            }
            catch
            {
                try { Restore(); } catch { /* Outer activation transaction retains the pending target for cleanup. */ }
                throw;
            }
        }

        private void Restore()
        {
            Exception error = null;
            if (patchObject != null) patchObject.SetActive(false);
            for (int i = renderChanges.Count - 1; i >= 0; i--)
            {
                RenderChange change = renderChanges[i];
                try
                {
                    if (change.Filter != null && change.Filter.sharedMesh == change.Replacement) change.Filter.sharedMesh = change.Original;
                    renderChanges.RemoveAt(i);
                }
                catch (Exception ex) { error = ex; }
            }
            try
            {
                if (collisionPending && wall != null && wall.sharedMesh == replacementCollision) wall.sharedMesh = originalCollision;
                collisionPending = false;
            }
            catch (Exception ex) { error = ex; }
            if (error != null) throw error;
            if (patchObject != null) UnityEngine.Object.Destroy(patchObject);
            patchObject = null;
            foreach (Mesh mesh in generated) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            generated.Clear(); applied = false;
        }
    }
}
