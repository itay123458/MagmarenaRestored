using System;
using MagmarenaPrivate;

internal static class TunnelTests
{
    public static void Run()
    {
        double[] center = TunnelCoordinates.Point(-57.483757, 7.750107, -21.669643);
        Near(center[0], -47.6); Near(center[1], 6.67); Near(center[2], -25.4);
        // The retained mouth is a vertical rectangle in the YZ plane. It must
        // meet the current Forest doorway in XY with an overlapping seam.
        double[] corner = TunnelCoordinates.Point(-57.483757, 6.220107, -22.969643);
        Near(corner[0], -48.9); Near(corner[1], 5.14); Near(corner[2], -25.4);
        double[] forward = TunnelCoordinates.Direction(-1, 0, 0);
        Near(forward[0], 0); Near(forward[1], 0); Near(forward[2], 1);
        double[] other = TunnelCoordinates.Point(-58.483757, 8.750107, -19.669643);
        Near(Math.Pow(other[0]-center[0],2)+Math.Pow(other[1]-center[1],2)+Math.Pow(other[2]-center[2],2), 6);
        if (!TunnelCoordinates.ForestHalf(-20) || TunnelCoordinates.ForestHalf(200))
            throw new Exception("Tunnel alignment would also move the Arena-side replica");
        if (!TunnelCoordinates.ForestBoundaryCasing(-50.3,7.2,-24.5) ||
            TunnelCoordinates.ForestBoundaryCasing(-56.4,7.6,-22.9) ||
            TunnelCoordinates.ForestBoundaryCasing(100,7.2,200))
            throw new Exception("Grounding mask would move the wrong boundary casing");
        Console.WriteLine("PASS: tunnel mouth alignment, orientation, rigid distances and replica separation");
    }

    private static void Near(double actual, double expected)
    {
        if (Math.Abs(actual-expected)>0.00001) throw new Exception("Tunnel coordinate mismatch: " + actual + " expected " + expected);
    }
}
