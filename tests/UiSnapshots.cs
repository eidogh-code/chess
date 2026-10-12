using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Chess.Rules;
using Chess.UI;

namespace Chess.Tests
{
    /// <summary>Открывает окно за пределами экрана, проходит по экранам и сохраняет снимки в PNG.</summary>
    static class UiSnapshots
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        static string outDir;

        static object Field(object o, string name) { return o.GetType().GetField(name, Any).GetValue(o); }
        static void Call(object o, string name, params object[] args) { o.GetType().GetMethod(name, Any).Invoke(o, args); }

        static void Pump(int ms)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < ms) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
        }

        static void Snap(Form f, string name)
        {
            Pump(700);
            using (var bmp = new Bitmap(f.ClientSize.Width, f.ClientSize.Height))
            {
                foreach (Control c in f.Controls)
                {
                    if (!c.Visible) continue;
                    c.DrawToBitmap(bmp, c.Bounds);
                }
                bmp.Save(Path.Combine(outDir, name + ".png"), ImageFormat.Png);
            }
            Console.WriteLine("saved " + name);
        }

        static void Play(GameView game, params string[] sans)
        {
            var session = (GameSession)Field(game, "session");
            foreach (var san in sans)
            {
                var p = session.Current;
                var m = session.Legal.First(x => Notation.San(p, x).TrimEnd('+', '#') == san);
                Call(game, "ApplyMove", m, true);
                Pump(380);
            }
        }

        [STAThread]
        static int Main(string[] args)
        {
            outDir = args.Length > 0 ? args[0] : ".";
            Directory.CreateDirectory(outDir);
            Application.EnableVisualStyles();
            var form = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-4000, 0) };
            form.Show();
            var menu = (MenuView)Field(form, "menu");
            var game = (GameView)Field(form, "game");

            Snap(form, "1-menu");
            Call(menu, "ShowPage", Field(menu, "botPage"));
            Snap(form, "2-bot-setup");

            Call(form, "ShowGame", GameMode.TwoPlayers, Difficulty.Pro, Piece.White);
            Play(game, "e4", "d5", "exd5", "Фxd5", "Кc3", "Фa5", "d4", "Кf6");
            var board = (BoardControl)Field(game, "board");
            // Выделяем коня g1, чтобы показать подсказки ходов.
            board.GetType().GetField("selected", Any).SetValue(board, Position.SquareIndex("g1"));
            ((Spring)Field(board, "dots")).Snap(1);
            board.Invalidate();
            Snap(form, "3-game");

            Call(game, "StartGame", GameMode.TwoPlayers, Difficulty.Pro, Piece.White);
            Play(game, "f3", "e5", "g4", "Фh4");
            Snap(form, "4-mate");

            Call(game, "StartGame", GameMode.VsBot, Difficulty.Beginner, Piece.White);
            Pump(300);
            Play(game, "e4");
            Pump(2500);
            Snap(form, "5-vs-bot");

            Call(board, "ShowOverlay", new PromotionOverlay(Piece.White, k => { }));
            Snap(form, "6-promotion");

            form.Close();
            return 0;
        }
    }
}
