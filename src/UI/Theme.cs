using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Chess.UI
{
    /// <summary>
    /// Цвета, шрифты и формы в духе Material 3 Expressive (тёмная схема от базового фиолетового).
    /// </summary>
    static class Theme
    {
        public static float Scale = 1f;

        public static readonly Color Background = Hex(0x141218);
        public static readonly Color SurfaceContainerLow = Hex(0x1D1B20);
        public static readonly Color SurfaceContainer = Hex(0x211F26);
        public static readonly Color SurfaceContainerHigh = Hex(0x2B2930);
        public static readonly Color SurfaceContainerHighest = Hex(0x36343B);
        public static readonly Color OnSurface = Hex(0xE6E0E9);
        public static readonly Color OnSurfaceVariant = Hex(0xCAC4D0);
        public static readonly Color Outline = Hex(0x938F99);
        public static readonly Color OutlineVariant = Hex(0x49454F);
        public static readonly Color Primary = Hex(0xD0BCFF);
        public static readonly Color OnPrimary = Hex(0x381E72);
        public static readonly Color PrimaryContainer = Hex(0x4F378B);
        public static readonly Color OnPrimaryContainer = Hex(0xEADDFF);
        public static readonly Color SecondaryContainer = Hex(0x4A4458);
        public static readonly Color OnSecondaryContainer = Hex(0xE8DEF8);
        public static readonly Color Tertiary = Hex(0xEFB8C8);
        public static readonly Color TertiaryContainer = Hex(0x633B48);
        public static readonly Color OnTertiaryContainer = Hex(0xFFD8E4);
        public static readonly Color Error = Hex(0xF2B8B5);
        public static readonly Color ErrorContainer = Hex(0x8C1D18);
        public static readonly Color OnErrorContainer = Hex(0xF9DEDC);

        public static readonly Color BoardLight = Hex(0xEADDFF);
        public static readonly Color BoardDark = Hex(0x9A82DB);
        public static readonly Color LastMove = Color.FromArgb(150, 0xFF, 0xB8, 0x6C);
        public static readonly Color Selected = Color.FromArgb(170, 0x7D, 0x52, 0x60);
        public static readonly Color MoveDot = Color.FromArgb(110, 0x21, 0x00, 0x5D);
        public static readonly Color Scrim = Color.FromArgb(150, 0, 0, 0);

        const string TextFamily = "Segoe UI Variable Text";
        const string DisplayFamily = "Segoe UI Variable Display";
        const string PieceFamily = "Segoe UI Symbol";
        static readonly string[] IconFamilies = { "Segoe Fluent Icons", "Segoe MDL2 Assets" };

        // Значки Segoe Fluent Icons / MDL2.
        public const string IconBack = "";
        public const string IconUndo = "";
        public const string IconFlip = "";
        public const string IconFlag = "";
        public const string IconHome = "";
        public const string IconRobot = "";
        public const string IconPeople = "";
        public const string IconExit = "";
        public const string IconPlay = "";
        public const string IconCheck = "";
        public const string IconRefresh = "";

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        static FontFamily pieceFamily;
        static string iconFamily;

        public static int S(float v) { return (int)Math.Round(v * Scale); }
        public static float F(float v) { return v * Scale; }

        static Color Hex(int rgb) { return Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF); }

        public static Color WithAlpha(Color c, float alpha)
        {
            return Color.FromArgb(Clamp((int)(c.A * alpha)), c.R, c.G, c.B);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                Clamp((int)(a.A + (b.A - a.A) * t)), Clamp((int)(a.R + (b.R - a.R) * t)),
                Clamp((int)(a.G + (b.G - a.G) * t)), Clamp((int)(a.B + (b.B - a.B) * t)));
        }

        static int Clamp(int v) { return Math.Max(0, Math.Min(255, v)); }

        static Font Get(string family, float px, FontStyle style)
        {
            string key = family + "|" + px.ToString("0.0") + "|" + style;
            Font f;
            if (fonts.TryGetValue(key, out f)) return f;
            try { f = new Font(family, px, style, GraphicsUnit.Pixel); }
            catch (ArgumentException) { f = new Font("Segoe UI", px, style, GraphicsUnit.Pixel); }
            // GDI+ молча подставляет Microsoft Sans Serif, если шрифта нет.
            if (f.Name != family) { f.Dispose(); f = new Font("Segoe UI", px, style, GraphicsUnit.Pixel); }
            fonts[key] = f;
            return f;
        }

        public static Font Display(float px) { return Get(DisplayFamily, F(px), FontStyle.Bold); }
        public static Font Headline(float px) { return Get(DisplayFamily, F(px), FontStyle.Bold); }
        public static Font Title(float px) { return Get(TextFamily, F(px), FontStyle.Bold); }
        public static Font Body(float px) { return Get(TextFamily, F(px), FontStyle.Regular); }
        public static Font Label(float px) { return Get(TextFamily, F(px), FontStyle.Bold); }

        public static Font Icon(float px)
        {
            if (iconFamily == null)
            {
                iconFamily = "Segoe UI Symbol";
                foreach (var name in IconFamilies)
                {
                    using (var probe = new Font(name, 10f))
                        if (probe.Name == name) { iconFamily = name; break; }
                }
            }
            return Get(iconFamily, F(px), FontStyle.Regular);
        }

        static FontFamily PieceFamilyOrFallback()
        {
            if (pieceFamily != null) return pieceFamily;
            try { pieceFamily = new FontFamily(PieceFamily); }
            catch (ArgumentException) { pieceFamily = FontFamily.GenericSansSerif; }
            return pieceFamily;
        }

        public static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, path);
        }

        /// <summary>«Печенье» из набора форм M3 Expressive: круг с волнистым краем.</summary>
        public static GraphicsPath Cookie(PointF center, float radius, int lobes, float depth, float rotationDeg)
        {
            const int Steps = 180;
            var pts = new PointF[Steps];
            double rot = rotationDeg * Math.PI / 180;
            for (int i = 0; i < Steps; i++)
            {
                double a = 2 * Math.PI * i / Steps;
                double r = radius * (1 - depth + depth * Math.Cos(lobes * a));
                pts[i] = new PointF(center.X + (float)(r * Math.Cos(a + rot)), center.Y + (float)(r * Math.Sin(a + rot)));
            }
            var path = new GraphicsPath();
            path.AddPolygon(pts);
            return path;
        }

        public static void DrawText(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment h, StringAlignment v)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = v, Trimming = StringTrimming.EllipsisCharacter })
            using (var b = new SolidBrush(color))
            {
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(text, font, b, rect, sf);
            }
        }

        public static void DrawWrapped(Graphics g, string text, Font font, Color color, RectangleF rect, StringAlignment h)
        {
            using (var sf = new StringFormat { Alignment = h, LineAlignment = StringAlignment.Near })
            using (var b = new SolidBrush(color))
                g.DrawString(text, font, b, rect, sf);
        }

        public static string Glyph(int kind, bool outline)
        {
            // Контурные: ♙♘♗♖♕♔, залитые: ♟♞♝♜♛♚
            const string Outline = "♙♘♗♖♕♔";
            const string Solid = "♟♞♝♜♛♚";
            return (outline ? Outline : Solid)[kind - 1].ToString();
        }

        /// <summary>Рисует фигуру (v со знаком) в прямоугольнике клетки с тенью, прозрачностью и масштабом.</summary>
        public static void DrawPiece(Graphics g, int v, RectangleF cell, float alpha, float scale)
        {
            if (v == 0 || alpha <= 0.01f) return;
            int kind = Math.Abs(v);
            float w = cell.Width * scale, h = cell.Height * scale;
            var r = new RectangleF(cell.X + (cell.Width - w) / 2, cell.Y + (cell.Height - h) / 2 + h * 0.03f, w, h);
            float em = h * 0.80f;
            var family = PieceFamilyOrFallback();
            // Шрифты рассчитаны на заливку по правилу ненулевой обмотки; по умолчанию GDI+ заливает «чёт-нечет»
            // и выворачивает перекрывающиеся контуры глифов.
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var outline = new GraphicsPath(FillMode.Winding))
            {
                outline.AddString(Glyph(kind, true), family, 0, em, r, sf);
                using (var silhouette = Silhouette(outline))
                {
                    using (var shadow = (GraphicsPath)silhouette.Clone())
                    using (var m = new Matrix())
                    {
                        m.Translate(0, Math.Max(1.5f, h * 0.035f));
                        shadow.Transform(m);
                        using (var sb = new SolidBrush(Color.FromArgb((int)(70 * alpha), 0, 0, 0))) g.FillPath(sb, shadow);
                    }
                    // Силуэт заливается цветом фигуры, сверху — линии контурного глифа.
                    bool white = v > 0;
                    var body = white ? Color.FromArgb(0xFF, 0xFB, 0xFE) : Color.FromArgb(0x1D, 0x1B, 0x20);
                    var lines = white ? Color.FromArgb(255, 0x1D, 0x1B, 0x20) : Color.FromArgb(150, 0xE6, 0xE0, 0xE9);
                    using (var fill = new SolidBrush(Color.FromArgb((int)(255 * alpha), body))) g.FillPath(fill, silhouette);
                    using (var ink = new SolidBrush(Color.FromArgb((int)(lines.A * alpha), lines))) g.FillPath(ink, outline);
                }
            }
        }

        /// <summary>Внешние контуры глифа (без внутренних) — сплошной силуэт фигуры.</summary>
        static GraphicsPath Silhouette(GraphicsPath glyph)
        {
            var subpaths = new List<GraphicsPath>();
            using (var it = new GraphicsPathIterator(glyph))
            {
                bool closed;
                var sub = new GraphicsPath(FillMode.Winding);
                while (it.NextSubpath(sub, out closed) > 0)
                {
                    subpaths.Add(sub);
                    sub = new GraphicsPath(FillMode.Winding);
                }
                sub.Dispose();
            }
            var boxes = subpaths.ConvertAll(s => s.GetBounds());
            var result = new GraphicsPath(FillMode.Winding);
            for (int i = 0; i < subpaths.Count; i++)
            {
                bool inner = false;
                for (int j = 0; j < subpaths.Count && !inner; j++)
                    inner = j != i && boxes[j].Contains(boxes[i]) && boxes[j].Width * boxes[j].Height > boxes[i].Width * boxes[i].Height;
                // Внешний контур берём как многоугольник (кривые предварительно спрямляем) — он заливается целиком.
                if (inner) continue;
                subpaths[i].Flatten();
                result.AddPolygon(subpaths[i].PathPoints);
            }
            foreach (var s in subpaths) s.Dispose();
            return result;
        }
    }
}
