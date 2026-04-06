using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace RelayTestApp
{
    /// <summary>
    /// Custom-rendered game viewport: draws splotches, shockwave rings, and cursor arrows.
    /// All positions are stored as normalized 0.0–1.0 coordinates (matching Java/JS/C++ apps)
    /// and scaled to pixel coordinates here at render time.
    /// </summary>
    public class ViewportControl : Control
    {
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
                ctx.DrawEllipse(brush, null, new Point(sp.pos.X * w, sp.pos.Y * h), 8, 8);
            }

            // Shockwaves — expanding rings that fade out over 1 second
            foreach (var sw in State.shockwaves)
            {
                double elapsed = (now - sw.startTime).TotalMilliseconds;
                float  t       = (float)(elapsed / 1000.0);
                t = 1.0f - (1.0f - t) * (1.0f - t); // ease-out
                double radius  = t * 32.0;

                var pen = new Pen(new SolidColorBrush(CursorColor.COLORS[sw.colorIndex]), 1.5);
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
