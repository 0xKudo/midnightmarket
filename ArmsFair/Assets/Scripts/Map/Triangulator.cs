using System.Collections.Generic;
using UnityEngine;

namespace ArmsFair.Map
{
    // Ear-clipping triangulator for simple (non-self-intersecting) polygons.
    public class Triangulator
    {
        private readonly Vector2[] _points;

        public Triangulator(Vector2[] points) => _points = points;

        public int[] Triangulate()
        {
            var indices = new List<int>();
            int n = _points.Length;
            if (n < 3) return indices.ToArray();

            var remaining = new List<int>(n);
            for (int i = 0; i < n; i++) remaining.Add(i);

            // Ensure counter-clockwise winding
            if (SignedArea(_points) < 0)
                remaining.Reverse();

            int tries = 0;
            int count = remaining.Count;

            while (count > 3 && tries < count * count)
            {
                bool clipped = false;
                for (int i = 0; i < count; i++)
                {
                    int a = remaining[(i - 1 + count) % count];
                    int b = remaining[i];
                    int c = remaining[(i + 1) % count];

                    if (!IsEar(a, b, c, remaining)) continue;

                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(c);
                    remaining.RemoveAt(i);
                    count--;
                    clipped = true;
                    break;
                }
                if (!clipped) tries++;
            }

            if (count == 3)
            {
                indices.Add(remaining[0]);
                indices.Add(remaining[1]);
                indices.Add(remaining[2]);
            }

            return indices.ToArray();
        }

        private bool IsEar(int a, int b, int c, List<int> remaining)
        {
            var pa = _points[a];
            var pb = _points[b];
            var pc = _points[c];

            if (Cross(pa, pb, pc) <= 0) return false;

            foreach (int idx in remaining)
            {
                if (idx == a || idx == b || idx == c) continue;
                if (PointInTriangle(_points[idx], pa, pb, pc)) return false;
            }
            return true;
        }

        private static float Cross(Vector2 o, Vector2 a, Vector2 b)
            => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            => Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0;

        private static float SignedArea(Vector2[] pts)
        {
            float area = 0;
            int n = pts.Length;
            for (int i = 0; i < n; i++)
            {
                var curr = pts[i];
                var next = pts[(i + 1) % n];
                area += curr.x * next.y - next.x * curr.y;
            }
            return area * 0.5f;
        }
    }
}
