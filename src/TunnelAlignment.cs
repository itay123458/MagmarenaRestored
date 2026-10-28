using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagmarenaPrivate
{
    internal sealed class TunnelAlignment : IActivationTarget
    {
        private sealed class Pose
        {
            public Transform Target;
            public Vector3 OriginalPosition, AlignedPosition;
            public Quaternion OriginalRotation, AlignedRotation;
        }

        private sealed class MeshSwap
        {
            public MeshFilter Target;
            public Mesh Original, Aligned;
        }

        private readonly GameObject connector;
        private readonly Transform[] forestObjects;
        private readonly List<Pose> poses = new List<Pose>();
        private readonly List<MeshSwap> meshes = new List<MeshSwap>();
        private readonly List<Mesh> owned = new List<Mesh>();
        private GameObject sleeve;
        private bool applied;

        public TunnelAlignment(GameObject connector, Transform[] forestObjects)
        { this.connector = connector; this.forestObjects = forestObjects; }

        public string Name { get { return "Original Forest tunnel alignment"; } }
        public bool IsAlive { get { return connector != null || applied; } }
        public bool Active
        {
            get { return applied; }
            set { if (value) Apply(); else Restore(); }
        }

        private void Apply()
        {
            if (applied) return;
            applied = true;
            try
            {
                Quaternion rotation = Quaternion.Euler(0, 90, 0);
                foreach (Transform target in forestObjects)
                {
                    if (target == null) throw new InvalidOperationException("A Forest tunnel anchor is missing");
                    Vector3 alignedPosition = Point(target.position);
                    poses.Add(new Pose { Target = target, OriginalPosition = target.position, OriginalRotation = target.rotation,
                        AlignedPosition = alignedPosition, AlignedRotation = rotation * target.rotation });
                }
                int casingVertices = 0;
                foreach (MeshFilter filter in connector.GetComponentsInChildren<MeshFilter>(true))
                {
                    // The native combiner stores both distant tunnel copies in
                    // these root meshes, separate from their collider hierarchies.
                    if (filter.transform.parent != connector.transform || !filter.name.Contains("combined by EdMeshCombiner")) continue;
                    Mesh original = filter.sharedMesh;
                    if (original == null || !original.isReadable) throw new InvalidOperationException("Tunnel render mesh is not readable");
                    Mesh aligned = UnityEngine.Object.Instantiate(original);
                    owned.Add(aligned);
                    aligned.name = original.name + " (Magmarena Forest alignment)";
                    casingVertices += Warp(aligned, filter.transform);
                    meshes.Add(new MeshSwap { Target = filter, Original = original, Aligned = aligned });
                }
                if (meshes.Count != 5) throw new InvalidOperationException("Unexpected retained tunnel render layout");
                if (casingVertices != 60) throw new InvalidOperationException("Unexpected Forest boundary casing layout");
                foreach (Pose pose in poses) pose.Target.SetPositionAndRotation(pose.AlignedPosition, pose.AlignedRotation);
                foreach (MeshSwap mesh in meshes) mesh.Target.sharedMesh = mesh.Aligned;
                MakeSleeve();
            }
            catch
            {
                try { Restore(); } catch { }
                throw;
            }
        }

        private static Vector3 Point(Vector3 p)
        {
            double[] value = TunnelCoordinates.Point(p.x, p.y, p.z);
            return new Vector3((float)value[0], (float)value[1], (float)value[2]);
        }

        private static Vector3 Direction(Vector3 p) { return new Vector3(p.z, p.y, -p.x); }

        private void MakeSleeve()
        {
            MeshRenderer concrete = connector.transform.Find("Concrete_Dirty (combined by EdMeshCombiner)").GetComponent<MeshRenderer>();
            Transform wall = forestObjects[0].Find("Arena_Connector/Walls_ForestToArena");
            MeshCollider nativeWall = wall == null ? null : wall.GetComponent<MeshCollider>();
            if (concrete == null || concrete.sharedMaterial == null || nativeWall == null)
                throw new InvalidOperationException("Original tunnel surface is missing");
            sleeve = new GameObject("Magmarena Doorway Sleeve");
            sleeve.SetActive(false);
            sleeve.transform.SetParent(connector.transform, false);
            // Close the small difference between the wider rock cutout and the
            // retained 2.6m tunnel mouth. These four solid sides leave its
            // walking opening clear, overlap the rock rim, and meet the native
            // mouth without coplanar floor/wall surfaces.
            Slab("Left", new Vector3(-49.73f, 6.67f, -26.475f), new Vector3(1.66f, 4.52f, 2.15f), concrete.sharedMaterial, nativeWall.gameObject.layer);
            Slab("Right", new Vector3(-45.47f, 6.67f, -26.475f), new Vector3(1.66f, 4.52f, 2.15f), concrete.sharedMaterial, nativeWall.gameObject.layer);
            Slab("Floor", new Vector3(-47.6f, 4.775f, -26.475f), new Vector3(2.6f, 0.73f, 2.15f), concrete.sharedMaterial, nativeWall.gameObject.layer);
            Slab("Roof", new Vector3(-47.6f, 8.565f, -26.475f), new Vector3(2.6f, 0.73f, 2.15f), concrete.sharedMaterial, nativeWall.gameObject.layer);
            sleeve.SetActive(true);
        }

        private void Slab(string name, Vector3 position, Vector3 size, Material material, int layer)
        {
            GameObject slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.SetActive(false);
            slab.transform.SetParent(sleeve.transform, false);
            slab.name = name;
            slab.layer = layer;
            slab.transform.position = position;
            slab.transform.localScale = size;
            slab.GetComponent<MeshRenderer>().sharedMaterial = material;
            slab.SetActive(true);
        }

        private static int Warp(Mesh mesh, Transform transform)
        {
            Vector3[] positions = mesh.vertices, normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            int casingVertices = 0;
            bool[] hidden = new bool[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                Vector3 world = transform.TransformPoint(positions[i]);
                if (!TunnelCoordinates.ForestHalf(world.z)) continue;
                Vector3 aligned = Point(world);
                if (TunnelCoordinates.ForestBoundaryCasing(world.x, world.y, world.z))
                { hidden[i] = true; casingVertices++; }
                positions[i] = transform.InverseTransformPoint(aligned);
                if (normals.Length == positions.Length)
                    normals[i] = transform.InverseTransformDirection(Direction(transform.TransformDirection(normals[i])));
                if (tangents.Length == positions.Length)
                {
                    Vector4 old = tangents[i];
                    Vector3 tangent = transform.InverseTransformDirection(Direction(transform.TransformDirection(new Vector3(old.x, old.y, old.z))));
                    tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, old.w);
                }
            }
            mesh.vertices = positions;
            if (normals.Length == positions.Length) mesh.normals = normals;
            if (tangents.Length == positions.Length) mesh.tangents = tangents;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] original = mesh.GetTriangles(sub);
                List<int> kept = new List<int>();
                for (int i=0;i<original.Length;i+=3)
                {
                    int count=(hidden[original[i]]?1:0)+(hidden[original[i+1]]?1:0)+(hidden[original[i+2]]?1:0);
                    if (count==0) { kept.Add(original[i]);kept.Add(original[i+1]);kept.Add(original[i+2]); }
                    else if (count!=3) throw new InvalidOperationException("Forest sign removal intersects unrelated geometry");
                }
                mesh.SetTriangles(kept,sub,false);
            }
            mesh.RecalculateBounds();
            return casingVertices;
        }

        private void Restore()
        {
            Exception error = null;
            if (sleeve != null) { sleeve.SetActive(false); UnityEngine.Object.Destroy(sleeve); sleeve = null; }
            for (int i = meshes.Count - 1; i >= 0; i--)
            {
                MeshSwap mesh = meshes[i];
                try
                {
                    if (mesh.Target != null && mesh.Target.sharedMesh == mesh.Aligned) mesh.Target.sharedMesh = mesh.Original;
                    meshes.RemoveAt(i);
                }
                catch (Exception ex) { error = ex; }
            }
            for (int i = poses.Count - 1; i >= 0; i--)
            {
                Pose pose = poses[i];
                try
                {
                    if (pose.Target != null)
                    {
                        if ((pose.Target.position - pose.AlignedPosition).sqrMagnitude < 0.000001f) pose.Target.position = pose.OriginalPosition;
                        if (Quaternion.Angle(pose.Target.rotation, pose.AlignedRotation) < 0.001f) pose.Target.rotation = pose.OriginalRotation;
                    }
                    poses.RemoveAt(i);
                }
                catch (Exception ex) { error = ex; }
            }
            if (error != null) throw error;
            foreach (Mesh mesh in owned) if (mesh != null) UnityEngine.Object.Destroy(mesh);
            owned.Clear(); applied = false;
        }
    }
}
