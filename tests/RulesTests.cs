using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Rules;

namespace Chess.Tests
{
    static class RulesTests
    {
        static int failed, passed;

        static void Check(string name, bool ok, string detail)
        {
            if (ok) { passed++; return; }
            failed++;
            Console.WriteLine("FAIL: " + name + (detail != null ? " - " + detail : ""));
        }

        static void Equal<T>(string name, T expected, T actual)
        {
            Check(name, Equals(expected, actual), "expected " + expected + ", got " + actual);
        }

        static long Perft(Position p, int depth)
        {
            if (depth == 0) return 1;
            long n = 0;
            foreach (var m in p.Legal()) n += Perft(p.Make(m), depth - 1);
            return n;
        }

        static Move Find(GameSession s, string san)
        {
            var p = s.Current;
            foreach (var m in s.Legal)
                if (Notation.San(p, m).TrimEnd('+', '#') == san) return m;
            throw new ArgumentException("No move " + san);
        }

        static void Play(GameSession s, params string[] sans)
        {
            foreach (var san in sans) s.Apply(Find(s, san));
        }

        static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Эталонные значения perft (chessprogramming.org/Perft_Results).
            Equal("perft start 4", 197281L, Perft(Position.Start(), 4));
            Equal("perft kiwipete 3", 97862L, Perft(Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"), 3));
            Equal("perft pos3 4", 43238L, Perft(Position.FromFen("8/2p5/3p4/KP5r/1R3p1k/8/4P1P1/8 w - - 0 1"), 4));
            Equal("perft pos4 3", 9467L, Perft(Position.FromFen("r3k2r/Pppp1ppp/1b3nbN/nP6/BBP1P3/q4N2/Pp1P2PP/R2Q1RK1 w kq - 0 1"), 3));
            Equal("perft pos5 3", 62379L, Perft(Position.FromFen("rnbq1k1r/pp1Pbppp/2p5/8/2B5/8/PPP1NnPP/RNBQK2R w KQ - 1 8"), 3));

            // Мёртвые позиции.
            Check("K vs K", Position.FromFen("8/8/4k3/8/8/4K3/8/8 w - - 0 1").InsufficientMaterial(), null);
            Check("KN vs K", Position.FromFen("8/8/4k3/8/8/4KN2/8/8 w - - 0 1").InsufficientMaterial(), null);
            Check("KB vs KB same colour", Position.FromFen("8/8/3bk3/8/8/4K3/5B2/8 w - - 0 1").InsufficientMaterial(), null);
            Check("KB vs KB opposite colour is playable", !Position.FromFen("8/8/2b1k3/8/8/4K3/5B2/8 w - - 0 1").InsufficientMaterial(), null);
            Check("KNN vs K is playable", !Position.FromFen("8/8/4k3/8/8/4KNN1/8/8 w - - 0 1").InsufficientMaterial(), null);
            Check("KR vs K is playable", !Position.FromFen("8/8/4k3/8/8/4KR2/8/8 w - - 0 1").InsufficientMaterial(), null);

            // Право взятия на проходе влияет на сравнение позиций, только если взятие возможно.
            Equal("unplayable ep ignored in key",
                Position.FromFen("4k3/8/8/8/4P3/8/8/4K3 b - - 0 1").Key(),
                Position.FromFen("4k3/8/8/8/4P3/8/8/4K3 b - e3 0 1").Key());
            Check("playable ep counted in key",
                Position.FromFen("4k3/8/8/8/3pP3/8/8/4K3 b - e3 0 1").Key() != Position.FromFen("4k3/8/8/8/3pP3/8/8/4K3 b - - 0 1").Key(), null);

            // Нотация.
            var nb = Position.FromFen("4k3/8/8/8/8/5N2/8/1N2K3 w - - 0 1");
            Equal("SAN file disambiguation", "Кbd2", Notation.San(nb, new Move(Position.SquareIndex("b1"), Position.SquareIndex("d2"), 0)));
            var rr = Position.FromFen("4k3/8/8/R7/8/8/8/R3K3 w - - 0 1");
            Equal("SAN rank disambiguation", "Л5a3", Notation.San(rr, new Move(Position.SquareIndex("a5"), Position.SquareIndex("a3"), 0)));
            var promo = Position.FromFen("8/P3k3/8/8/8/8/8/4K3 w - - 0 1");
            Equal("SAN promotion", "a8=Ф", Notation.San(promo, new Move(Position.SquareIndex("a7"), Position.SquareIndex("a8"), Piece.Queen)));
            var castle = Position.FromFen("4k3/8/8/8/8/8/8/R3K2R w KQ - 0 1");
            Equal("SAN castle", "0-0", Notation.San(castle, new Move(60, 62, 0)));

            // Детский мат дураку.
            var fool = new GameSession(GameMode.TwoPlayers, Difficulty.Pro, 0);
            Play(fool, "f3", "e5", "g4", "Фh4");
            Check("fool's mate", fool.Result != null && fool.Result.Winner == Piece.Black && fool.Result.Title == "Мат", null);
            Equal("fool's mate SAN", "Фh4#", fool.Last.San);

            // Троекратное повторение — по заявлению, пятикратное — автоматически.
            var rep = new GameSession(GameMode.TwoPlayers, Difficulty.Pro, 0);
            Play(rep, "Кf3", "Кf6", "Кg1", "Кg8", "Кf3", "Кf6", "Кg1", "Кg8");
            Check("threefold claimable", rep.DrawClaimReason != null && !rep.IsOver, null);
            Play(rep, "Кf3", "Кf6", "Кg1", "Кg8", "Кf3", "Кf6", "Кg1", "Кg8");
            Check("fivefold automatic", rep.IsOver && rep.Result.Winner == 0, null);

            // Взятые фигуры и материал.
            var cap = new GameSession(GameMode.TwoPlayers, Difficulty.Pro, 0);
            Play(cap, "e4", "d5", "exd5", "Фxd5");
            Equal("white captured", "1", string.Join(",", cap.CapturedBy(Piece.White)));
            Equal("black captured", "1", string.Join(",", cap.CapturedBy(Piece.Black)));
            Equal("material even", 0, cap.MaterialBalance());

            // Взятие на проходе снимает пешку и записывается в взятые.
            var ep = new GameSession(GameMode.TwoPlayers, Difficulty.Pro, 0);
            Play(ep, "e4", "a6", "e5", "d5", "exd6");
            Equal("ep removes pawn", 0, ep.Current.B[Position.SquareIndex("d5")]);
            Equal("ep counted as capture", 1, ep.CapturedBy(Piece.White).Count);

            // Отмена против бота откатывает до хода человека.
            var bot = new GameSession(GameMode.VsBot, Difficulty.Beginner, Piece.Black);
            Play(bot, "e4", "e5");
            bot.Undo();
            Equal("undo vs bot", 1, bot.History.Count);

            // Сдача.
            var res = new GameSession(GameMode.TwoPlayers, Difficulty.Pro, 0);
            res.Resign(Piece.White);
            Check("resign", res.IsOver && res.Result.Winner == Piece.Black, null);

            // Бот находит мат в один ход на любом уровне.
            foreach (Difficulty d in Enum.GetValues(typeof(Difficulty)))
            {
                var mateIn1 = Position.FromFen("6k1/5ppp/8/8/8/8/5PPP/3R2K1 w - - 0 1");
                var best = new Engine().Best(mateIn1, BotLevel.For(d));
                Check("mate in 1 (" + d + ")", best.HasValue && Notation.San(mateIn1, best.Value) == "Лd8#", best.HasValue ? Notation.San(mateIn1, best.Value) : "null");
            }

            // Профи укладывается в лимит времени (с запасом).
            var sw = System.Diagnostics.Stopwatch.StartNew();
            new Engine().Best(Position.FromFen("r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1"), BotLevel.For(Difficulty.Pro));
            Check("pro respects time limit", sw.ElapsedMilliseconds < 3000, sw.ElapsedMilliseconds + " ms");

            Console.WriteLine("Passed: " + passed + ", failed: " + failed);
            return failed == 0 ? 0 : 1;
        }
    }
}
