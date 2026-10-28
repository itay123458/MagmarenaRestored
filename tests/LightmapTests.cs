using System;
using MagmarenaPrivate;

internal static class LightmapTests
{
    public static void Run()
    {
        SurfacePoint a=new SurfacePoint(0,0,0),b=new SurfacePoint(2,0,0),c=new SurfacePoint(0,2,0),w;
        SurfacePoint closest=LightmapProjection.Closest(new SurfacePoint(0.5,0.5,3),a,b,c,out w);
        Near(closest.X,.5);Near(closest.Y,.5);Near(closest.Z,0);
        Near(w.X,.5);Near(w.Y,.25);Near(w.Z,.25);
        closest=LightmapProjection.Closest(new SurfacePoint(3,3,2),a,b,c,out w);
        Near(closest.X,1);Near(closest.Y,1);Near(w.X,0);Near(w.Y,.5);Near(w.Z,.5);
        closest=LightmapProjection.Closest(new SurfacePoint(-2,-1,0),a,b,c,out w);
        Near(closest.X,0);Near(closest.Y,0);Near(w.X,1);Near(w.Y,0);Near(w.Z,0);
        Console.WriteLine("PASS: continuous lightmap projection onto triangle interiors, edges and vertices");
    }
    private static void Near(double a,double b) { if (Math.Abs(a-b)>.000001) throw new Exception("Lightmap projection mismatch"); }
}
