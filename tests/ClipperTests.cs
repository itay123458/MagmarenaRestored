using System;
using System.Collections.Generic;
using MagmarenaPrivate;

internal static class ClipperTests
{
    private static ClipVertex V(double x, double y) { return new ClipVertex(x, y, 0, new[] { (float)x, (float)y }); }
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static double Area(List<ClipVertex[]> triangles)
    {
        double sum = 0;
        foreach (ClipVertex[] t in triangles)
            sum += Math.Abs((t[1].X - t[0].X) * (t[2].Y - t[0].Y) - (t[1].Y - t[0].Y) * (t[2].X - t[0].X)) * 0.5;
        return sum;
    }
    public static void Run()
    {
        double[] min = { 0.5, 0.5, -1 }, max = { 1.0, 1.0, 1 };
        ClipVertex a = V(0, 0), b = V(2, 0), c = V(0, 2);
        List<ClipVertex[]> outside = TriangleClipper.Clip(a, b, c, min, max, false);
        List<ClipVertex[]> inside = TriangleClipper.Clip(a, b, c, min, max, true);
        Assert(Math.Abs(Area(outside) - 1.75) < 1e-8, "Door cut removed geometry outside its box or left the sealed cap");
        Assert(Math.Abs(Area(inside) - 0.25) < 1e-8, "Original doorway patch was not clipped to the exact region");
        foreach (ClipVertex[] triangle in inside)
            foreach (ClipVertex v in triangle)
            {
                Assert(v.X >= 0.5 - 1e-8 && v.X <= 1 + 1e-8 && v.Y >= 0.5 - 1e-8 && v.Y <= 1 + 1e-8, "Patch escaped its bounds");
                Assert(Math.Abs(v.Data[0] - v.X) < 1e-6 && Math.Abs(v.Data[1] - v.Y) < 1e-6, "Clipping corrupted interpolated UV/vertex attributes");
            }
        Assert(Area(TriangleClipper.Clip(V(3, 3), V(4, 3), V(3, 4), min, max, false)) == 0.5, "Unrelated wall triangle changed");
        Assert(TriangleClipper.Clip(V(3, 3), V(4, 3), V(3, 4), min, max, true).Count == 0, "Unrelated geometry entered the doorway patch");
        Console.WriteLine("PASS: localized doorway clipping, outside-area preservation, original-patch bounds, interpolated attributes");
    }
}
