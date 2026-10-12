using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Chess.Rules;

namespace Chess.UI
{
    sealed class MainForm : Form
    {
        const int DwmUseImmersiveDarkMode = 20;
        const int DwmCaptionColor = 35;

        readonly MenuView menu = new MenuView();
        readonly GameView game = new GameView();

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr handle);

        public MainForm()
        {
            using (var g = CreateGraphics()) Theme.Scale = g.DpiX / 96f;
            Text = "Шахматы";
            BackColor = Theme.Background;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Theme.S(1280), Theme.S(820));
            MinimumSize = SizeFromClientSize(new Size(Theme.S(1000), Theme.S(680)));
            Icon = BuildIcon();

            menu.Dock = DockStyle.Fill;
            game.Dock = DockStyle.Fill;
            game.Visible = false;
            Controls.Add(menu);
            Controls.Add(game);

            menu.PlayBot += (s, e) => ShowGame(GameMode.VsBot, e.Level, e.HumanSide);
            menu.PlayTwo += (s, e) => ShowGame(GameMode.TwoPlayers, Difficulty.Pro, Piece.White);
            menu.ExitRequested += (s, e) => Close();
            game.MenuRequested += (s, e) => ShowMenu();
        }

        void ShowGame(GameMode mode, Difficulty level, int humanSide)
        {
            game.StartGame(mode, level, humanSide);
            game.Visible = true;
            menu.Visible = false;
            game.Focus();
        }

        void ShowMenu()
        {
            game.StopBot();
            menu.ShowMain();
            menu.Visible = true;
            game.Visible = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Тёмный заголовок окна в цвет фона (Windows 10 20H1+ / 11); на старых системах вызов просто не сработает.
            int dark = 1;
            int caption = ColorTranslator.ToWin32(Theme.Background);
            try
            {
                DwmSetWindowAttribute(Handle, DwmUseImmersiveDarkMode, ref dark, sizeof(int));
                DwmSetWindowAttribute(Handle, DwmCaptionColor, ref caption, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (game.Visible)
            {
                if (keyData == (Keys.Control | Keys.Z) || keyData == Keys.F || keyData == Keys.Escape)
                {
                    game.HandleKey(keyData);
                    return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        static Icon BuildIcon()
        {
            const int Size = 64;
            using (var bmp = new Bitmap(Size, Size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    Theme.Prepare(g);
                    g.Clear(Color.Transparent);
                    using (var cookie = Theme.Cookie(new PointF(Size / 2f, Size / 2f), Size / 2f - 1, 8, 0.06f, 0))
                    using (var b = new SolidBrush(Theme.Primary))
                        g.FillPath(b, cookie);
                    Theme.DrawPiece(g, -Piece.Knight, new RectangleF(4, 2, Size - 8, Size - 8), 1f, 1f);
                }
                IntPtr h = bmp.GetHicon();
                try { return (Icon)Icon.FromHandle(h).Clone(); }
                finally { DestroyIcon(h); }
            }
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch (EntryPointNotFoundException) { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
