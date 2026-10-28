using System;
using System.Collections.Generic;

namespace MagmarenaPrivate
{
    internal sealed class ClipVertex
    {
        public readonly double X, Y, Z;
        public readonly float[] Data;
        public ClipVertex(double x, double y, double z, float[] data) { X = x; Y = y; Z = z; Data = data; }
        public double Axis(int axis) { return axis == 0 ? X : axis == 1 ? Y : Z; }
        public static ClipVertex Lerp(ClipVertex a, ClipVertex b, double t)
        {
            float[] data = new float[a.Data.Length];
            for (int i = 0; i < data.Length; i++) data[i] = (float)(a.Data[i] + (b.Data[i] - a.Data[i]) * t);
            return new ClipVertex(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t, data);
        }
    }

    internal static class TriangleClipper
    {
        public static List<ClipVertex[]> Clip(ClipVertex a, ClipVertex b, ClipVertex c, double[] min, double[] max, bool keepInside)
        {
            List<ClipVertex[]> result = new List<ClipVertex[]>();
            bool allInside = true;
            for (int axis = 0; axis < 3; axis++)
            {
                double low = Math.Min(a.Axis(axis), Math.Min(b.Axis(axis), c.Axis(axis)));
                double high = Math.Max(a.Axis(axis), Math.Max(b.Axis(axis), c.Axis(axis)));
                if (high < min[axis] || low > max[axis])
                {
                    if (!keepInside) result.Add(new[] { a, b, c });
                    return result;
                }
                if (low < min[axis] || high > max[axis]) allInside = false;
            }
            if (allInside) { if (keepInside) result.Add(new[] { a, b, c }); return result; }
            List<ClipVertex> remaining = new List<ClipVertex> { a, b, c };
            for (int axis = 0; axis < 3 && remaining.Count >= 3; axis++)
            {
                for (int side = 0; side < 2 && remaining.Count >= 3; side++)
                {
                    double limit = side == 0 ? min[axis] : max[axis];
                    double sign = side == 0 ? 1 : -1;
                    if (!keepInside) AddTriangles(result, Half(remaining, axis, limit, sign, false));
                    remaining = Half(remaining, axis, limit, sign, true);
                }
            }
            if (keepInside) AddTriangles(result, remaining);
            return result;
        }

        private static List<ClipVertex> Half(List<ClipVertex> polygon, int axis, double limit, double sign, bool inside)
        {
            List<ClipVertex> result = new List<ClipVertex>();
            if (polygon.Count == 0) return result;
            ClipVertex previous = polygon[polygon.Count - 1];
            double previousDistance = (previous.Axis(axis) - limit) * sign;
            bool previousKeep = inside ? previousDistance >= -1e-9 : previousDistance < -1e-9;
            foreach (ClipVertex current in polygon)
            {
                double distance = (current.Axis(axis) - limit) * sign;
                bool keep = inside ? distance >= -1e-9 : distance < -1e-9;
                if (keep != previousKeep)
                {
                    double t = previousDistance / (previousDistance - distance);
                    result.Add(ClipVertex.Lerp(previous, current, t));
                }
                if (keep) result.Add(current);
                previous = current; previousDistance = distance; previousKeep = keep;
            }
            return result;
        }

        private static void AddTriangles(List<ClipVertex[]> target, List<ClipVertex> polygon)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                ClipVertex a = polygon[0], b = polygon[i], c = polygon[i + 1];
                double x1 = b.X - a.X, y1 = b.Y - a.Y, z1 = b.Z - a.Z;
                double x2 = c.X - a.X, y2 = c.Y - a.Y, z2 = c.Z - a.Z;
                double x = y1 * z2 - z1 * y2, y = z1 * x2 - x1 * z2, z = x1 * y2 - y1 * x2;
                if (x * x + y * y + z * z > 1e-20) target.Add(new[] { a, b, c });
            }
        }
    }
}
