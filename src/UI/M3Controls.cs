using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace Chess.UI
{
    /// <summary>Основа для элементов M3: двойная буферизация, состояния наведения и нажатия на пружинах.</summary>
    abstract class AnimatedControl : Control
    {
        protected readonly Spring Hover = Spring.Smooth(0);
        protected readonly Spring Press = Spring.Bouncy(0);
        Color surface = Theme.Background;
        readonly Func<float, bool> tick;

        protected AnimatedControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand;
            tick = Tick;
        }

        /// <summary>Цвет поверхности, на которой лежит элемент (им закрашиваются скруглённые углы).</summary>
        public Color Surface { get { return surface; } set { surface = value; Invalidate(); } }

        protected virtual bool StepAnimations(float dt)
        {
            Hover.Step(dt);
            Press.Step(dt);
            return !(Hover.Settled && Press.Settled);
        }

        bool Tick(float dt)
        {
            if (IsDisposed) return false;
            bool running = StepAnimations(dt);
            Invalidate();
            return running;
        }

        protected void Animate() { FrameClock.Run(tick); }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Hover.Target = 1; Animate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Hover.Target = 0; Press.Target = 0; Animate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Press.Target = 1;
            Animate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Press.Target = 0;
            Animate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(surface);
            PaintContent(g);
        }

        protected abstract void PaintContent(Graphics g);
    }

    enum ButtonStyle { Filled, Tonal, Outlined, Text }

    /// <summary>Кнопка M3 Expressive: при нажатии скругление «сжимается» (shape morph), от точки клика расходится волна.</summary>
    sealed class M3Button : AnimatedControl
    {
        public ButtonStyle Style = ButtonStyle.Filled;
        public string IconGlyph;
        public float FontPx = 16;
        readonly Spring ripple = Spring.Smooth(1);
        PointF rippleCenter;

        public M3Button(string text, string icon, ButtonStyle style)
        {
            Text = text;
            IconGlyph = icon;
            Style = style;
        }

        protected override bool StepAnimations(float dt)
        {
            ripple.Step(dt * 0.6f);
            bool baseRunning = base.StepAnimations(dt);
            return baseRunning || !ripple.Settled;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { rippleCenter = e.Location; ripple.Snap(0); ripple.Target = 1; }
            base.OnMouseDown(e);
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        void Colors(out Color container, out Color content)
        {
            switch (Style)
            {
                case ButtonStyle.Filled: container = Theme.Primary; content = Theme.OnPrimary; break;
                case ButtonStyle.Tonal: container = Theme.SecondaryContainer; content = Theme.OnSecondaryContainer; break;
                default: container = Color.Transparent; content = Theme.Primary; break;
            }
            if (!Enabled)
            {
                container = Style == ButtonStyle.Filled || Style == ButtonStyle.Tonal ? Theme.WithAlpha(Theme.OnSurface, 0.12f) : Color.Transparent;
                content = Theme.WithAlpha(Theme.OnSurface, 0.38f);
            }
        }

        protected override void PaintContent(Graphics g)
        {
            Color container, content;
            Colors(out container, out content);
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float full = r.Height / 2f;
            float pressed = Math.Min(full, Theme.F(12));
            float radius = full + (pressed - full) * Press.Value;

            using (var path = Theme.RoundRect(r, radius))
            {
                if (container.A > 0) using (var b = new SolidBrush(container)) g.FillPath(b, path);
                if (Style == ButtonStyle.Outlined)
                    using (var pen = new Pen(Enabled ? Theme.Outline : Theme.WithAlpha(Theme.OnSurface, 0.12f), Theme.F(1))) g.DrawPath(pen, path);
                if (Enabled)
                {
                    float state = 0.08f * Hover.Value + 0.04f * Math.Max(0, Press.Value);
                    using (var b = new SolidBrush(Theme.WithAlpha(content, state))) g.FillPath(b, path);
                    if (ripple.Value < 0.999f)
                    {
                        var clip = g.Clip;
                        g.SetClip(path);
                        float maxR = (float)Math.Sqrt(Width * Width + Height * Height);
                        float rr = maxR * ripple.Value;
                        using (var b = new SolidBrush(Theme.WithAlpha(content, 0.14f * (1 - ripple.Value))))
                            g.FillEllipse(b, rippleCenter.X - rr, rippleCenter.Y - rr, rr * 2, rr * 2);
                        g.Clip = clip;
                    }
                }
            }
            PaintLabel(g, content);
        }

        void PaintLabel(Graphics g, Color content)
        {
            var font = Theme.Label(FontPx);
            bool hasText = !string.IsNullOrEmpty(Text);
            float iconSize = Theme.F(FontPx + 4);
            float gap = hasText && IconGlyph != null ? Theme.F(8) : 0;
            float textW = hasText ? g.MeasureString(Text, font).Width : 0;
            float total = (IconGlyph != null ? iconSize : 0) + gap + textW;
            float x = (Width - total) / 2f;
            if (IconGlyph != null)
            {
                Theme.DrawText(g, IconGlyph, Theme.Icon(FontPx + 2), content, new RectangleF(x, 0, iconSize, Height), StringAlignment.Center, StringAlignment.Center);
                x += iconSize + gap;
            }
            if (hasText)
                Theme.DrawText(g, Text, font, content, new RectangleF(x, 0, textW + 2, Height), StringAlignment.Near, StringAlignment.Center);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty);
        }
    }

    /// <summary>Карточка выбора (уровень сложности): выбранная становится цветной и меняет форму.</summary>
    sealed class SelectCard : AnimatedControl
    {
        public string Title, Description, Glyph;
        readonly Spring selection = Spring.Bouncy(0);

        public SelectCard(string title, string description, string glyph)
        {
            Title = title;
            Description = description;
            Glyph = glyph;
        }

        public bool Selected
        {
            get { return selection.Target > 0.5f; }
            set { selection.Target = value ? 1 : 0; Animate(); }
        }

        protected override bool StepAnimations(float dt)
        {
            selection.Step(dt);
            bool baseRunning = base.StepAnimations(dt);
            return baseRunning || !selection.Settled;
        }

        protected override void PaintContent(Graphics g)
        {
            float s = Math.Max(0, Math.Min(1, selection.Value));
            var bg = Theme.Mix(Theme.SurfaceContainerHigh, Theme.PrimaryContainer, s);
            var fg = Theme.Mix(Theme.OnSurface, Theme.OnPrimaryContainer, s);
            float inset = Theme.F(2) * Press.Value;
            var r = new RectangleF(inset, inset, Width - 1 - inset * 2, Height - 1 - inset * 2);
            // Выбранная карточка «раздувает» углы — смена формы, характерная для Expressive.
            float radius = Theme.F(16) + Theme.F(14) * selection.Value;
            using (var path = Theme.RoundRect(r, radius))
            {
                using (var b = new SolidBrush(bg)) g.FillPath(b, path);
                using (var b = new SolidBrush(Theme.WithAlpha(fg, 0.08f * Hover.Value))) g.FillPath(b, path);
            }

            float pad = Theme.F(18);
            float badge = Theme.F(52);
            var badgeRect = new RectangleF(pad, (Height - badge) / 2, badge, badge);
            using (var cookie = Theme.Cookie(new PointF(badgeRect.X + badge / 2, badgeRect.Y + badge / 2), badge / 2, 8, 0.07f, 22.5f * selection.Value))
            using (var b = new SolidBrush(Theme.Mix(Theme.SecondaryContainer, Theme.Primary, s)))
                g.FillPath(b, cookie);
            Theme.DrawText(g, Glyph, Theme.Body(30), Theme.Mix(Theme.OnSecondaryContainer, Theme.OnPrimary, s), badgeRect, StringAlignment.Center, StringAlignment.Center);

            float tx = badgeRect.Right + Theme.F(16);
            float tw = Width - tx - pad - Theme.F(28);
            Theme.DrawText(g, Title, Theme.Title(19), fg, new RectangleF(tx, Height / 2f - Theme.F(26), tw, Theme.F(28)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, Description, Theme.Body(14), Theme.WithAlpha(fg, 0.78f), new RectangleF(tx, Height / 2f + Theme.F(1), tw, Theme.F(24)), StringAlignment.Near, StringAlignment.Center);

            if (s > 0.01f)
            {
                float c = Theme.F(26) * s;
                var cr = new RectangleF(Width - pad - Theme.F(26) + (Theme.F(26) - c) / 2, (Height - c) / 2, c, c);
                using (var b = new SolidBrush(Theme.Primary)) g.FillEllipse(b, cr);
                Theme.DrawText(g, Theme.IconCheck, Theme.Icon(13), Theme.OnPrimary, cr, StringAlignment.Center, StringAlignment.Center);
            }
        }
    }

    /// <summary>Сегментированная кнопка M3 (выбор цвета фигур).</summary>
    sealed class SegmentedButton : AnimatedControl
    {
        readonly string[] options;
        int selected;
        int hoverIndex = -1;
        readonly Spring slide = Spring.Bouncy(0);

        public event EventHandler SelectionChanged;

        public SegmentedButton(string[] options, int selected)
        {
            this.options = options;
            this.selected = selected;
            slide.Snap(selected);
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected) return;
                selected = value;
                slide.Target = value;
                Animate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        protected override bool StepAnimations(float dt)
        {
            slide.Step(dt);
            bool baseRunning = base.StepAnimations(dt);
            return baseRunning || !slide.Settled;
        }

        int IndexAt(int x) { return Math.Max(0, Math.Min(options.Length - 1, x * options.Length / Math.Max(1, Width))); }

        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); hoverIndex = IndexAt(e.X); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hoverIndex = -1; }
        protected override void OnMouseClick(MouseEventArgs e) { base.OnMouseClick(e); SelectedIndex = IndexAt(e.X); }

        protected override void PaintContent(Graphics g)
        {
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float seg = r.Width / options.Length;
            using (var outer = Theme.RoundRect(r, r.Height / 2))
            {
                var clip = g.Clip;
                g.SetClip(outer);
                // Выделение «переезжает» между сегментами на пружине.
                var sel = new RectangleF(r.X + seg * slide.Value, r.Y, seg, r.Height);
                using (var b = new SolidBrush(Theme.SecondaryContainer)) g.FillRectangle(b, sel);
                if (hoverIndex >= 0)
                    using (var b = new SolidBrush(Theme.WithAlpha(Theme.OnSurface, 0.08f * Hover.Value)))
                        g.FillRectangle(b, r.X + seg * hoverIndex, r.Y, seg, r.Height);
                g.Clip = clip;
                using (var pen = new Pen(Theme.Outline, Theme.F(1)))
                {
                    g.DrawPath(pen, outer);
                    for (int i = 1; i < options.Length; i++) g.DrawLine(pen, r.X + seg * i, r.Y, r.X + seg * i, r.Bottom);
                }
            }
            for (int i = 0; i < options.Length; i++)
            {
                bool on = i == selected;
                var cell = new RectangleF(r.X + seg * i, r.Y, seg, r.Height);
                string text = (on ? "✓ " : "") + options[i];
                Theme.DrawText(g, text, Theme.Label(15), on ? Theme.OnSecondaryContainer : Theme.OnSurface, cell, StringAlignment.Center, StringAlignment.Center);
            }
        }
    }
}
