using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RelayTestApp
{
    /// <summary>
    /// Custom-rendered game viewport: draws splotches, shockwave rings, and cursor arrows.
    /// All positions are stored as normalized 0.0–1.0 coordinates (matching Java/JS/C++ apps)
    /// and scaled to pixel coordinates here at render time.
    /// </summary>
    public class ViewportControl : Control
    {
        private const double SplotchSize = 64.0; // rendered diameter (px), matches the other RTA clients

        // Shared splotch art (white alpha-mask), loaded once and used as an opacity mask
        // so it can be filled with each player's colour (opaque) and rotated to a synced angle.
        private static Bitmap _splatBitmap;
        private static bool   _splatLoadAttempted;

        private static Bitmap SplatBitmap()
        {
            if (!_splatLoadAttempted)
            {
                _splatLoadAttempted = true;
                try
                {
                    _splatBitmap = new Bitmap(AssetLoader.Open(new Uri("avares://RelayTestApp/assets/PaintSplatter1.png")));
                }
                catch { _splatBitmap = null; }
            }
            return _splatBitmap;
        }

        public override void Render(DrawingContext ctx)
        {
            // Background — dark grey matching Java/C++/JS versions
            ctx.FillRectangle(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), new Rect(Bounds.Size));

            if (State.lobby == null) return;

            double w = Bounds.Width;
            double h = Bounds.Height;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var  now   = DateTime.Now;

            // Splotches — persistent filled circles, optionally fading at end of life
            foreach (var sp in State.splotches)
            {
                double alpha = 1.0;
                if (State.splotchDurationSec > 0)
                {
                    long lifeMs  = State.splotchDurationSec * 1000L;
                    long age     = nowMs - sp.startTimeMs;
                    long fadeMs  = 3000L;
                    if (age >= lifeMs - fadeMs)
                        alpha = Math.Clamp(1.0 - (double)(age - (lifeMs - fadeMs)) / fadeMs, 0.0, 1.0);
                }

                byte a      = (byte)(255 * alpha);
                var  c      = CursorColor.COLORS[sp.colorIndex];
                var  brush  = new SolidColorBrush(new Color(a, c.R, c.G, c.B));
                double cx   = sp.pos.X * w;
                double cy   = sp.pos.Y * h;

                Bitmap splat = SplatBitmap();
                if (splat != null)
                {
                    // Opaque, player-coloured PaintSplatter1.png, rotated by the network-synced angle.
                    // rect is centred on the origin; rotate about the origin then translate to (cx,cy),
                    // so the splotch spins about its own centre.
                    var rect = new Rect(-SplotchSize / 2, -SplotchSize / 2, SplotchSize, SplotchSize);
                    var transform = Matrix.CreateRotation(sp.angle) * Matrix.CreateTranslation(cx, cy);

                    using (ctx.PushTransform(transform))
                    using (ctx.PushOpacityMask(new ImageBrush(splat) { Stretch = Stretch.Uniform }, rect))
                    {
                        ctx.FillRectangle(brush, rect);
                    }
                }
                else
                {
                    // Fallback if the splat image failed to load
                    ctx.DrawEllipse(brush, null, new Point(cx, cy), 8, 8);
                }
            }

            // Shockwaves — expanding rings that fade out over 1 second
            foreach (var sw in State.shockwaves)
            {
                double elapsed = (now - sw.startTime).TotalMilliseconds;
                float  t       = (float)Math.Clamp(elapsed / 600.0, 0.0, 1.0); // 0.6s, matches React/Java rings
                float  eased   = 1.0f - (1.0f - t) * (1.0f - t);               // ease-out
                double radius  = eased * 64.0;                                 // 128px diameter, matches other clients
                byte   ringA   = (byte)(255 * (1.0 - t));                      // fade out as it expands

                var c   = CursorColor.COLORS[sw.colorIndex];
                var pen = new Pen(new SolidColorBrush(new Color(ringA, c.R, c.G, c.B)), 2.0);
                ctx.DrawEllipse(null, pen, new Point(sw.pos.X * w, sw.pos.Y * h), radius, radius);
            }

            // Cursors — small filled arrowhead in each player's color
            foreach (var user in State.lobby.members)
            {
                if (!user.isAlive) continue;
                DrawCursor(ctx, new Point(user.pos.X * w, user.pos.Y * h), CursorColor.COLORS[user.colorIndex]);
            }
        }

        // Draws a small downward-pointing arrowhead at the given position.
        static void DrawCursor(DrawingContext ctx, Point pos, Color color)
        {
            var geometry = new StreamGeometry();
            using (var sg = geometry.Open())
            {
                sg.BeginFigure(pos, true);
                sg.LineTo(new Point(pos.X - 8, pos.Y + 14));
                sg.LineTo(new Point(pos.X,     pos.Y + 10));
                sg.LineTo(new Point(pos.X + 8, pos.Y + 14));
                sg.EndFigure(true);
            }
            ctx.DrawGeometry(
                new SolidColorBrush(color),
                new Pen(Brushes.White, 0.8),
                geometry);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (State.screenState == ScreenState.Game)
            {
                var raw = e.GetPosition(this);
                State.app.MouseMoved(new Point(raw.X / Bounds.Width, raw.Y / Bounds.Height));
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (State.screenState == ScreenState.Game)
            {
                var raw = e.GetPosition(this);
                State.app.Shockwave(new Point(raw.X / Bounds.Width, raw.Y / Bounds.Height));
            }
        }
    }
}
