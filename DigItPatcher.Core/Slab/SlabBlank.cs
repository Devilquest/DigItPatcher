using DigItPatcher.Core.Formats;

namespace DigItPatcher.Core.Slab;

/// <summary>Derives a blank third slab for the Credits screen out of its own second slab and a donor
/// screen, erasing every trace of the game's own artwork and closing the gaps with its own stone.</summary>
internal static class SlabBlank
{
    private const int Width = FrameCodec.Width;

    private const int Step = 150;
    private const int Shift = 150; // (destination stop 2 - source stop 1) * Step

    private const int SrcTop = 170, SrcBot = 313;

    private static readonly (int Left, int Right)[] RopeCols = [(76, 83), (244, 251)];

    private const int HoleTop = 285, HoleBot = 320, HoleHalf = 22;

    private const int InkLum = 80, InkSat = 25;
    private const int EdgeKeep = 4;
    private const int PatchPad = 2;
    private const int MinBlob = 8;
    private const int Jitter = 3;

    private const int TopFixShift = 301;
    private const int TopFixRowFrom = 196, TopFixRowTo = 202;
    private const int TopFixColFrom = 110, TopFixColTo = 201;

    private static readonly (int X0, int Y0, int X1, int Y1) BodyFix = (131, 72, 188, 146);
    private const int BodyFixShift = 450;

    private static readonly (int X0, int Y0, int X1, int Y1)[] HandMarkBoxes =
        [(151, 46, 157, 53), (159, 48, 168, 59)];
    private const int WindowOffset = 300; // destination stop 2 * Step

    private const int KnotPad = 2;
    private static readonly HashSet<byte> KnotIndices = [.. Enumerable.Range(162, 6).Select(i => (byte)i)];

    private const int KeepHalf = 45;
    private const int KeepTop = 178, KeepBot = 233;

    private static readonly (int Dx, int Dy)[] Orthogonal = [(-1, 0), (1, 0), (0, -1), (0, 1)];

    /// <summary>Derives a 320x1600 strip with the third slab blank.</summary>
    /// <param name="art">The target screen's foreground plane, decoded whole.</param>
    /// <param name="donor">The donor screen's foreground plane, decoded whole, same shape as <paramref name="art"/>.</param>
    /// <param name="palette">The palette both planes are expressed in.</param>
    public static byte[] Derive(byte[] art, byte[] donor, (byte R, byte G, byte B)[] palette)
    {
        var pal = palette;
        var outArt = (byte[])art.Clone();
        int top = SrcTop + Shift, bottom = SrcBot + Shift;

        // 1. the copy. A copy that already carries a slab of ours has holes already punched into slab 2's
        // rows, so from HoleTop down the columns the holes occupy are read back from the slab this tool put
        // there last time instead of from slab 2's now-holed stone.
        bool done = false;
        for (int y = top; y < bottom && !done; y++)
            for (int x = 0; x < Width; x++)
                if (outArt[(y * Width) + x] != 0) { done = true; break; }

        var holed = HoleColumns();
        for (int y = SrcTop; y < SrcBot; y++)
        {
            bool recover = done && y >= HoleTop;
            for (int x = 0; x < Width; x++)
            {
                int source = recover && holed.Contains(x) ? y + Shift : y;
                outArt[((y + Shift) * Width) + x] = art[(source * Width) + x];
            }
        }

        // 2. the tips of the heading the erasure cannot reach
        for (int y = TopFixRowFrom; y < TopFixRowTo; y++)
            for (int x = TopFixColFrom; x < TopFixColTo; x++)
                outArt[((y + Shift) * Width) + x] = donor[((y + TopFixShift) * Width) + x];

        // 3. the holes, so slab 2 can carry a rope
        for (int y = HoleTop; y < HoleBot; y++)
            foreach (int x in holed)
                outArt[(y * Width) + x] = donor[(y * Width) + x];

        // 4. the erasure
        var field = new HashSet<(int X, int Y)>();
        var lit = new HashSet<(int X, int Y)>();
        for (int y = top; y < bottom; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                var p = (x, y);
                field.Add(p);
                if (outArt[(y * Width) + x] != 0) lit.Add(p);
            }
        }

        var inside = new HashSet<(int X, int Y)>(lit);
        inside.UnionWith(Enclosed(lit, field));

        var depths = Depths(inside);
        var keep = Grow(Knots(outArt, top, bottom), KnotPad, inside);
        keep.UnionWith(HoleBlocks(top));

        var (_, patch) = FindInk(outArt, pal, inside, depths, keep);

        var marked = new HashSet<(int X, int Y)>(HandMarks());
        marked.RemoveWhere(p => !depths.TryGetValue(p, out var distance) || distance < EdgeKeep);
        patch.UnionWith(marked);

        // 5. the middle of the slab, from a slab with nothing written across it, found in after the ink so
        // the eraser sees the whole word rather than the fringe the block would otherwise leave behind
        var block = BodyBlock();
        foreach (var (x, y) in block)
            outArt[(y * Width) + x] = donor[((y + BodyFixShift) * Width) + x];
        patch.ExceptWith(block);

        ClosePatch(outArt, patch, inside, depths, keep, top, bottom);

        return outArt;
    }

    private static HashSet<int> HoleColumns()
    {
        var cols = new HashSet<int>();
        foreach (var (left, right) in RopeCols)
        {
            int middle = (left + right - 1) / 2;
            for (int x = Math.Max(0, middle - HoleHalf); x < Math.Min(Width, middle + HoleHalf + 1); x++)
                cols.Add(x);
        }

        return cols;
    }

    private static HashSet<(int X, int Y)> BodyBlock()
    {
        var (x0, y0, x1, y1) = BodyFix;
        var block = new HashSet<(int, int)>();
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                block.Add((x, y + WindowOffset));
        return block;
    }

    private static HashSet<(int X, int Y)> HandMarks()
    {
        var marks = new HashSet<(int, int)>();
        foreach (var (x0, y0, x1, y1) in HandMarkBoxes)
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    marks.Add((x, y + WindowOffset));
        return marks;
    }

    private static HashSet<(int X, int Y)> HoleBlocks(int top)
    {
        var blocks = new HashSet<(int, int)>();
        int offset = top - SrcTop;
        foreach (var (left, right) in RopeCols)
        {
            int middle = (left + right - 1) / 2;
            for (int y = KeepTop + offset; y < KeepBot + offset; y++)
                for (int x = middle - KeepHalf; x < middle + KeepHalf + 1; x++)
                    if (x >= 0 && x < Width) blocks.Add((x, y));
        }

        return blocks;
    }

    /// <summary>Splits a set of pixels into 8-connected touching groups.</summary>
    private static List<List<(int X, int Y)>> Blobs(HashSet<(int X, int Y)> cells)
    {
        var seen = new HashSet<(int, int)>();
        var groups = new List<List<(int, int)>>();
        foreach (var start in cells)
        {
            if (seen.Contains(start)) continue;
            seen.Add(start);
            var stack = new Stack<(int, int)>();
            stack.Push(start);
            var group = new List<(int, int)> { start };
            while (stack.Count > 0)
            {
                var (x, y) = stack.Pop();
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        var p = (x + dx, y + dy);
                        if (cells.Contains(p) && !seen.Contains(p))
                        {
                            seen.Add(p);
                            stack.Push(p);
                            group.Add(p);
                        }
                    }
                }
            }

            groups.Add(group);
        }

        return groups;
    }

    private static HashSet<(int X, int Y)> Grow(HashSet<(int X, int Y)> cells, int radius, HashSet<(int X, int Y)> limit)
    {
        var outSet = new HashSet<(int, int)>(cells);
        var edge = new HashSet<(int, int)>(cells);
        for (int i = 0; i < radius; i++)
        {
            var step = new HashSet<(int, int)>();
            foreach (var (x, y) in edge)
            {
                foreach (var (dx, dy) in Orthogonal)
                {
                    var p = (x + dx, y + dy);
                    if (limit.Contains(p) && outSet.Add(p)) step.Add(p);
                }
            }

            edge = step;
        }

        return outSet;
    }

    /// <summary>Rings of a region, from its boundary inward: distance from the nearest pixel outside it.</summary>
    private static Dictionary<(int X, int Y), int> Depths(HashSet<(int X, int Y)> inside)
    {
        var edge = new List<(int, int)>();
        foreach (var p in inside)
            if (Orthogonal.Any(d => !inside.Contains((p.X + d.Dx, p.Y + d.Dy))))
                edge.Add(p);

        var outMap = new Dictionary<(int, int), int>();
        foreach (var p in edge) outMap[p] = 0;

        int distance = 0;
        while (edge.Count > 0)
        {
            distance++;
            var step = new List<(int, int)>();
            foreach (var (x, y) in edge)
            {
                foreach (var (dx, dy) in Orthogonal)
                {
                    var p = (x + dx, y + dy);
                    if (inside.Contains(p) && !outMap.ContainsKey(p))
                    {
                        outMap[p] = distance;
                        step.Add(p);
                    }
                }
            }

            edge = step;
        }

        return outMap;
    }

    /// <summary>Whatever <paramref name="patch"/> closes a ring around within <paramref name="inside"/>.</summary>
    private static HashSet<(int X, int Y)> Enclosed(HashSet<(int X, int Y)> patch, HashSet<(int X, int Y)> inside)
    {
        var reached = new HashSet<(int, int)>();
        foreach (var p in inside)
            if (!patch.Contains(p) && Orthogonal.Any(d => !inside.Contains((p.X + d.Dx, p.Y + d.Dy))))
                reached.Add(p);

        var stack = new Stack<(int, int)>(reached);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            foreach (var (dx, dy) in Orthogonal)
            {
                var p = (x + dx, y + dy);
                if (inside.Contains(p) && !patch.Contains(p) && reached.Add(p)) stack.Push(p);
            }
        }

        var result = new HashSet<(int, int)>(inside);
        result.ExceptWith(patch);
        result.ExceptWith(reached);
        return result;
    }

    /// <summary>Flood fill from the rope columns through the brown ramp, to find the knots without the
    /// headings that share the same palette indices.</summary>
    private static HashSet<(int X, int Y)> Knots(byte[] art, int top, int bottom)
    {
        var seen = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        for (int y = top; y < bottom; y++)
        {
            foreach (var (left, right) in RopeCols)
            {
                for (int x = left; x < right; x++)
                {
                    if (KnotIndices.Contains(art[(y * Width) + x]) && seen.Add((x, y)))
                        stack.Push((x, y));
                }
            }
        }

        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var (px, py) = (x + dx, y + dy);
                    if (px >= 0 && px < Width && py >= top && py < bottom
                        && !seen.Contains((px, py)) && KnotIndices.Contains(art[(py * Width) + px]))
                    {
                        seen.Add((px, py));
                        stack.Push((px, py));
                    }
                }
            }
        }

        return seen;
    }

    /// <summary>The pixels of text and drawing on the stone, and the region to replace.</summary>
    private static (HashSet<(int X, int Y)> Ink, HashSet<(int X, int Y)> Patch) FindInk(
        byte[] art, (byte R, byte G, byte B)[] pal, HashSet<(int X, int Y)> inside,
        Dictionary<(int X, int Y), int> depths, HashSet<(int X, int Y)> keep)
    {
        var ink = new HashSet<(int, int)>();
        foreach (var ((x, y), distance) in depths)
        {
            if (distance < EdgeKeep || keep.Contains((x, y))) continue;
            byte value = art[(y * Width) + x];
            if (value == 0)
            {
                ink.Add((x, y));
                continue;
            }

            var (r, g, b) = pal[value];
            double lum = (r + g + b) / 3.0;
            int sat = Math.Max(r, Math.Max(g, b)) - Math.Min(r, Math.Min(g, b));
            if (lum < InkLum || sat > InkSat) ink.Add((x, y));
        }

        var blobbedInk = new HashSet<(int, int)>();
        foreach (var group in Blobs(ink))
            if (group.Count >= MinBlob)
                foreach (var p in group)
                    blobbedInk.Add(p);

        var patch = Grow(blobbedInk, PatchPad, inside);
        patch.UnionWith(Enclosed(patch, inside));
        patch.ExceptWith(keep);
        return (blobbedInk, patch);
    }

    private static long Drift(int x, int y)
    {
        long h = ((long)x * 73856093) ^ ((long)y * 19349663);
        return (h % ((2 * Jitter) + 1)) - Jitter;
    }

    /// <summary>The stone rows of one column in one direction, nearest first, ink and held pixels skipped.</summary>
    private static List<int> CleanRun(int x, int start, int step, int top, int bottom,
        HashSet<(int X, int Y)> inside, HashSet<(int X, int Y)> taken, Dictionary<(int X, int Y), int> depths)
    {
        var rows = new List<int>();
        for (int y = start; top <= y && y < bottom; y += step)
        {
            var p = (x, y);
            if (inside.Contains(p) && !taken.Contains(p) && depths.GetValueOrDefault(p, 0) >= EdgeKeep)
                rows.Add(y);
        }

        return rows;
    }

    /// <summary>Fills every patched pixel with the stone that surrounds it, mirrored inward column by column.</summary>
    private static void ClosePatch(byte[] art, HashSet<(int X, int Y)> patch, HashSet<(int X, int Y)> inside,
        Dictionary<(int X, int Y), int> depths, HashSet<(int X, int Y)> keep, int top, int bottom)
    {
        var taken = new HashSet<(int, int)>(patch);
        taken.UnionWith(keep);

        var columns = new Dictionary<int, List<int>>();
        foreach (var (x, y) in patch)
        {
            if (!columns.TryGetValue(x, out var rows)) columns[x] = rows = [];
            rows.Add(y);
        }

        foreach (var (x, rows) in columns)
        {
            rows.Sort();
            var runs = new List<(int First, int Last)>();
            int runStart = rows[0], prev = rows[0];
            for (int i = 1; i < rows.Count; i++)
            {
                int y = rows[i];
                if (y != prev + 1)
                {
                    runs.Add((runStart, prev));
                    runStart = y;
                }

                prev = y;
            }

            runs.Add((runStart, prev));

            foreach (var (first, last) in runs)
            {
                var up = CleanRun(x, first - 1, -1, top, bottom, inside, taken, depths);
                var down = CleanRun(x, last + 1, 1, top, bottom, inside, taken, depths);
                for (int y = first; y <= last; y++)
                {
                    var (near, far) = y - first <= last - y ? (up, down) : (down, up);
                    int steps = Math.Min(y - first, last - y);
                    int? source = steps < near.Count ? near[steps] : (steps < far.Count ? far[steps] : null);
                    if (source is null) continue;

                    int column = x + (int)Drift(x, y);
                    if (!inside.Contains((column, source.Value)) || taken.Contains((column, source.Value))
                        || depths.GetValueOrDefault((column, source.Value), 0) < EdgeKeep)
                        column = x;

                    art[(y * Width) + x] = art[(source.Value * Width) + column];
                }
            }
        }
    }
}
