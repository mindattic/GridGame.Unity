using System;
using GridGame.Core.Content;

namespace GridGame.Core.World
{
    /// <summary>A region's fog-of-war reveal mask (BIBLE 28.7, GG-T1687): a low-res bit grid over the region's art pixels.
    /// A cell is revealed once its centre falls inside a revealed circle or band; reveals only ever add, so they are
    /// idempotent, and everything is clamped to the region.</summary>
    public sealed class FogMask
    {
        public const int DefaultCellSize = 16;

        public readonly int Width, Height, CellSize, Cols, Rows;
        private readonly byte[] bits;

        public FogMask(int width, int height, int cellSize = DefaultCellSize)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width and Height must be positive");
            if (cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
            Width = width; Height = height; CellSize = cellSize;
            Cols = (width + cellSize - 1) / cellSize;
            Rows = (height + cellSize - 1) / cellSize;
            bits = new byte[(Cols * Rows + 7) / 8];
        }

        public FogMask(RegionDef region, int cellSize = DefaultCellSize) : this(region.Width, region.Height, cellSize) { }

        public int RevealedCells
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cols * Rows; i++) if (Get(i)) n++;
                return n;
            }
        }

        public float RevealedFraction => (float)RevealedCells / (Cols * Rows);

        public bool IsRevealed(float x, float y)
        {
            if (x < 0 || y < 0 || x > Width || y > Height) return false;
            return Get(Index(CellX(x), CellY(y)));
        }

        public bool IsVisible(NodeDef node) => node != null && IsRevealed(node.X, node.Y);

        public void RevealCircle(float x, float y, float radius)
        {
            if (radius < 0) return;
            RevealWithin(x, y, x, y, radius);
        }

        /// <summary>Reveals every cell whose centre is within halfWidth of the polyline; a single point is a circle.</summary>
        public void RevealBand(WorldPoint[] points, float halfWidth)
        {
            if (points == null || points.Length == 0 || halfWidth < 0) return;
            if (points.Length == 1) { RevealCircle(points[0].X, points[0].Y, halfWidth); return; }
            for (int i = 0; i < points.Length - 1; i++)
                RevealWithin(points[i].X, points[i].Y, points[i + 1].X, points[i + 1].Y, halfWidth);
        }

        /// <summary>The mask as base64 of its packed bits (row-major, least significant bit first); the dimensions come
        /// from the region when it is read back.</summary>
        public string ToBase64() => Convert.ToBase64String(bits);

        public static FogMask FromBase64(int width, int height, string data, int cellSize = DefaultCellSize)
        {
            var mask = new FogMask(width, height, cellSize);
            if (string.IsNullOrEmpty(data)) return mask;
            byte[] raw;
            try { raw = Convert.FromBase64String(data); }
            catch (FormatException) { return mask; }
            Array.Copy(raw, mask.bits, Math.Min(raw.Length, mask.bits.Length));
            int used = mask.Cols * mask.Rows;
            if (used % 8 != 0) mask.bits[mask.bits.Length - 1] &= (byte)((1 << (used % 8)) - 1);
            return mask;
        }

        private void RevealWithin(float ax, float ay, float bx, float by, float r)
        {
            int c0 = CellX(Math.Min(ax, bx) - r), c1 = CellX(Math.Max(ax, bx) + r);
            int r0 = CellY(Math.Min(ay, by) - r), r1 = CellY(Math.Max(ay, by) + r);
            float dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy, r2 = r * r;
            for (int cy = r0; cy <= r1; cy++)
                for (int cx = c0; cx <= c1; cx++)
                {
                    float px = (cx + 0.5f) * CellSize, py = (cy + 0.5f) * CellSize;
                    float t = len2 > 0 ? Math.Max(0f, Math.Min(1f, ((px - ax) * dx + (py - ay) * dy) / len2)) : 0f;
                    float ex = px - (ax + t * dx), ey = py - (ay + t * dy);
                    if (ex * ex + ey * ey <= r2) Set(Index(cx, cy));
                }
            if (len2 == 0 && ax >= 0 && ay >= 0 && ax <= Width && ay <= Height) Set(Index(CellX(ax), CellY(ay)));
        }

        private int CellX(float x) => Math.Max(0, Math.Min(Cols - 1, (int)Math.Floor(x / CellSize)));
        private int CellY(float y) => Math.Max(0, Math.Min(Rows - 1, (int)Math.Floor(y / CellSize)));
        private int Index(int cx, int cy) => cy * Cols + cx;
        private bool Get(int i) => (bits[i >> 3] & (1 << (i & 7))) != 0;
        private void Set(int i) => bits[i >> 3] |= (byte)(1 << (i & 7));
    }
}
