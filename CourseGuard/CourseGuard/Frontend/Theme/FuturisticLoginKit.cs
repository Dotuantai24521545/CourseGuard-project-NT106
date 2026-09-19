using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace CourseGuard.Frontend.Theme
{
    internal static class FuturisticLoginKit
    {
        public static Font CreateUiFont(float size, FontStyle style = FontStyle.Regular)
        {
            string[] preferred = { "Inter", "Poppins", "Segoe UI Variable", "Segoe UI" };
            foreach (var name in preferred)
            {
                try
                {
                    using var test = new Font(name, size, style);
                    if (string.Equals(test.Name, name, StringComparison.OrdinalIgnoreCase))
                        return new Font(name, size, style);
                }
                catch { /* ignore */ }
            }
            var fallbackFont = SystemFonts.MessageBoxFont;
            return new Font(fallbackFont?.FontFamily ?? FontFamily.GenericSansSerif, size, style);
        }

        public static GraphicsPath CreateRoundedRect(RectangleF rect, float radius)
        {
            float d = radius * 2f;
            var path = new GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(Math.Clamp(alpha, 0, 255), c);
    }

    internal sealed class AnimatedFuturisticBackgroundPanel : Panel
    {
        private readonly System.Windows.Forms.Timer _timer;
        private readonly List<Particle> _particles = new();
        private readonly Random _rng = new();
        private float _t;
        private Point _mouse;
        private Image? _bgImage;

        public AnimatedFuturisticBackgroundPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Dock = DockStyle.Fill;

            LoadBackgroundImage();

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, _) =>
            {
                _t += 0.012f;
                StepParticles();
                Invalidate();
            };
            _timer.Start();

            MouseMove += (_, e) => _mouse = e.Location;
            Resize += (_, _) => EnsureParticles();
        }

        private void LoadBackgroundImage()
        {
            try
            {
                string bgPath = FindBgImage();
                if (!string.IsNullOrEmpty(bgPath))
                {
                    _bgImage = Image.FromFile(bgPath);
                }
            }
            catch { /* ignore */ }
        }

        private static string FindBgImage()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] paths = {
                System.IO.Path.Combine(baseDir, "Frontend", "Assets", "bg.jpg"),
                System.IO.Path.Combine(baseDir, "Frontend", "Assets", "bg.png"),
                System.IO.Path.Combine(baseDir, "Assets", "bg.jpg"),
                System.IO.Path.Combine(baseDir, "Assets", "bg.png"),
                System.IO.Path.Combine(baseDir, "bg.jpg"),
                System.IO.Path.Combine(baseDir, "bg.png"),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "Frontend", "Assets", "bg.jpg"),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "Frontend", "Assets", "bg.png"),
                System.IO.Path.Combine(Application.StartupPath, "Frontend", "Assets", "bg.jpg"),
                System.IO.Path.Combine(Application.StartupPath, "Frontend", "Assets", "bg.png")
            };
            foreach (var p in paths)
            {
                if (System.IO.File.Exists(p)) return p;
            }
            return string.Empty;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _timer.Dispose();
                _bgImage?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void EnsureParticles()
        {
            int target = Math.Clamp((Width * Height) / 35000, 45, 120);
            while (_particles.Count < target) _particles.Add(Particle.Create(_rng, Width, Height));
            while (_particles.Count > target && _particles.Count > 0) _particles.RemoveAt(_particles.Count - 1);
        }

        private void StepParticles()
        {
            if (Width <= 0 || Height <= 0) return;
            EnsureParticles();

            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                p.X += p.Vx;
                p.Y += p.Vy;
                if (p.X < -50) p.X = Width + 50;
                if (p.X > Width + 50) p.X = -50;
                if (p.Y < -50) p.Y = Height + 50;
                if (p.Y > Height + 50) p.Y = -50;
                _particles[i] = p;
            }
        }

        public void DrawBackgroundGraphics(Graphics g, Rectangle clipRect)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_bgImage != null)
            {
                // Draw background image to fit the container
                g.DrawImage(_bgImage, ClientRectangle);
            }
            else
            {
                // Base background fallback: Deep Slate
                g.Clear(Color.FromArgb(15, 23, 42));
            }

            // Dark overlay (opacity 0.30 matching style.css body:after { opacity: .3; })
            using (var overlay = new SolidBrush(Color.FromArgb(76, 0, 0, 0)))
            {
                g.FillRectangle(overlay, ClientRectangle);
            }

            // Floating particles: Soft warm/cool dots (very subtle 6% opacity)
            using var particleBrush = new SolidBrush(Color.FromArgb(15, 255, 255, 255));
            for (int i = 0; i < _particles.Count; i++)
            {
                var p = _particles[i];
                float a = p.Size;
                g.FillEllipse(particleBrush, p.X - a / 2f, p.Y - a / 2f, a, a);
            }

            // Mouse-follow glow: Very subtle white light
            DrawMouseGlow(g);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            DrawBackgroundGraphics(e.Graphics, e.ClipRectangle);
        }

        private void DrawMouseGlow(Graphics g)
        {
            if (Width <= 0 || Height <= 0) return;
            var center = new PointF(_mouse.X, _mouse.Y);
            float radius = Math.Min(Math.Min(Width, Height) * 0.45f, 400);
            RectangleF r = new(center.X - radius / 2f, center.Y - radius / 2f, radius, radius);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(r);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterPoint = center;
                    pgb.CenterColor = Color.FromArgb(15, 255, 255, 255);
                    pgb.SurroundColors = new[] { Color.FromArgb(0, 255, 255, 255) };
                    g.FillPath(pgb, path);
                }
            }
        }

        private struct Particle
        {
            public float X;
            public float Y;
            public float Vx;
            public float Vy;
            public float Size;

            public static Particle Create(Random rng, int w, int h)
            {
                float x = (float)(rng.NextDouble() * Math.Max(1, w));
                float y = (float)(rng.NextDouble() * Math.Max(1, h));
                float vx = (float)((rng.NextDouble() - 0.5) * 0.4);
                float vy = (float)((rng.NextDouble() - 0.5) * 0.35);
                float size = (float)(1.5 + rng.NextDouble() * 2.5);
                return new Particle { X = x, Y = y, Vx = vx, Vy = vy, Size = size };
            }
        }
    }

    internal sealed class GlassCardHostPanel : Panel
    {
        private readonly System.Windows.Forms.Timer _timer;
        private float _hover;
        private float _sweep;
        private bool _isHover;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public float CornerRadius { get; set; } = 32f;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color GlassFill { get; set; } = Color.FromArgb(15, 255, 255, 255); // translucent frosted glass

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color BorderColor { get; set; } = Color.FromArgb(40, 255, 255, 255); // translucent white border

        public GlassCardHostPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Tag = "custom";

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, _) =>
            {
                float target = _isHover ? 1f : 0f;
                _hover = _hover + (target - _hover) * 0.10f;
                _sweep = (_sweep + 0.02f) % 1.2f;
                Invalidate();
            };
            _timer.Start();

            MouseEnter += (_, _) => _isHover = true;
            MouseLeave += (_, _) => _isHover = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent is AnimatedFuturisticBackgroundPanel bg)
            {
                var g = e.Graphics;
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                bg.DrawBackgroundGraphics(g, new Rectangle(Left, Top, Width, Height));
                g.Restore(state);
            }
            else if (Parent != null)
            {
                var g = e.Graphics;
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                using (var pe = new PaintEventArgs(g, new Rectangle(Left, Top, Width, Height)))
                {
                    InvokePaintBackground(Parent, pe);
                    InvokePaint(Parent, pe);
                }
                g.Restore(state);
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            float margin = 24f;
            var rect = new RectangleF(margin, margin, Width - margin * 2f, Height - margin * 2f);
            using var path = FuturisticLoginKit.CreateRoundedRect(rect, CornerRadius);

            // If GlassFill is fully transparent, bypass drop shadow and hover shimmer effects
            if (GlassFill.A == 0)
            {
                if (BorderColor.A > 0)
                {
                    using var pen = new Pen(BorderColor, 1f);
                    g.DrawPath(pen, path);
                }
                return;
            }

            // Draw drop shadow (very soft black shadow)
            var stateGlow = g.Save();
            using (var reg = new Region(path))
            {
                g.ExcludeClip(reg);
            }
            DrawSoftGlow(g, rect, CornerRadius, Color.FromArgb((int)(10 + 10 * _hover), 0, 0, 0), 20);
            g.Restore(stateGlow);

            // Translucent glass fill
            using (var fill = new SolidBrush(GlassFill))
                g.FillPath(fill, path);

            // Light sweep on hover (shimmer)
            if (_hover > 0.05f)
            {
                float sweepW = rect.Width * 0.5f;
                float x = rect.X + (rect.Width + sweepW) * (_sweep - 0.2f) - sweepW;
                using var sweepBrush = new LinearGradientBrush(
                    new RectangleF(x, rect.Y, sweepW, rect.Height),
                    Color.FromArgb(0, 255, 255, 255),
                    Color.FromArgb((int)(40 * _hover), 255, 255, 255),
                    20f);
                var cb = new ColorBlend
                {
                    Colors = new[]
                    {
                        Color.FromArgb(0, 255, 255, 255),
                        Color.FromArgb((int)(50 * _hover), 255, 255, 255),
                        Color.FromArgb(0, 255, 255, 255)
                    },
                    Positions = new[] { 0f, 0.5f, 1f }
                };
                sweepBrush.InterpolationColors = cb;
                var state = g.Save();
                g.SetClip(path);
                g.FillRectangle(sweepBrush, x, rect.Y, sweepW, rect.Height);
                g.Restore(state);
            }

            // Border (very clean translucent line)
            if (BorderColor.A > 0)
            {
                using var pen = new Pen(BorderColor, 1f);
                g.DrawPath(pen, path);
            }
        }

        private static void DrawSoftGlow(Graphics g, RectangleF rect, float radius, Color glowColor, int spread)
        {
            for (int i = spread; i >= 1; i -= 2)
            {
                float alphaScale = (float)i / spread;
                int a = (int)(glowColor.A * (1f - alphaScale) * 0.55f);
                if (a <= 0) continue;
                using var p = FuturisticLoginKit.CreateRoundedRect(new RectangleF(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2), radius + i);
                using var b = new SolidBrush(Color.FromArgb(a, glowColor));
                g.FillPath(b, p);
            }
        }
    }

    internal sealed class NeonGradientButton : Button
    {
        private readonly System.Windows.Forms.Timer _timer;
        private bool _hover;
        private Point _lastClick;
        private float _ripple;

        public NeonGradientButton()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.Black;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;

            _timer = new System.Windows.Forms.Timer { Interval = 16 };
            _timer.Tick += (_, _) =>
            {
                if (_ripple > 0) _ripple *= 0.86f;
                Invalidate();
            };
            _timer.Start();

            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
            MouseDown += (_, e) => { _lastClick = e.Location; _ripple = 1f; Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            float r = Math.Min(Height, 50) / 2f; // capsule shape!
            var rect = new RectangleF(0, 0, Width, Height);
            using (var path = FuturisticLoginKit.CreateRoundedRect(rect, r))
            {
                var oldRegion = this.Region;
                this.Region = new Region(path);
                oldRegion?.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // First draw the parent background so transparent parts inherit correctly
            base.OnPaintBackground(e);

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float r = Math.Min(Height, 50) / 2f; // capsule shape!
            using var path = FuturisticLoginKit.CreateRoundedRect(rect, r);

            // Peach color accent: #fbceb5 (RGB 251, 206, 181)
            Color peach = Color.FromArgb(251, 206, 181);

            if (_hover)
            {
                // Hover: Transparent background with peach border
                using (var borderPen = new Pen(peach, 1.5f))
                {
                    g.DrawPath(borderPen, path);
                }
                ForeColor = peach;
            }
            else
            {
                // Normal: Solid peach background
                using (var fill = new SolidBrush(peach))
                {
                    g.FillPath(fill, path);
                }
                ForeColor = Color.Black;
            }

            // Draw ripple effect if clicked
            if (_ripple > 0.02f)
            {
                float maxR = Math.Max(Width, Height) * 1.1f;
                float rr = maxR * (1f - _ripple);
                int a = (int)(40 * _ripple);
                using var rippleBrush = new SolidBrush(Color.FromArgb(a, 255, 255, 255));
                var state = g.Save();
                g.SetClip(path);
                g.FillEllipse(rippleBrush, _lastClick.X - rr / 2f, _lastClick.Y - rr / 2f, rr, rr);
                g.Restore(state);
            }

            // Text uppercase
            TextRenderer.DrawText(
                g,
                Text.ToUpper(),
                Font,
                Rectangle.Round(rect),
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal class TransparentPanel : Panel
    {
        public TransparentPanel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            BackColor = Color.Transparent;
            Tag = "custom"; // Prevents AppColors.ApplyTheme from overwriting BackColor
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Parent != null)
            {
                var g = e.Graphics;
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                using (var pe = new PaintEventArgs(g, new Rectangle(Left, Top, Width, Height)))
                {
                    InvokePaintBackground(Parent, pe);
                    // InvokePaint(Parent, pe); // Removed to fix border drawing artifacts
                }
                g.Restore(state);
            }
            else
            {
                base.OnPaintBackground(e);
            }
        }
    }
}

