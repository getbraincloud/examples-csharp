using Avalonia;

namespace RelayTestApp
{
    class Splotch
    {
        public Point pos;
        public int colorIndex;
        public double angle;   // network-synced rotation (radians) so all clients match
        public long startTimeMs;
    }
}
