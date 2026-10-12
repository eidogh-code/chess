using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Chess.Rules;

namespace Chess.UI
{
    /// <summary>Запись партии: пары ходов, последний ход выделен, прокрутка колесом.</summary>
    sealed class MovesListView : Control
    {
        const float RowH = 34;
        const float Pad = 14;
        readonly List<string> sans = new List<string>();
        float scroll;

        public MovesListView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        public void SetHistory(IList<HistoryEntry> history)
        {
            int before = sans.Count;
            sans.Clear();
            for (int i = 1; i < history.Count; i++) sans.Add(history[i].San);
            // Новый ход прокручивает к концу; в остальных случаях позицию прокрутки не трогаем.
            scroll = sans.Count > before ? MaxScroll() : Math.Min(scroll, MaxScroll());
            Invalidate();
        }

        int Rows { get { return (sans.Count + 1) / 2; } }

        float MaxScroll()
        {
            return Math.Max(0, Rows * Theme.F(RowH) + Theme.F(Pad) * 2 + Theme.F(36) - Height);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll = Math.Max(0, Math.Min(MaxScroll(), scroll - e.Delta / 120f * Theme.F(RowH) * 2));
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Focus(); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); scroll = Math.Min(scroll, MaxScroll()); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(Theme.Background);
            var card = new RectangleF(0, 0, Width - 1, Height - 1);
            Theme.FillRound(g, Theme.SurfaceContainerLow, card, Theme.F(24));
            using (var path = Theme.RoundRect(card, Theme.F(24)))
            {
                g.SetClip(path);
                float pad = Theme.F(Pad), rowH = Theme.F(RowH);
                Theme.DrawText(g, "Ходы", Theme.Label(14), Theme.Primary, new RectangleF(pad + Theme.F(4), pad - scroll, Width, Theme.F(24)), StringAlignment.Near, StringAlignment.Center);
                if (sans.Count == 0)
                {
                    Theme.DrawText(g, "Сделайте первый ход", Theme.Body(14), Theme.OnSurfaceVariant, new RectangleF(0, Theme.F(36), Width, Height - Theme.F(36)), StringAlignment.Center, StringAlignment.Center);
                    return;
                }
                float colW = (Width - pad * 2 - Theme.F(44)) / 2;
                for (int row = 0; row < Rows; row++)
                {
                    float y = pad + Theme.F(36) + row * rowH - scroll;
                    if (y + rowH < 0 || y > Height) continue;
                    if (row % 2 == 1)
                        Theme.FillRound(g, Theme.WithAlpha(Theme.SurfaceContainerHigh, 0.6f), new RectangleF(pad * 0.5f, y, Width - pad, rowH), rowH / 2);
                    Theme.DrawText(g, (row + 1) + ".", Theme.Body(14), Theme.OnSurfaceVariant, new RectangleF(pad, y, Theme.F(40), rowH), StringAlignment.Near, StringAlignment.Center);
                    for (int k = 0; k < 2; k++)
                    {
                        int idx = row * 2 + k;
                        if (idx >= sans.Count) break;
                        var cell = new RectangleF(pad + Theme.F(44) + colW * k, y + Theme.F(3), colW - Theme.F(6), rowH - Theme.F(6));
                        bool last = idx == sans.Count - 1;
                        if (last) Theme.FillRound(g, Theme.PrimaryContainer, cell, cell.Height / 2);
                        Theme.DrawText(g, sans[idx], last ? Theme.Label(15) : Theme.Body(15), last ? Theme.OnPrimaryContainer : Theme.OnSurface,
                            RectangleF.Inflate(cell, -Theme.F(10), 0), StringAlignment.Near, StringAlignment.Center);
                    }
                }
            }
        }
    }
}
