using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagmarenaPrivate
{
    internal sealed class ForestLightmap
    {
        private struct Triangle
        {
            public SurfacePoint A,B,C;
            public Vector2 U,V,W;
        }
        private readonly List<Triangle> triangles = new List<Triangle>();
        private int map = -1;
        private static SurfacePoint Point(Vector3 p) { return new SurfacePoint(p.x,p.y,p.z); }

        public void Add(Mesh mesh,Transform transform,Renderer renderer,Bounds region)
        {
            if (renderer.lightmapIndex<0 || (map>=0 && renderer.lightmapIndex!=map)) return;
            Vector3[] vertices=mesh.vertices;
            Vector2[] uv=mesh.uv2;
            if (uv.Length!=vertices.Length) return;
            Vector4 st=renderer.isPartOfStaticBatch ? new Vector4(1,1,0,0) : renderer.lightmapScaleOffset;
            for (int i=0;i<vertices.Length;i++)
            { vertices[i]=transform.TransformPoint(vertices[i]);uv[i]=new Vector2(uv[i].x*st.x+st.z,uv[i].y*st.y+st.w); }
            Vector3 low=region.min,high=region.max;
            for (int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int[] indices=mesh.GetTriangles(sub);
                for (int i=0;i<indices.Length;i+=3)
                {
                    int a=indices[i],b=indices[i+1],c=indices[i+2];
                    Vector3 min=Vector3.Min(vertices[a],Vector3.Min(vertices[b],vertices[c]));
                    Vector3 max=Vector3.Max(vertices[a],Vector3.Max(vertices[b],vertices[c]));
                    if (min.x>high.x || min.y>high.y || min.z>high.z || max.x<low.x || max.y<low.y || max.z<low.z) continue;
                    if (Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude<0.00000001f) continue;
                    triangles.Add(new Triangle { A=Point(vertices[a]),B=Point(vertices[b]),C=Point(vertices[c]),U=uv[a],V=uv[b],W=uv[c] });
                    map=renderer.lightmapIndex;
                }
            }
        }

        public int Apply(Mesh mesh,Transform transform)
        {
            if (map<0 || triangles.Count==0) throw new InvalidOperationException("Forest doorway lighting reference is missing");
            Vector3[] vertices=mesh.vertices;
            Vector2[] uv=new Vector2[vertices.Length];
            bool[] written=new bool[vertices.Length];
            for (int i=0;i<vertices.Length;i++) vertices[i]=transform.TransformPoint(vertices[i]);
            for (int sub=0;sub<mesh.subMeshCount;sub++)
            {
                int[] indices=mesh.GetTriangles(sub);
                for (int i=0;i<indices.Length;i+=3)
                {
                    SurfacePoint center=Point((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3f);
                    double best=double.PositiveInfinity;
                    Triangle reference=new Triangle();
                    foreach (Triangle triangle in triangles)
                    {
                        SurfacePoint weights;
                        SurfacePoint closest=LightmapProjection.Closest(center,triangle.A,triangle.B,triangle.C,out weights);
                        SurfacePoint difference=closest-center;
                        double distance=SurfacePoint.Dot(difference,difference);
                        if (distance>=best) continue;
                        best=distance; reference=triangle;
                    }
                    // MeshRegion emits unshared vertices. Keep each output UV
                    // triangle inside one source UV triangle, so interpolation
                    // cannot cross unrelated packed lightmap islands.
                    for (int j=0;j<3;j++)
                    {
                        int index=indices[i+j];
                        if (written[index]) throw new InvalidOperationException("Doorway lighting requires unshared triangle vertices");
                        written[index]=true;
                        SurfacePoint weights;
                        LightmapProjection.Closest(Point(vertices[index]),reference.A,reference.B,reference.C,out weights);
                        uv[index]=reference.U*(float)weights.X+reference.V*(float)weights.Y+reference.W*(float)weights.Z;
                    }
                }
            }
            mesh.uv2=uv;
            return map;
        }
    }
}
