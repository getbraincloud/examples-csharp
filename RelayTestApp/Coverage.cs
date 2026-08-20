using System;
using System.Collections.Generic;
using System.Linq;

namespace RelayTestApp
{
    // Pure port of the cpp reference client's coverage.cpp rasterized-ownership-grid
    // algorithm (also ported to Java as-is): every splotch is stamped as a filled
    // circle onto a coarse grid, in paint order, last one wins; coverage% =
    // owned-cell-count * cellArea / canvasArea.
    static class Coverage
    {
        const float CanvasW = 800.0f;
        const float CanvasH = 600.0f;
        const float SplotchRadius = 32.0f; // matches the 64px rendered splotch diameter
        const float CellSize = 2.0f;

        public class CoverageEntry
        {
            public string cxId = "";
            public int colorIndex = 0;
            public float coveragePct = 0f;
            public int visibleCount = 0;
            public int rank = 0;
            public int beaten = 0;
        }

        // Splotch has no ownerCxId here either (never extended the wire format that
        // far — same limitation accepted in Java/react), so attribution is by
        // colorIndex only, matched against the first live member with that colour.
        public static List<CoverageEntry> Compute(List<Splotch> splotches, List<User> members)
        {
            int gridW = (int)Math.Ceiling(CanvasW / CellSize);
            int gridH = (int)Math.Ceiling(CanvasH / CellSize);
            int[] owner = new int[gridW * gridH];
            for (int i = 0; i < owner.Length; i++) owner[i] = -1;

            var result = new List<CoverageEntry>();
            foreach (var m in members)
            {
                result.Add(new CoverageEntry { cxId = m.cxId, colorIndex = m.colorIndex });
            }

            int cellRadius = (int)Math.Ceiling(SplotchRadius / CellSize);
            float r2 = SplotchRadius * SplotchRadius;

            foreach (var s in splotches)
            {
                int memberIndex = -1;
                for (int i = 0; i < members.Count; i++)
                {
                    if (members[i].colorIndex == s.colorIndex) { memberIndex = i; break; }
                }

                float px = (float)(s.pos.X * CanvasW);
                float py = (float)(s.pos.Y * CanvasH);
                int centerGX = (int)Math.Floor(px / CellSize);
                int centerGY = (int)Math.Floor(py / CellSize);

                int gxMin = Math.Max(0, centerGX - cellRadius);
                int gxMax = Math.Min(gridW - 1, centerGX + cellRadius);
                int gyMin = Math.Max(0, centerGY - cellRadius);
                int gyMax = Math.Min(gridH - 1, centerGY + cellRadius);

                for (int gy = gyMin; gy <= gyMax; gy++)
                {
                    float cy = (gy + 0.5f) * CellSize;
                    for (int gx = gxMin; gx <= gxMax; gx++)
                    {
                        float cx = (gx + 0.5f) * CellSize;
                        float dx = cx - px;
                        float dy = cy - py;
                        if (dx * dx + dy * dy <= r2)
                            owner[gy * gridW + gx] = memberIndex; // unconditional overwrite: paint order = last one wins
                    }
                }
            }

            for (int c = 0; c < owner.Length; c++)
            {
                if (owner[c] >= 0) result[owner[c]].visibleCount++;
            }

            float cellArea = CellSize * CellSize;
            float canvasArea = CanvasW * CanvasH;
            foreach (var e in result)
                e.coveragePct = Math.Min(100f, e.visibleCount * cellArea / canvasArea * 100f);

            result.Sort((a, b) =>
            {
                if (a.coveragePct != b.coveragePct) return b.coveragePct.CompareTo(a.coveragePct);
                if (a.visibleCount != b.visibleCount) return b.visibleCount.CompareTo(a.visibleCount);
                return string.CompareOrdinal(a.cxId, b.cxId);
            });

            int rank = 1;
            for (int i = 0; i < result.Count; i++)
            {
                if (i > 0)
                {
                    var prev = result[i - 1];
                    var cur = result[i];
                    if (cur.coveragePct != prev.coveragePct || cur.visibleCount != prev.visibleCount)
                        rank = i + 1;
                }
                result[i].rank = rank;
            }

            foreach (var e in result)
                e.beaten = result.Count(other => other.rank > e.rank);

            return result;
        }
    }
}
