using System;
using System.Drawing;
using System.Windows.Forms;
using Chess.Rules;

namespace Chess.UI
{
    sealed class PlayBotEventArgs : EventArgs
    {
        public Difficulty Level;
        public int HumanSide;
    }

    sealed class BufferedPanel : Panel
    {
        public BufferedPanel()
        {
            DoubleBuffered = true;
            BackColor = Theme.Background;
        }
    }

    /// <summary>Главное меню: игра с ботом (с выбором уровня и цвета), игра вдвоём, выход.</summary>
    sealed class MenuView : Control
    {
        const float ColumnWidth = 460;
        const float WideLayoutMin = 980;
        const float RotationDegPerSec = 6;

        readonly BufferedPanel mainPage = new BufferedPanel();
        readonly BufferedPanel botPage = new BufferedPanel();
        readonly SelectCard[] levelCards;
        readonly SegmentedButton colorPicker;
        readonly M3Button backButton;
        readonly M3Button startButton;
        readonly Spring enter = Spring.Bouncy(0);
        readonly Random rnd = new Random();
        readonly Func<float, bool> tick;
        float rotation;
        Difficulty level = Difficulty.Amateur;

        public event EventHandler<PlayBotEventArgs> PlayBot;
        public event EventHandler PlayTwo;
        public event EventHandler ExitRequested;

        public MenuView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Background;
            tick = Tick;

            var bot = new M3Button("Играть с ботом", Theme.IconRobot, ButtonStyle.Filled) { FontPx = 18 };
            var two = new M3Button("Вдвоём на одном экране", Theme.IconPeople, ButtonStyle.Tonal) { FontPx = 18 };
            var exit = new M3Button("Выход", Theme.IconExit, ButtonStyle.Outlined) { FontPx = 18 };
            bot.Click += delegate { ShowPage(botPage); };
            two.Click += delegate { if (PlayTwo != null) PlayTwo(this, EventArgs.Empty); };
            exit.Click += delegate { if (ExitRequested != null) ExitRequested(this, EventArgs.Empty); };
            mainPage.Controls.AddRange(new Control[] { bot, two, exit });

            levelCards = new[]
            {
                new SelectCard("Новичок", "Делает ошибки, можно поймать", "♟"),
                new SelectCard("Любитель", "Считает на пару ходов вперёд", "♞"),
                new SelectCard("Профи", "Глубокий расчёт, не прощает зевков", "♛"),
            };
            for (int i = 0; i < levelCards.Length; i++)
            {
                var d = (Difficulty)i;
                levelCards[i].Click += delegate { SelectLevel(d); };
                botPage.Controls.Add(levelCards[i]);
            }
            SelectLevel(level);

            colorPicker = new SegmentedButton(new[] { "Белые", "Случайно", "Чёрные" }, 0);
            var back = backButton = new M3Button("Назад", Theme.IconBack, ButtonStyle.Text) { FontPx = 16 };
            var start = startButton = new M3Button("Начать партию", Theme.IconPlay, ButtonStyle.Filled) { FontPx = 18 };
            back.Click += delegate { ShowPage(mainPage); };
            start.Click += delegate { StartBotGame(); };
            botPage.Controls.AddRange(new Control[] { colorPicker, back, start });
            botPage.Paint += PaintBotPageLabels;
            mainPage.Paint += PaintMainPageTitle;

            Controls.Add(mainPage);
            Controls.Add(botPage);
            botPage.Visible = false;
        }

        void SelectLevel(Difficulty d)
        {
            level = d;
            for (int i = 0; i < levelCards.Length; i++) levelCards[i].Selected = i == (int)d;
        }

        void StartBotGame()
        {
            int side = colorPicker.SelectedIndex == 0 ? Piece.White
                     : colorPicker.SelectedIndex == 2 ? Piece.Black
                     : (rnd.Next(2) == 0 ? Piece.White : Piece.Black);
            if (PlayBot != null) PlayBot(this, new PlayBotEventArgs { Level = level, HumanSide = side });
        }

        public void ShowMain() { ShowPage(mainPage); }

        void ShowPage(Panel page)
        {
            mainPage.Visible = page == mainPage;
            botPage.Visible = page == botPage;
            enter.Snap(1);
            enter.Target = 0;
            LayoutPages();
            FrameClock.Run(tick);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) FrameClock.Run(tick);
        }

        bool Tick(float dt)
        {
            if (IsDisposed || !Visible) return false;
            enter.Step(dt);
            rotation = (rotation + RotationDegPerSec * dt) % 360;
            LayoutPages();
            Invalidate(DecorationBounds());
            return true;
        }

        bool Wide { get { return Width >= Theme.S(WideLayoutMin); } }

        Rectangle ColumnBounds()
        {
            int w = Math.Min(Theme.S(ColumnWidth), Width - Theme.S(48));
            int x = Wide ? Theme.S(96) : (Width - w) / 2;
            return new Rectangle(x, 0, w, Height);
        }

        Rectangle DecorationBounds()
        {
            if (!Wide) return Rectangle.Empty;
            var col = ColumnBounds();
            int x = col.Right + Theme.S(32);
            return new Rectangle(x, 0, Width - x, Height);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            LayoutPages();
        }

        void LayoutPages()
        {
            var col = ColumnBounds();
            int offset = (int)(Theme.S(36) * enter.Value);
            int mainH = Theme.S(440), botH = Theme.S(600);
            mainPage.Bounds = new Rectangle(col.X, Math.Max(0, (Height - mainH) / 2) + offset, col.Width, mainH);
            botPage.Bounds = new Rectangle(col.X, Math.Max(0, (Height - botH) / 2) + offset, col.Width, botH);

            int w = col.Width, bh = Theme.S(64), gap = Theme.S(14);
            int y = Theme.S(196);
            foreach (Control c in mainPage.Controls) { c.Bounds = new Rectangle(0, y, w, bh); y += bh + gap; }

            y = Theme.S(4);
            backButton.Bounds = new Rectangle(-Theme.S(12), y, Theme.S(130), Theme.S(44));
            y = Theme.S(146);
            foreach (var card in levelCards) { card.Bounds = new Rectangle(0, y, w, Theme.S(84)); y += Theme.S(84) + Theme.S(10); }
            y += Theme.S(34);
            colorPicker.Bounds = new Rectangle(0, y, w, Theme.S(48));
            startButton.Bounds = new Rectangle(0, y + Theme.S(72), w, bh);
        }

        void PaintMainPageTitle(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            int w = mainPage.Width;
            Theme.DrawText(g, "Шахматы", Theme.Display(72), Theme.OnSurface, new RectangleF(0, 0, w, Theme.F(100)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, "Классическая игра в новом облике", Theme.Body(19), Theme.OnSurfaceVariant, new RectangleF(Theme.F(4), Theme.F(100), w, Theme.F(32)), StringAlignment.Near, StringAlignment.Center);
            var pill = new RectangleF(Theme.F(4), Theme.F(142), Theme.F(196), Theme.F(32));
            Theme.FillRound(g, Theme.TertiaryContainer, pill, pill.Height / 2);
            Theme.DrawText(g, "Правила ФИДЕ · 3 уровня", Theme.Label(13), Theme.OnTertiaryContainer, pill, StringAlignment.Center, StringAlignment.Center);
        }

        void PaintBotPageLabels(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            int w = botPage.Width;
            Theme.DrawText(g, "Игра с ботом", Theme.Headline(40), Theme.OnSurface, new RectangleF(0, Theme.F(50), w, Theme.F(56)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, "Сложность", Theme.Label(14), Theme.Primary, new RectangleF(Theme.F(2), Theme.F(116), w, Theme.F(22)), StringAlignment.Near, StringAlignment.Center);
            Theme.DrawText(g, "Ваш цвет", Theme.Label(14), Theme.Primary, new RectangleF(Theme.F(2), colorPicker.Top - Theme.F(28), w, Theme.F(22)), StringAlignment.Near, StringAlignment.Center);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.Prepare(g);
            g.Clear(Theme.Background);
            var deco = DecorationBounds();
            if (deco.Width <= 0) return;

            float size = Math.Min(deco.Width, deco.Height) * 0.78f;
            var center = new PointF(deco.X + deco.Width / 2f, deco.Y + deco.Height / 2f);
            using (var big = Theme.Cookie(center, size / 2, 12, 0.045f, rotation))
            using (var b = new SolidBrush(Theme.PrimaryContainer))
                g.FillPath(b, big);
            var small = new PointF(center.X + size * 0.36f, center.Y - size * 0.34f);
            using (var clover = Theme.Cookie(small, size * 0.17f, 4, 0.16f, -rotation * 2))
            using (var b = new SolidBrush(Theme.TertiaryContainer))
                g.FillPath(b, clover);
            var dot = new PointF(center.X - size * 0.40f, center.Y + size * 0.36f);
            using (var b = new SolidBrush(Theme.SecondaryContainer))
                g.FillEllipse(b, dot.X - size * 0.08f, dot.Y - size * 0.08f, size * 0.16f, size * 0.16f);

            var knight = new RectangleF(center.X - size * 0.32f, center.Y - size * 0.34f, size * 0.64f, size * 0.64f);
            Theme.DrawPiece(g, Piece.Knight, knight, 1f, 1f);
        }
    }
}
