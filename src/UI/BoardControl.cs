using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Chess.Rules;

namespace Chess.UI
{
    /// <summary>Шахматная доска: отрисовка, ходы кликом и перетаскиванием, анимация ходов, модальные слои.</summary>
    sealed class BoardControl : Control
    {
        const float MoveDurationSec = 0.30f;
        const float DragThresholdPx = 5;
        const float DragScale = 1.15f;

        sealed class PieceAnim
        {
            public int Piece, From, To;
            public bool Captured;
        }

        GameSession session;
        int selected = -1, hoverSq = -1;
        int dragFrom = -1;
        bool dragging;
        Point downPoint;
        PointF dragPoint;
        readonly List<PieceAnim> anims = new List<PieceAnim>();
        readonly HashSet<int> hiddenSquares = new HashSet<int>();
        float animProgress = 1;
        readonly Spring lift = Spring.Bouncy(0);
        readonly Spring dots = Spring.Smooth(0);
        readonly Spring checkPulse = Spring.Bouncy(0);
        BoardOverlay overlay;
        readonly Func<float, bool> tick;

        public bool Flipped;
        public bool InputEnabled = true;

        /// <summary>Игрок выбрал ход. Второй аргумент — нужна ли анимация (при перетаскивании не нужна).</summary>
        public event Action<Move, bool> MoveChosen;

        public BoardControl()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            tick = Tick;
        }

        public GameSession Session
        {
            get { return session; }
            set { session = value; ClearSelection(); anims.Clear(); hiddenSquares.Clear(); overlay = null; Invalidate(); }
        }

        public bool HasOverlay { get { return overlay != null; } }

        public void ShowOverlay(BoardOverlay o)
        {
            overlay = o;
            ClearSelection();
            Animate();
        }

        public void CloseOverlay() { overlay = null; Invalidate(); }

        public void ClearSelection()
        {
            selected = -1;
            dragFrom = -1;
            dragging = false;
            lift.Target = 0;
            dots.Snap(0);
            Invalidate();
        }

        void Animate() { FrameClock.Run(tick); }

        bool Tick(float dt)
        {
            if (IsDisposed) return false;
            bool running = false;
            if (animProgress < 1)
            {
                animProgress = Math.Min(1, animProgress + dt / MoveDurationSec);
                if (animProgress >= 1) { anims.Clear(); hiddenSquares.Clear(); }
                running = true;
            }
            foreach (var s in new[] { lift, dots, checkPulse })
            {
                s.Step(dt);
                running |= !s.Settled;
            }
            if (overlay != null) { overlay.Appear.Step(dt); running |= !overlay.Appear.Settled; }
            Invalidate();
            return running;
        }

        /// <summary>Запускает анимацию хода m, сделанного из позиции before.</summary>
        public void AnimateMove(Position before, Move m, bool animate)
        {
            ClearSelection();
            anims.Clear();
            hiddenSquares.Clear();
            if (animate)
            {
                int piece = before.B[m.From];
                anims.Add(new PieceAnim { Piece = piece, From = m.From, To = m.To });
                hiddenSquares.Add(m.To);
                if (Position.IsCastle(piece, m))
                {
                    int rf, rt;
                    Position.CastleRook(m.To, out rf, out rt);
                    anims.Add(new PieceAnim { Piece = before.B[rf], From = rf, To = rt });
                    hiddenSquares.Add(rt);
                }
                int cap = before.CapturedBy(m);
                if (cap != 0)
                {
                    int sq = before.CaptureSquare(m);
                    anims.Add(new PieceAnim { Piece = cap, From = sq, To = sq, Captured = true });
                }
                animProgress = 0;
            }
            var after = session.Current;
            if (after.InCheck(after.Side)) { checkPulse.Snap(1.6f); checkPulse.Target = 1; }
            else checkPulse.Snap(0);
            Animate();
        }

        // ---------- Геометрия ----------

        RectangleF BoardRect()
        {
            float size = Math.Min(Width, Height) - Theme.F(4);
            return new RectangleF((Width - size) / 2, (Height - size) / 2, size, size);
        }

        RectangleF CellRect(int sq)
        {
            var b = BoardRect();
            float cell = b.Width / 8;
            int r = sq / 8, c = sq % 8;
            int vr = Flipped ? 7 - r : r, vc = Flipped ? 7 - c : c;
            return new RectangleF(b.X + vc * cell, b.Y + vr * cell, cell, cell);
        }

        int SquareAt(PointF p)
        {
            var b = BoardRect();
            if (!b.Contains(p)) return -1;
            float cell = b.Width / 8;
            int vc = Math.Min(7, (int)((p.X - b.X) / cell)), vr = Math.Min(7, (int)((p.Y - b.Y) / cell));
            int r = Flipped ? 7 - vr : vr, c = Flipped ? 7 - vc : vc;
            return r * 8 + c;
        }

        IEnumerable<Move> MovesFrom(int sq)
        {
            if (session == null || sq < 0) return Enumerable.Empty<Move>();
            return session.Legal.Where(m => m.From == sq);
        }

        // ---------- Мышь ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (overlay != null)
            {
                var current = overlay;
                if (current.Click(e.Location, BoardRect()) && overlay == current) overlay = null;
                Invalidate();
                return;
            }
            if (!InputEnabled || session == null) return;
            int sq = SquareAt(e.Location);
            if (sq < 0) { ClearSelection(); return; }
            if (selected >= 0 && MovesFrom(selected).Any(m => m.To == sq)) { Choose(selected, sq, true); return; }
            if (session.Current.B[sq] * session.Current.Side > 0)
            {
                bool same = sq == selected;
                selected = sq;
                dragFrom = sq;
                downPoint = e.Location;
                dragPoint = e.Location;
                lift.Target = 1;
                if (!same) { dots.Snap(0); dots.Target = 1; }
                Animate();
                return;
            }
            ClearSelection();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (overlay != null)
            {
                if (overlay.MouseMove(e.Location)) Invalidate();
                Cursor = Cursors.Default;
                return;
            }
            int sq = SquareAt(e.Location);
            if (sq != hoverSq) { hoverSq = sq; Invalidate(); }
            bool own = InputEnabled && session != null && sq >= 0 && session.Current.B[sq] * session.Current.Side > 0;
            bool target = selected >= 0 && MovesFrom(selected).Any(m => m.To == sq);
            Cursor = InputEnabled && (own || target || dragging) ? Cursors.Hand : Cursors.Default;
            if (dragFrom >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                if (!dragging && (Math.Abs(e.X - downPoint.X) > Theme.F(DragThresholdPx) || Math.Abs(e.Y - downPoint.Y) > Theme.F(DragThresholdPx)))
                    dragging = true;
                dragPoint = e.Location;
                if (dragging) Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) { dragFrom = -1; return; }
            int from = dragFrom;
            dragging = false;
            dragFrom = -1;
            int sq = SquareAt(e.Location);
            if (sq >= 0 && MovesFrom(from).Any(m => m.To == sq)) Choose(from, sq, false);
            else Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverSq = -1;
            Invalidate();
        }

        void Choose(int from, int to, bool animate)
        {
            var candidates = MovesFrom(from).Where(m => m.To == to).ToList();
            if (candidates.Count == 1) { Raise(candidates[0], animate); return; }
            int side = session.Current.Side;
            ShowOverlay(new PromotionOverlay(side, kind =>
            {
                overlay = null;
                if (kind == 0) { Invalidate(); return; }
                Raise(candidates.First(m => m.Promo == kind), animate);
            }));
        }

        void Raise(Move m, bool animate)
        {
            ClearSelection();
            if (MoveChosen != null) MoveChosen(m, animate);
        }

        // ---------- Отрисовка ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(Parent != null ? Parent.BackColor : Theme.Background);
            if (session == null) return;
            var b = BoardRect();
            float radius = Theme.F(24);

            Theme.FillRound(g, Color.FromArgb(70, 0, 0, 0), new RectangleF(b.X, b.Y + Theme.F(8), b.Width, b.Height), radius);
            using (var boardPath = Theme.RoundRect(b, radius))
            {
                var clip = g.Clip;
                g.SetClip(boardPath);
                PaintSquares(g);
                PaintPieces(g);
                PaintMoveHints(g);
                PaintAnimations(g);
                g.Clip = clip;
            }
            if (dragging && dragFrom >= 0)
            {
                float cell = b.Width / 8 * DragScale;
                Theme.DrawPiece(g, session.Current.B[dragFrom], new RectangleF(dragPoint.X - cell / 2, dragPoint.Y - cell / 2, cell, cell), 1f, 1f);
            }
            if (overlay != null) overlay.Paint(g, b);
        }

        void PaintSquares(Graphics g)
        {
            var p = session.Current;
            var last = session.Last.Move;
            int checkSq = p.InCheck(p.Side) ? p.KingSq(p.Side) : -1;
            using (var light = new SolidBrush(Theme.BoardLight))
            using (var dark = new SolidBrush(Theme.BoardDark))
            using (var lastB = new SolidBrush(Theme.LastMove))
            using (var selB = new SolidBrush(Theme.Selected))
            {
                for (int sq = 0; sq < 64; sq++)
                {
                    var r = CellRect(sq);
                    var rr = new RectangleF(r.X, r.Y, r.Width + 0.6f, r.Height + 0.6f);
                    bool isLight = (sq / 8 + sq % 8) % 2 == 0;
                    g.FillRectangle(isLight ? light : dark, rr);
                    if (last.HasValue && (sq == last.Value.From || sq == last.Value.To)) g.FillRectangle(lastB, rr);
                    if (sq == selected) g.FillRectangle(selB, rr);
                    if (sq == checkSq) PaintCheckGlow(g, r);
                    PaintCoordinates(g, sq, r, isLight);
                }
            }
        }

        void PaintCheckGlow(Graphics g, RectangleF r)
        {
            float grow = Math.Max(0.6f, checkPulse.Value);
            float w = r.Width * grow;
            var er = new RectangleF(r.X + (r.Width - w) / 2, r.Y + (r.Height - w) / 2, w, w);
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(er);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(230, 0xFF, 0x3B, 0x30);
                    brush.SurroundColors = new[] { Color.FromArgb(0, 0xFF, 0x3B, 0x30) };
                    g.FillPath(brush, path);
                }
            }
        }

        void PaintCoordinates(Graphics g, int sq, RectangleF r, bool isLight)
        {
            int vr = Flipped ? 7 - sq / 8 : sq / 8, vc = Flipped ? 7 - sq % 8 : sq % 8;
            var color = isLight ? Theme.BoardDark : Theme.BoardLight;
            var font = Theme.Label(Math.Max(9, r.Height / Theme.Scale * 0.16f));
            float pad = r.Width * 0.06f;
            if (vc == 0)
                Theme.DrawText(g, (8 - sq / 8).ToString(), font, color, new RectangleF(r.X + pad, r.Y + pad * 0.5f, r.Width, r.Height * 0.3f), StringAlignment.Near, StringAlignment.Near);
            if (vr == 7)
                Theme.DrawText(g, ((char)('a' + sq % 8)).ToString(), font, color, new RectangleF(r.X, r.Bottom - r.Height * 0.3f, r.Width - pad, r.Height * 0.3f - pad * 0.4f), StringAlignment.Far, StringAlignment.Far);
        }

        void PaintPieces(Graphics g)
        {
            var p = session.Current;
            for (int sq = 0; sq < 64; sq++)
            {
                if (p.B[sq] == 0 || hiddenSquares.Contains(sq)) continue;
                if (dragging && sq == dragFrom)
                {
                    Theme.DrawPiece(g, p.B[sq], CellRect(sq), 0.35f, 1f);
                    continue;
                }
                float scale = sq == selected ? 1f + 0.08f * lift.Value : 1f;
                Theme.DrawPiece(g, p.B[sq], CellRect(sq), 1f, scale);
            }
        }

        void PaintMoveHints(Graphics g)
        {
            int from = selected;
            if (from < 0 || dots.Value <= 0.01f || !InputEnabled) return;
            var p = session.Current;
            float a = Math.Max(0, Math.Min(1, dots.Value));
            foreach (var m in MovesFrom(from).GroupBy(m => m.To).Select(gr => gr.First()))
            {
                var r = CellRect(m.To);
                bool capture = p.CapturedBy(m) != 0;
                bool hovered = m.To == hoverSq;
                using (var b = new SolidBrush(Theme.WithAlpha(Theme.MoveDot, a)))
                {
                    if (hovered)
                    {
                        Theme.FillRound(g, Theme.WithAlpha(Theme.MoveDot, a * 0.8f), RectangleF.Inflate(r, -r.Width * 0.04f, -r.Height * 0.04f), r.Width * 0.22f);
                    }
                    else if (capture)
                    {
                        using (var pen = new Pen(b, r.Width * 0.08f))
                            g.DrawEllipse(pen, RectangleF.Inflate(r, -r.Width * 0.08f, -r.Height * 0.08f));
                    }
                    else
                    {
                        float d = r.Width * 0.30f * (0.6f + 0.4f * dots.Value);
                        g.FillEllipse(b, r.X + (r.Width - d) / 2, r.Y + (r.Height - d) / 2, d, d);
                    }
                }
            }
        }

        void PaintAnimations(Graphics g)
        {
            if (anims.Count == 0) return;
            float t = Easing.EmphasizedDecelerate(animProgress);
            foreach (var a in anims.Where(x => x.Captured))
            {
                // Взятая фигура «съёживается» и тает.
                float fade = 1 - Math.Min(1, animProgress * 1.6f);
                Theme.DrawPiece(g, a.Piece, CellRect(a.From), fade, 0.6f + 0.4f * fade);
            }
            foreach (var a in anims.Where(x => !x.Captured))
            {
                var from = CellRect(a.From);
                var to = CellRect(a.To);
                var r = new RectangleF(from.X + (to.X - from.X) * t, from.Y + (to.Y - from.Y) * t, from.Width, from.Height);
                // В полёте фигура чуть приподнимается.
                float arc = (float)Math.Sin(Math.PI * animProgress) * 0.10f;
                Theme.DrawPiece(g, a.Piece, r, 1f, 1f + arc);
            }
        }
    }
}
