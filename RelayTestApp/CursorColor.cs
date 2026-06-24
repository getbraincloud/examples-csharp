using Avalonia.Media;

namespace RelayTestApp
{
    static class CursorColor
    {
        // 40-color palette aligned with Java/C++/JS CursorParty implementations.
        // Row 0 (0-9):  Vivid saturated
        // Row 1 (10-19): Vivid-medium / complementary
        // Row 2 (20-29): Pastel / light
        // Row 3 (30-39): Medium-depth / muted
        public static Color[] COLORS =
        {
            // Row 0 — vivid
            Color.FromRgb(0xFF, 0x33, 0x33), // 0  vivid red
            Color.FromRgb(0xFF, 0x88, 0x00), // 1  vivid orange
            Color.FromRgb(0xFF, 0xD7, 0x00), // 2  gold
            Color.FromRgb(0x88, 0xFF, 0x00), // 3  vivid lime
            Color.FromRgb(0x00, 0xEE, 0x44), // 4  vivid green
            Color.FromRgb(0x00, 0xDD, 0xDD), // 5  vivid cyan
            Color.FromRgb(0x00, 0xAA, 0xFF), // 6  vivid sky blue
            Color.FromRgb(0x33, 0x55, 0xFF), // 7  vivid blue (default)
            Color.FromRgb(0xAA, 0x00, 0xFF), // 8  vivid purple
            Color.FromRgb(0xFF, 0x00, 0xBB), // 9  vivid magenta

            // Row 1 — vivid-medium
            Color.FromRgb(0xFF, 0x55, 0x66), // 10 coral
            Color.FromRgb(0xFF, 0xAA, 0x00), // 11 amber
            Color.FromRgb(0xAA, 0xDD, 0x00), // 12 yellow-green
            Color.FromRgb(0x00, 0xFF, 0x88), // 13 spring green
            Color.FromRgb(0x00, 0xFF, 0xCC), // 14 aqua
            Color.FromRgb(0x00, 0x88, 0xFF), // 15 azure
            Color.FromRgb(0x88, 0x33, 0xFF), // 16 violet
            Color.FromRgb(0xFF, 0x44, 0xAA), // 17 hot pink
            Color.FromRgb(0x77, 0xFF, 0x33), // 18 chartreuse
            Color.FromRgb(0xFF, 0x66, 0x88), // 19 rose

            // Row 2 — pastel
            Color.FromRgb(0xFF, 0x99, 0x99), // 20 light red
            Color.FromRgb(0xFF, 0xCC, 0x88), // 21 peach
            Color.FromRgb(0xFF, 0xFF, 0x88), // 22 pale yellow
            Color.FromRgb(0xAA, 0xFF, 0xAA), // 23 pale green
            Color.FromRgb(0x88, 0xFF, 0xEE), // 24 pale cyan
            Color.FromRgb(0xAA, 0xBB, 0xFF), // 25 periwinkle
            Color.FromRgb(0xDD, 0xBB, 0xFF), // 26 lavender
            Color.FromRgb(0xFF, 0xBB, 0xDD), // 27 light pink
            Color.FromRgb(0xCC, 0xFF, 0xDD), // 28 mint
            Color.FromRgb(0xFF, 0xEE, 0xCC), // 29 cream

            // Row 3 — muted
            Color.FromRgb(0xCC, 0x11, 0x33), // 30 crimson
            Color.FromRgb(0xCC, 0x55, 0x00), // 31 burnt orange
            Color.FromRgb(0x88, 0xAA, 0x00), // 32 olive
            Color.FromRgb(0x22, 0x88, 0x55), // 33 forest green
            Color.FromRgb(0x00, 0x99, 0x99), // 34 deep teal
            Color.FromRgb(0x33, 0x66, 0xAA), // 35 steel blue
            Color.FromRgb(0x77, 0x44, 0xCC), // 36 medium purple
            Color.FromRgb(0xAA, 0x33, 0x66), // 37 dark rose
            Color.FromRgb(0xAA, 0x66, 0x33), // 38 brown
            Color.FromRgb(0x77, 0x88, 0xAA), // 39 slate
        };
    }
}
