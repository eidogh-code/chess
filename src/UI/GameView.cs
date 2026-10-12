using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Chess.Rules;

namespace Chess.UI
{
    /// <summary>Экран партии: доска, панель игроков со взятыми фигурами, список ходов, действия.</summary>
    sealed class GameView : Control
    {
        const int MinBotDelayMs = 450;
        const int BotAcceptsDrawBelowCp = -150;
        const float PanelWidth = 380;
        const float SnackbarSeconds = 2.8f;

        static readonly string[] LevelNames = { "Новичок", "Любитель", "Профи" };

        readonly BoardControl board = new BoardControl();
        readonly MovesListView movesList = new MovesListView();
        readonly M3Button undoButton = new M3Button(null, Theme.IconUndo, ButtonStyle.Tonal);
        readonly M3Button flipButton = new M3Button(null, Theme.IconFlip, ButtonStyle.Tonal);
        readonly M3Button drawButton = new M3Button("½", null, ButtonStyle.Tonal) { FontPx = 20 };
        readonly M3Button resignButton = new M3Button(null, Theme.IconFlag, ButtonStyle.Tonal);
        readonly M3Button menuButton = new M3Button("В меню", Theme.IconHome, ButtonStyle.Outlined);
        readonly ToolTip tips = new ToolTip();
        readonly Spring[] capturePop = { Spring.Bouncy(1), Spring.Bouncy(1) };
        readonly Spring snack = Spring.Smooth(0);
        readonly Func<float, bool> tick;
        GameSession session;
        bool thinking;
        int botRequest;
        float thinkingPhase;
        string snackText;
        float snackLeft;
        int[] lastCaptureCounts = new int[2];

        public event EventHandler MenuRequested;

        public GameView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            tick = Tick;

            board.MoveChosen += (m, animate) => ApplyMove(m, animate);
            undoButton.Click += delegate { Undo(); };
            flipButton.Click += delegate { Flip(); };
            drawButton.Click += delegate { OfferDraw(); };
            resignButton.Click += delegate { ConfirmResign(); };
            menuButton.Click += delegate { if (MenuRequested != null) MenuRequested(this, EventArgs.Empty); };
            tips.SetToolTip(undoButton, "Отменить ход (Ctrl+Z)");
            tips.SetToolTip(flipButton, "Перевернуть доску (F)");
            tips.SetToolTip(drawButton, "Ничья");
            tips.SetToolTip(resignButton, "Сдаться");

            Controls.AddRange(new Control[] { board, movesList, undoButton, flipButton, drawButton, resignButton, menuButton });
        }

        public void StartGame(GameMode mode, Difficulty level, int humanSide)
        {
            botRequest++;
            thinking = false;
            session = new GameSession(mode, level, mode == GameMode.VsBot ? -humanSide : 0);
            board.Flipped = mode == GameMode.VsBot && humanSide == Piece.Black;
            board.Session = session;
            lastCaptureCounts = new int[2];
            snackLeft = 0;
            snack.Snap(0);
            Refresh();
            UpdateUi();
            MaybeBotMove();
        }

        /// <summary>Отменяет расчёт бота (например, при выходе в меню).</summary>
        public void StopBot()
        {
            botRequest++;
            thinking = false;
        }

        // ---------- Ходы ----------

        void ApplyMove(Move m, bool animate)
        {
            if (session.IsOver) return;
            var before = session.Current;
            session.Apply(m);
            board.AnimateMove(before, m, animate);
            AfterChange();
            MaybeBotMove();
        }

        void AfterChange()
        {
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? Piece.White : Piece.Black;
                int count = session.CapturedBy(side).Count;
                if (count > lastCaptureCounts[i]) { capturePop[i].Snap(0.2f); capturePop[i].Target = 1; }
                lastCaptureCounts[i] = count;
            }
            UpdateUi();
            if (session.IsOver) ShowResult();
            FrameClock.Run(tick);
        }

        async void MaybeBotMove()
        {
            if (session == null || !session.IsBotTurn) return;
            int request = ++botRequest;
            var current = session;
            var pos = current.Current;
            var level = BotLevel.For(current.Level);
            thinking = true;
            UpdateUi();
            FrameClock.Run(tick);
            var sw = Stopwatch.StartNew();
            Move? best;
            try
            {
                best = await Task.Run(() => new Engine().Best(pos, level));
                int wait = MinBotDelayMs - (int)sw.ElapsedMilliseconds;
                if (wait > 0) await Task.Delay(wait);
            }
            catch (Exception ex)
            {
                if (request == botRequest) { thinking = false; ShowSnack("Ошибка бота. Отмените ход или сдайтесь. " + ex.Message); UpdateUi(); }
                return;
            }
            if (request != botRequest || current != session || IsDisposed) return;
            thinking = false;
            if (best.HasValue) ApplyMove(best.Value, true);
            else UpdateUi();
        }

        void Undo()
        {
            if (session == null || !session.CanUndo || board.HasOverlay) return;
            botRequest++;
            thinking = false;
            session.Undo();
            board.Session = session;
            lastCaptureCounts = new[] { session.CapturedBy(Piece.White).Count, session.CapturedBy(Piece.Black).Count };
            UpdateUi();
            MaybeBotMove();
        }

        void Flip()
        {
            board.Flipped = !board.Flipped;
            board.Invalidate();
            Invalidate();
        }

        public void HandleKey(Keys key)
        {
            if (key == (Keys.Control | Keys.Z)) Undo();
            else if (key == Keys.F && !board.HasOverlay) Flip();
            else if (key == Keys.Escape && board.HasOverlay && session != null && !session.IsOver) board.CloseOverlay();
        }

        // ---------- Ничья, сдача, итог ----------

        int HumanSide { get { return session.BotSide != 0 ? -session.BotSide : session.Current.Side; } }

        void OfferDraw()
        {
            if (session == null || session.IsOver || thinking) return;
            string reason = session.DrawClaimReason;
            if (reason != null)
            {
                board.ShowOverlay(new DialogOverlay("Потребовать ничью?", reason + ". По правилам можно зафиксировать ничью.", "½")
                    .AddButton("Отмена", false, () => { })
                    .AddButton("Потребовать", true, () => { session.ClaimDraw(); AfterChange(); }));
                return;
            }
            if (session.Mode == GameMode.TwoPlayers)
            {
                int side = session.Current.Side;
                board.ShowOverlay(new DialogOverlay(GameSession.SideName(side, true) + " предлагают ничью",
                        GameSession.SideName(-side, true) + ", вы согласны?", "½")
                    .AddButton("Отклонить", false, () => ShowSnack("Ничья отклонена"))
                    .AddButton("Согласиться", true, () => { session.AgreeDraw(); AfterChange(); }));
                return;
            }
            // Бот соглашается, только если его позиция заметно хуже.
            var pos = session.Current;
            int score = new Engine().QuickScore(pos);
            int botScore = pos.Side == session.BotSide ? score : -score;
            if (botScore <= BotAcceptsDrawBelowCp) { session.AgreeDraw(); AfterChange(); }
            else ShowSnack("Бот отклонил ничью");
        }

        void ConfirmResign()
        {
            if (session == null || session.IsOver) return;
            int side = HumanSide;
            string who = session.Mode == GameMode.VsBot ? "Вы уверены?" : GameSession.SideName(side, true) + " признают поражение.";
            board.ShowOverlay(new DialogOverlay("Сдаться?", who, null)
                .AddButton("Отмена", false, () => { })
                .AddButton("Сдаться", true, () =>
                {
                    botRequest++;
                    thinking = false;
                    session.Resign(side);
                    AfterChange();
                }));
        }

        void ShowResult()
        {
            var r = session.Result;
            string badge = r.Winner == 0 ? "½" : Theme.Glyph(Piece.King, r.Winner == Piece.White);
            string title = r.Title;
            if (session.Mode == GameMode.VsBot && r.Winner != 0)
                title = r.Winner == HumanSide ? "Победа!" : "Поражение";
            string text = (title != r.Title ? r.Title + ". " : "") + r.Detail + ".";
            board.ShowOverlay(new DialogOverlay(title, text, badge)
                .AddButton("Доска", false, () => { })
                .AddButton("Ещё раз", true, () => StartGame(session.Mode, session.Level, HumanSideForRematch()))
                .AddButton("Меню", false, () => { if (MenuRequested != null) MenuRequested(this, EventArgs.Empty); }));
        }

        int HumanSideForRematch() { return session.BotSide != 0 ? -session.BotSide : Piece.White; }

        void ShowSnack(string text)
        {
            snackText = text;
            snackLeft = SnackbarSeconds;
            snack.Target = 1;
            FrameClock.Run(tick);
        }

        // ---------- Состояние и анимация ----------

        void UpdateUi()
        {
            if (session == null) return;
            bool busy = thinking || session.IsOver;
            board.InputEnabled = !busy && !session.IsBotTurn;
            undoButton.Enabled = session.CanUndo && !thinking;
            drawButton.Enabled = !busy;
            resignButton.Enabled = !session.IsOver;
            movesList.SetHistory(session.History);
            Invalidate();
        }

        bool Tick(float dt)
        {
            if (IsDisposed) return false;
            bool running = thinking;
            thinkingPhase += dt;
            foreach (var s in capturePop) { s.Step(dt); running |= !s.Settled; }
            if (snackLeft > 0)
            {
                snackLeft -= dt;
                if (snackLeft <= 0) snack.Target = 0;
                running = true;
            }
            snack.Step(dt);
            running |= !snack.Settled;
            Invalidate(PanelRect());
            return running;
        }

        // ---------- Раскладка ----------

        Rectangle PanelRect()
        {
            int pad = Theme.S(24);
            int boardSize = BoardSize();
            int x = pad + boardSize + pad;
            return new Rectangle(x, pad, Math.Max(Theme.S(260), Width - x - pad), Height - pad * 2);
        }

        int BoardSize()
        {
            int pad = Theme.S(24);
            return Math.Max(Theme.S(240), Math.Min(Height - pad * 2, Width - Theme.S(PanelWidth) - pad * 3));
        }

        const float StatusH = 52, PlayerH = 96, Gap = 12, ButtonH = 56;

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = Theme.S(24);
            int size = BoardSize();
            board.Bounds = new Rectangle(pad, (Height - size) / 2, size, size);

            var p = PanelRect();
            int gap = Theme.S(Gap), bh = Theme.S(ButtonH);
            int menuY = p.Bottom - bh;
            menuButton.Bounds = new Rectangle(p.X, menuY, p.Width, bh);
            int rowY = menuY - gap - bh;
            var row = new[] { undoButton, flipButton, drawButton, resignButton };
            int bw = (p.Width - gap * (row.Length - 1)) / row.Length;
            for (int i = 0; i < row.Length; i++) row[i].Bounds = new Rectangle(p.X + i * (bw + gap), rowY, bw, bh);

            int listTop = p.Y + Theme.S(StatusH) + gap + Theme.S(PlayerH) + gap;
            int listBottom = rowY - gap - Theme.S(PlayerH) - gap;
            movesList.Bounds = new Rectangle(p.X, listTop, p.Width, Math.Max(Theme.S(60), listBottom - listTop));
        }

        // ---------- Отрисовка панели ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(Theme.Background);
            if (session == null) return;
            var p = PanelRect();
            PaintStatus(g, new RectangleF(p.X, p.Y, p.Width, Theme.F(StatusH)));
            int topSide = board.Flipped ? Piece.White : Piece.Black;
            float topY = p.Y + Theme.F(StatusH) + Theme.F(Gap);
            PaintPlayer(g, new RectangleF(p.X, topY, p.Width, Theme.F(PlayerH)), topSide);
            PaintPlayer(g, new RectangleF(p.X, movesList.Bottom + Theme.F(Gap), p.Width, Theme.F(PlayerH)), -topSide);
            PaintSnackbar(g, p);
        }

        void PaintStatus(Graphics g, RectangleF r)
        {
            var pos = session.Current;
            string text;
            Color bg = Theme.SurfaceContainerHigh, fg = Theme.OnSurface;
            if (session.IsOver) { text = session.Result.Title + " · " + session.Result.Detail; bg = Theme.TertiaryContainer; fg = Theme.OnTertiaryContainer; }
            else if (pos.InCheck(pos.Side)) { text = "Шах! Ход " + GameSession.SideName(pos.Side, false); bg = Theme.ErrorContainer; fg = Theme.OnErrorContainer; }
            else if (thinking) text = "Бот думает…";
            else text = "Ход " + GameSession.SideName(pos.Side, false);
            if (!session.IsOver && session.DrawClaimReason != null) text += " · можно потребовать ничью";
            Theme.FillRound(g, bg, r, r.Height / 2);
            Theme.DrawText(g, text, Theme.Title(16), fg, RectangleF.Inflate(r, -Theme.F(20), 0), StringAlignment.Center, StringAlignment.Center);
        }

        string PlayerName(int side)
        {
            if (session.Mode == GameMode.TwoPlayers) return GameSession.SideName(side, true);
            return side == session.BotSide ? "Бот · " + LevelNames[(int)session.Level] : "Вы";
        }

        void PaintPlayer(Graphics g, RectangleF r, int side)
        {
            bool active = !session.IsOver && session.Current.Side == side;
            var bg = active ? Theme.SecondaryContainer : Theme.SurfaceContainer;
            var fg = active ? Theme.OnSecondaryContainer : Theme.OnSurface;
            Theme.FillRound(g, bg, r, Theme.F(active ? 28 : 20));

            float avatar = Theme.F(56);
            var center = new PointF(r.X + Theme.F(16) + avatar / 2, r.Y + r.Height / 2);
            using (var cookie = Theme.Cookie(center, avatar / 2, side == Piece.White ? 8 : 6, 0.07f, active ? thinkingPhase * 40 : 0))
            using (var b = new SolidBrush(active ? Theme.Primary : Theme.SurfaceContainerHighest))
                g.FillPath(b, cookie);
            Theme.DrawPiece(g, Piece.King * side, new RectangleF(center.X - avatar / 2, center.Y - avatar / 2, avatar, avatar), 1f, 0.8f);

            float tx = center.X + avatar / 2 + Theme.F(14);
            float tw = r.Right - tx - Theme.F(16);
            string name = PlayerName(side);
            if (active && thinking && side == session.BotSide) name += "  " + ThinkingDots();
            Theme.DrawText(g, name, Theme.Title(17), fg, new RectangleF(tx, r.Y + Theme.F(14), tw, Theme.F(26)), StringAlignment.Near, StringAlignment.Center);
            PaintCaptured(g, new RectangleF(tx, r.Y + Theme.F(46), tw, Theme.F(34)), side, fg);
        }

        string ThinkingDots()
        {
            int n = (int)(thinkingPhase * 3) % 4;
            return new string('•', n) + new string(' ', 3 - n);
        }

        /// <summary>Взятые стороной фигуры: значки, сгруппированные по виду, и перевес в материале.</summary>
        void PaintCaptured(Graphics g, RectangleF r, int side, Color fg)
        {
            var captured = session.CapturedBy(side);
            if (captured.Count == 0)
            {
                Theme.DrawText(g, "Взятых фигур нет", Theme.Body(13), Theme.WithAlpha(fg, 0.6f), r, StringAlignment.Near, StringAlignment.Center);
                return;
            }
            float size = r.Height * 1.3f;
            float step = size * 0.45f;
            float x = r.X - size * 0.22f;
            r = new RectangleF(r.X, r.Y + (r.Height - size) / 2, r.Width, size);
            var pop = capturePop[side == Piece.White ? 0 : 1];
            for (int i = 0; i < captured.Count; i++)
            {
                if (i > 0 && captured[i] != captured[i - 1]) x += size * 0.22f;
                bool newest = i == LatestCaptureIndex(side, captured);
                float scale = newest ? Math.Max(0.2f, pop.Value) : 1f;
                Theme.DrawPiece(g, captured[i] * -side, new RectangleF(x, r.Y, size, size), 1f, 0.82f * scale);
                x += step;
            }
            int balance = session.MaterialBalance() * side;
            if (balance > 0)
            {
                float chipH = Theme.F(24);
                var chip = new RectangleF(x + size * 0.6f, r.Y + (r.Height - chipH) / 2, Theme.F(42), chipH);
                Theme.FillRound(g, Theme.Primary, chip, chip.Height / 2);
                Theme.DrawText(g, "+" + balance, Theme.Label(13), Theme.OnPrimary, chip, StringAlignment.Center, StringAlignment.Center);
            }
        }

        int LatestCaptureIndex(int side, System.Collections.Generic.List<int> sorted)
        {
            var last = session.History.LastOrDefault(h => h.Captured != 0 && Math.Sign(h.Captured) == -side);
            return last == null ? -1 : sorted.LastIndexOf(Math.Abs(last.Captured));
        }

        void PaintSnackbar(Graphics g, Rectangle p)
        {
            if (snack.Value <= 0.01f || snackText == null) return;
            // Снекбар выезжает поверх строки статуса (ниже лежат дочерние элементы, которые бы его закрыли).
            float a = Math.Min(1, snack.Value);
            float h = Theme.F(StatusH);
            var r = new RectangleF(p.X, p.Y - Theme.F(12) * (1 - a), p.Width, h);
            Theme.FillRound(g, Theme.Background, r, Theme.F(12));
            Theme.FillRound(g, Theme.WithAlpha(Theme.OnSurface, a), r, Theme.F(12));
            Theme.DrawText(g, snackText, Theme.Body(15), Theme.WithAlpha(Theme.SurfaceContainer, a), RectangleF.Inflate(r, -Theme.F(16), 0), StringAlignment.Near, StringAlignment.Center);
        }
    }
}
