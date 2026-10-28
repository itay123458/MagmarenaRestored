using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = System.Object;

namespace MagmarenaPrivate
{
    internal static class MeshRegion
    {
        private sealed class Input
        {
            public ClipVertex[] Vertices;
            public int[][] Triangles;
            public bool Normals, Tangents, Colors;
            public bool[] UV = new bool[8];
        }
        private static Input Read(Mesh mesh, Transform transform)
        {
            if (!mesh.isReadable) throw new InvalidOperationException("Native doorway mesh is not readable: " + mesh.name);
            Input input = new Input();
            Vector3[] p = mesh.vertices, n = mesh.normals;
            Vector4[] t = mesh.tangents; Color[] colors = mesh.colors;
            input.Normals = n.Length == p.Length; input.Tangents = t.Length == p.Length; input.Colors = colors.Length == p.Length;
            List<Vector4>[] uv = new List<Vector4>[8];
            for (int c = 0; c < 8; c++) { uv[c] = new List<Vector4>(); mesh.GetUVs(c, uv[c]); input.UV[c] = uv[c].Count == p.Length; }
            input.Vertices = new ClipVertex[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                Vector3 world = transform.TransformPoint(p[i]); float[] data = new float[43];
                if (input.Normals) { data[0] = n[i].x; data[1] = n[i].y; data[2] = n[i].z; }
                if (input.Tangents) { data[3] = t[i].x; data[4] = t[i].y; data[5] = t[i].z; data[6] = t[i].w; }
                if (input.Colors) { data[7] = colors[i].r; data[8] = colors[i].g; data[9] = colors[i].b; data[10] = colors[i].a; }
                for (int c = 0; c < 8; c++) if (input.UV[c]) for (int j = 0; j < 4; j++) data[11 + c * 4 + j] = uv[c][i][j];
                input.Vertices[i] = new ClipVertex(world.x, world.y, world.z, data);
            }
            input.Triangles = new int[mesh.subMeshCount][];
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("Non-triangle native mesh");
                input.Triangles[sub] = mesh.GetTriangles(sub);
            }
            return input;
        }

        public static Mesh Build(Mesh source, Transform sourceTransform, Bounds region, bool keepInside,
            Mesh replacement, Transform replacementTransform, out bool changed)
        {
            Input input = Read(source, sourceTransform);
            List<ClipVertex>[] parts = new List<ClipVertex>[input.Triangles.Length];
            double[] min = { region.min.x, region.min.y, region.min.z }, max = { region.max.x, region.max.y, region.max.z };
            changed = false;
            for (int sub = 0; sub < parts.Length; sub++)
            {
                parts[sub] = new List<ClipVertex>(); int[] triangles = input.Triangles[sub];
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    ClipVertex a = input.Vertices[triangles[i]], b = input.Vertices[triangles[i + 1]], c = input.Vertices[triangles[i + 2]];
                    List<ClipVertex[]> clipped = TriangleClipper.Clip(a, b, c, min, max, keepInside);
                    if (clipped.Count != 1 || !Object.ReferenceEquals(clipped[0][0], a) || !Object.ReferenceEquals(clipped[0][1], b) || !Object.ReferenceEquals(clipped[0][2], c)) changed = true;
                    foreach (ClipVertex[] triangle in clipped) parts[sub].AddRange(triangle);
                }
            }
            if (replacement != null)
            {
                Input extra = Read(replacement, replacementTransform);
                foreach (int[] triangles in extra.Triangles)
                    for (int i = 0; i < triangles.Length; i += 3)
                        foreach (ClipVertex[] triangle in TriangleClipper.Clip(extra.Vertices[triangles[i]], extra.Vertices[triangles[i + 1]], extra.Vertices[triangles[i + 2]], min, max, true))
                            foreach (ClipVertex v in triangle)
                                parts[0].Add(new ClipVertex(v.X, v.Y, v.Z, new float[43])); // Collision-only merge.
                changed = true;
            }
            List<Vector3> positions = new List<Vector3>(), normals = new List<Vector3>();
            List<Vector4> tangents = new List<Vector4>(); List<Color> outColors = new List<Color>();
            List<Vector4>[] outUV = new List<Vector4>[8]; for (int c = 0; c < 8; c++) outUV[c] = new List<Vector4>();
            int[][] indices = new int[parts.Length][];
            for (int sub = 0; sub < parts.Length; sub++)
            {
                indices[sub] = new int[parts[sub].Count];
                for (int i = 0; i < parts[sub].Count; i++)
                {
                    ClipVertex v = parts[sub][i]; float[] d = v.Data; indices[sub][i] = positions.Count;
                    positions.Add(sourceTransform.InverseTransformPoint(new Vector3((float)v.X, (float)v.Y, (float)v.Z)));
                    if (input.Normals) normals.Add(new Vector3(d[0], d[1], d[2]).normalized);
                    if (input.Tangents) tangents.Add(new Vector4(d[3], d[4], d[5], d[6]));
                    if (input.Colors) outColors.Add(new Color(d[7], d[8], d[9], d[10]));
                    for (int c = 0; c < 8; c++) if (input.UV[c]) outUV[c].Add(new Vector4(d[11+c*4],d[12+c*4],d[13+c*4],d[14+c*4]));
                }
            }
            Mesh result = new Mesh(); result.name = "Magmarena doorway: " + source.name;
            result.indexFormat = positions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            result.SetVertices(positions); if (input.Normals) result.SetNormals(normals); if (input.Tangents) result.SetTangents(tangents);
            if (input.Colors) result.SetColors(outColors);
            for (int c = 0; c < 8; c++) if (input.UV[c]) result.SetUVs(c, outUV[c]);
            result.subMeshCount = parts.Length;
            for (int sub = 0; sub < parts.Length; sub++) result.SetTriangles(indices[sub], sub, false);
            result.RecalculateBounds(); return result;
        }
    }
}
