namespace MagmarenaPrivate
{
    internal static class TunnelCoordinates
    {
        public static double[] Point(double x, double y, double z)
        {
            // Quarter-turn about the retained Forest mouth, then place it at
            // the back of the recovered doorway, overlapping its rim by 0.36m.
            return new[] { -47.6 + (z + 21.669643), 6.67 + (y - 7.750107), -25.4 - (x + 57.483757) };
        }

        public static double[] Direction(double x, double y, double z)
        {
            return new[] { z, y, -x };
        }

        public static bool ForestHalf(double z) { return z < 100.0; }

        public static bool ForestBoundaryCasing(double x, double y, double z)
        {
            // The native combiner separated this casing from its marker root.
            return x >= -50.485 && x <= -50.117 && y >= 7.129 && y <= 7.345 && z >= -24.738 && z <= -24.228;
        }
    }
}
