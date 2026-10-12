using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using Chess.Rules;

namespace Chess.UI
{
    /// <summary>Кнопка, нарисованная прямо на доске (внутри диалога).</summary>
    sealed class DrawnButton
    {
        public RectangleF Bounds;
        public string Text;
        public bool Filled;
        public Action OnClick;
        public bool Hover;

        public void Paint(Graphics g, float alpha)
        {
            Color bg = Filled ? Theme.Primary : Color.Transparent;
            Color fg = Filled ? Theme.OnPrimary : Theme.Primary;
            using (var path = Theme.RoundRect(Bounds, Bounds.Height / 2))
            {
                if (Filled) using (var b = new SolidBrush(Theme.WithAlpha(bg, alpha))) g.FillPath(b, path);
                else using (var pen = new Pen(Theme.WithAlpha(Theme.Outline, alpha), Theme.F(1))) g.DrawPath(pen, path);
                if (Hover) using (var b = new SolidBrush(Theme.WithAlpha(fg, 0.10f * alpha))) g.FillPath(b, path);
            }
            Theme.DrawText(g, Text, Theme.Label(15), Theme.WithAlpha(fg, alpha), Bounds, StringAlignment.Center, StringAlignment.Center);
        }
    }

    /// <summary>Модальный слой поверх доски: затемнение и карточка, появляющаяся на пружине.</summary>
    abstract class BoardOverlay
    {
        public readonly Spring Appear = Spring.Bouncy(0);

        protected BoardOverlay() { Appear.Target = 1; }

        public abstract void Paint(Graphics g, RectangleF board);
        public abstract bool MouseMove(PointF p);
        /// <summary>Возвращает true, если слой нужно закрыть.</summary>
        public abstract bool Click(PointF p, RectangleF board);

        protected float Alpha { get { return Math.Max(0, Math.Min(1, Appear.Value)); } }

        protected void PaintScrim(Graphics g, RectangleF board)
        {
            using (var path = Theme.RoundRect(board, Theme.F(24)))
            using (var b = new SolidBrush(Theme.WithAlpha(Theme.Scrim, Alpha)))
                g.FillPath(b, path);
        }

        /// <summary>Рисует карточку, слегка «вырастающую» из 0.92 до 1.</summary>
        protected void PaintCard(Graphics g, RectangleF card)
        {
            float scale = 0.92f + 0.08f * Appear.Value;
            var c = new PointF(card.X + card.Width / 2, card.Y + card.Height / 2);
            var r = new RectangleF(c.X - card.Width * scale / 2, c.Y - card.Height * scale / 2, card.Width * scale, card.Height * scale);
            Theme.FillRound(g, Theme.WithAlpha(Color.Black, 0.25f * Alpha), new RectangleF(r.X, r.Y + Theme.F(6), r.Width, r.Height), Theme.F(28));
            Theme.FillRound(g, Theme.WithAlpha(Theme.SurfaceContainerHigh, Alpha), r, Theme.F(28));
        }
    }

    /// <summary>Диалог M3: заголовок, текст, кнопки. Используется для итога партии, ничьей и сдачи.</summary>
    sealed class DialogOverlay : BoardOverlay
    {
        readonly string title, text, badge;
        readonly List<DrawnButton> buttons = new List<DrawnButton>();
        RectangleF card;

        public DialogOverlay(string title, string text, string badge)
        {
            this.title = title;
            this.text = text;
            this.badge = badge;
        }

        public DialogOverlay AddButton(string caption, bool filled, Action onClick)
        {
            buttons.Add(new DrawnButton { Text = caption, Filled = filled, OnClick = onClick });
            return this;
        }

        void Layout(RectangleF board)
        {
            float w = Math.Min(board.Width * 0.84f, Theme.F(420));
            float h = Theme.F(badge != null ? 300 : 220);
            card = new RectangleF(board.X + (board.Width - w) / 2, board.Y + (board.Height - h) / 2, w, h);
            float bh = Theme.F(48), gap = Theme.F(10), pad = Theme.F(24);
            float bw = (w - pad * 2 - gap * (buttons.Count - 1)) / Math.Max(1, buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].Bounds = new RectangleF(card.X + pad + i * (bw + gap), card.Bottom - pad - bh, bw, bh);
        }

        public override void Paint(Graphics g, RectangleF board)
        {
            Layout(board);
            PaintScrim(g, board);
            PaintCard(g, card);
            float a = Alpha;
            float y = card.Y + Theme.F(24);
            if (badge != null)
            {
                float size = Theme.F(72);
                var center = new PointF(card.X + card.Width / 2, y + size / 2);
                using (var cookie = Theme.Cookie(center, size / 2, 9, 0.06f, 20 * (1 - Appear.Value)))
                using (var b = new SolidBrush(Theme.WithAlpha(Theme.PrimaryContainer, a)))
                    g.FillPath(b, cookie);
                Theme.DrawText(g, badge, Theme.Body(40), Theme.WithAlpha(Theme.OnPrimaryContainer, a),
                    new RectangleF(center.X - size / 2, center.Y - size / 2, size, size), StringAlignment.Center, StringAlignment.Center);
                y += size + Theme.F(12);
            }
            Theme.DrawText(g, title, Theme.Headline(30), Theme.WithAlpha(Theme.OnSurface, a), new RectangleF(card.X, y, card.Width, Theme.F(40)), StringAlignment.Center, StringAlignment.Center);
            y += Theme.F(44);
            Theme.DrawWrapped(g, text, Theme.Body(15), Theme.WithAlpha(Theme.OnSurfaceVariant, a), new RectangleF(card.X + Theme.F(24), y, card.Width - Theme.F(48), Theme.F(48)), StringAlignment.Center);
            foreach (var b in buttons) b.Paint(g, a);
        }

        public override bool MouseMove(PointF p)
        {
            bool changed = false;
            foreach (var b in buttons)
            {
                bool h = b.Bounds.Contains(p);
                if (h != b.Hover) { b.Hover = h; changed = true; }
            }
            return changed;
        }

        public override bool Click(PointF p, RectangleF board)
        {
            foreach (var b in buttons)
                if (b.Bounds.Contains(p)) { b.OnClick(); return true; }
            return false;
        }
    }

    /// <summary>Выбор фигуры при превращении пешки.</summary>
    sealed class PromotionOverlay : BoardOverlay
    {
        static readonly int[] Kinds = { Piece.Queen, Piece.Rook, Piece.Bishop, Piece.Knight };
        readonly int side;
        readonly Action<int> chosen;
        readonly RectangleF[] tiles = new RectangleF[4];
        int hover = -1;
        RectangleF card;

        public PromotionOverlay(int side, Action<int> chosen)
        {
            this.side = side;
            this.chosen = chosen;
        }

        void Layout(RectangleF board)
        {
            float tile = board.Width / 8 * 1.1f, gap = Theme.F(10), pad = Theme.F(20);
            float w = tile * 4 + gap * 3 + pad * 2, h = tile + pad * 2 + Theme.F(40);
            card = new RectangleF(board.X + (board.Width - w) / 2, board.Y + (board.Height - h) / 2, w, h);
            for (int i = 0; i < 4; i++)
                tiles[i] = new RectangleF(card.X + pad + i * (tile + gap), card.Y + pad + Theme.F(36), tile, tile);
        }

        public override void Paint(Graphics g, RectangleF board)
        {
            Layout(board);
            PaintScrim(g, board);
            PaintCard(g, card);
            float a = Alpha;
            Theme.DrawText(g, "Превращение пешки", Theme.Title(18), Theme.WithAlpha(Theme.OnSurface, a),
                new RectangleF(card.X, card.Y + Theme.F(14), card.Width, Theme.F(32)), StringAlignment.Center, StringAlignment.Center);
            for (int i = 0; i < 4; i++)
            {
                var color = i == hover ? Theme.PrimaryContainer : Theme.SurfaceContainerHighest;
                Theme.FillRound(g, Theme.WithAlpha(color, a), tiles[i], i == hover ? Theme.F(28) : Theme.F(16));
                Theme.DrawPiece(g, Kinds[i] * side, tiles[i], a, i == hover ? 1.06f : 0.95f);
            }
        }

        public override bool MouseMove(PointF p)
        {
            int h = -1;
            for (int i = 0; i < 4; i++) if (tiles[i].Contains(p)) h = i;
            bool changed = h != hover;
            hover = h;
            return changed;
        }

        public override bool Click(PointF p, RectangleF board)
        {
            for (int i = 0; i < 4; i++)
                if (tiles[i].Contains(p)) { chosen(Kinds[i]); return true; }
            // Клик мимо карточки отменяет превращение.
            if (!card.Contains(p)) { chosen(0); return true; }
            return false;
        }
    }
}
