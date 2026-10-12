using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Chess.Rules
{
    public enum Difficulty { Beginner, Amateur, Pro }

    /// <summary>Параметры силы бота.</summary>
    public sealed class BotLevel
    {
        public int MaxDepth;
        public int TimeLimitMs;
        // Бот выбирает случайный ход среди тех, что хуже лучшего не более чем на столько сантипешек.
        public int RandomMarginCp;

        public static BotLevel For(Difficulty d)
        {
            switch (d)
            {
                case Difficulty.Beginner: return new BotLevel { MaxDepth = 1, TimeLimitMs = 1000, RandomMarginCp = 180 };
                case Difficulty.Amateur: return new BotLevel { MaxDepth = 3, TimeLimitMs = 1500, RandomMarginCp = 30 };
                default: return new BotLevel { MaxDepth = 8, TimeLimitMs = 1800, RandomMarginCp = 0 };
            }
        }
    }

    public sealed class Engine
    {
        public const int Mate = 100000;
        const int Inf = 1000000;
        const int MaxQuiescenceDepth = 8;
        const int MaxExtensionPly = 10;
        const int TimeCheckMask = 1023;
        static readonly int[] Val = { 0, 100, 320, 330, 500, 900, 20000 };

        static readonly int[] PawnT = {
             0,  0,  0,  0,  0,  0,  0,  0,
            50, 50, 50, 50, 50, 50, 50, 50,
            10, 10, 20, 30, 30, 20, 10, 10,
             5,  5, 10, 25, 25, 10,  5,  5,
             0,  0,  0, 20, 20,  0,  0,  0,
             5, -5,-10,  0,  0,-10, -5,  5,
             5, 10, 10,-20,-20, 10, 10,  5,
             0,  0,  0,  0,  0,  0,  0,  0 };
        static readonly int[] KnightT = {
            -50,-40,-30,-30,-30,-30,-40,-50,
            -40,-20,  0,  0,  0,  0,-20,-40,
            -30,  0, 10, 15, 15, 10,  0,-30,
            -30,  5, 15, 20, 20, 15,  5,-30,
            -30,  0, 15, 20, 20, 15,  0,-30,
            -30,  5, 10, 15, 15, 10,  5,-30,
            -40,-20,  0,  5,  5,  0,-20,-40,
            -50,-40,-30,-30,-30,-30,-40,-50 };
        static readonly int[] BishopT = {
            -20,-10,-10,-10,-10,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5, 10, 10,  5,  0,-10,
            -10,  5,  5, 10, 10,  5,  5,-10,
            -10,  0, 10, 10, 10, 10,  0,-10,
            -10, 10, 10, 10, 10, 10, 10,-10,
            -10,  5,  0,  0,  0,  0,  5,-10,
            -20,-10,-10,-10,-10,-10,-10,-20 };
        static readonly int[] RookT = {
             0,  0,  0,  0,  0,  0,  0,  0,
             5, 10, 10, 10, 10, 10, 10,  5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
            -5,  0,  0,  0,  0,  0,  0, -5,
             0,  0,  0,  5,  5,  0,  0,  0 };
        static readonly int[] QueenT = {
            -20,-10,-10, -5, -5,-10,-10,-20,
            -10,  0,  0,  0,  0,  0,  0,-10,
            -10,  0,  5,  5,  5,  5,  0,-10,
             -5,  0,  5,  5,  5,  5,  0, -5,
              0,  0,  5,  5,  5,  5,  0, -5,
            -10,  5,  5,  5,  5,  5,  0,-10,
            -10,  0,  5,  0,  0,  0,  0,-10,
            -20,-10,-10, -5, -5,-10,-10,-20 };
        static readonly int[] KingMidT = {
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -30,-40,-40,-50,-50,-40,-40,-30,
            -20,-30,-30,-40,-40,-30,-30,-20,
            -10,-20,-20,-20,-20,-20,-20,-10,
             20, 20,  0,  0,  0,  0, 20, 20,
             20, 30, 10,  0,  0, 10, 30, 20 };
        static readonly int[] KingEndT = {
            -50,-40,-30,-20,-20,-30,-40,-50,
            -30,-20,-10,  0,  0,-10,-20,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 30, 40, 40, 30,-10,-30,
            -30,-10, 20, 30, 30, 20,-10,-30,
            -30,-30,  0,  0,  0,  0,-30,-30,
            -50,-30,-30,-30,-30,-30,-30,-50 };
        static readonly int[][] Tables = { null, PawnT, KnightT, BishopT, RookT, QueenT, KingMidT };

        public static int PieceValue(int kind) { return kind == Piece.King ? 0 : Val[kind]; }

        readonly Random rnd = new Random();
        Stopwatch clock;
        int timeLimitMs;
        long nodes;
        bool aborted;

        /// <summary>Оценка с точки зрения стороны, которая ходит (в сантипешках).</summary>
        public static int Eval(Position p)
        {
            int heavy = 0;
            for (int i = 0; i < 64; i++)
            {
                int k = Math.Abs(p.B[i]);
                if (k >= Piece.Knight && k <= Piece.Queen) heavy += Val[k];
            }
            bool endgame = heavy <= 1600;
            int score = 0;
            for (int i = 0; i < 64; i++)
            {
                int v = p.B[i];
                if (v == 0) continue;
                int k = Math.Abs(v);
                int idx = v > 0 ? i : (i ^ 56);
                int[] t = (k == Piece.King && endgame) ? KingEndT : Tables[k];
                int s = Val[k] + t[idx];
                score += v > 0 ? s : -s;
            }
            return score * p.Side;
        }

        static int OrderScore(Position p, Move m)
        {
            int victim = Math.Abs(p.B[m.To]);
            int attacker = Math.Abs(p.B[m.From]);
            int s = 0;
            if (victim != 0) s += 10000 + Val[victim] * 10 - Val[attacker] / 10;
            if (m.Promo != 0) s += 9000 + Val[m.Promo];
            return s;
        }

        static List<Move> Ordered(Position p, List<Move> moves)
        {
            return moves.OrderByDescending(m => OrderScore(p, m)).ToList();
        }

        public Move? Best(Position p, BotLevel level)
        {
            var moves = p.Legal();
            if (moves.Count == 0) return null;
            clock = Stopwatch.StartNew();
            timeLimitMs = level.TimeLimitMs;
            aborted = false;
            // Перемешиваем, чтобы в равных позициях бот не играл одно и то же.
            moves = Ordered(p, moves.OrderBy(m => rnd.Next()).ToList());
            if (level.RandomMarginCp > 0) return PickWithMargin(p, moves, level);
            return IterativeDeepening(p, moves, level.MaxDepth);
        }

        Move PickWithMargin(Position p, List<Move> moves, BotLevel level)
        {
            var scored = new List<KeyValuePair<Move, int>>();
            foreach (var m in moves)
                scored.Add(new KeyValuePair<Move, int>(m, -Negamax(p.Make(m), level.MaxDepth - 1, -Inf, Inf, 1, false)));
            int best = scored.Max(s => s.Value);
            // Мат не упускаем даже на лёгком уровне.
            int margin = best >= Mate - 100 ? 0 : level.RandomMarginCp;
            var good = scored.Where(s => s.Value >= best - margin).ToList();
            return good[rnd.Next(good.Count)].Key;
        }

        Move IterativeDeepening(Position p, List<Move> moves, int maxDepth)
        {
            Move best = moves[0];
            for (int depth = 1; depth <= maxDepth; depth++)
            {
                int alpha = -Inf;
                Move depthBest = moves[0];
                foreach (var m in moves)
                {
                    int s = -Negamax(p.Make(m), depth - 1, -Inf, -alpha, 1, true);
                    if (aborted) break;
                    if (s > alpha) { alpha = s; depthBest = m; }
                }
                if (aborted) break;
                best = depthBest;
                if (alpha >= Mate - 100) break;
                // Лучший ход прошлой итерации проверяем первым — так отсечения работают лучше.
                moves.Remove(best);
                moves.Insert(0, best);
                if (clock.ElapsedMilliseconds * 3 > timeLimitMs) break;
            }
            return best;
        }

        bool OutOfTime(bool timed)
        {
            if (!timed) return false;
            if ((++nodes & TimeCheckMask) == 0 && clock.ElapsedMilliseconds > timeLimitMs) aborted = true;
            return aborted;
        }

        int Negamax(Position p, int depth, int alpha, int beta, int ply, bool timed)
        {
            if (OutOfTime(timed)) return 0;
            bool inCheck = p.InCheck(p.Side);
            // Продлеваем поиск под шахом, но ограниченно — иначе вечный шах уведёт в бесконечную рекурсию.
            if (inCheck && ply < MaxExtensionPly) depth++;
            if (depth <= 0) return Quiesce(p, alpha, beta, 0);
            var pseudo = new List<Move>();
            p.GenPseudo(pseudo, false);
            int legal = 0;
            foreach (var m in Ordered(p, pseudo))
            {
                var np = p.Make(m);
                if (np.InCheck(p.Side)) continue;
                legal++;
                int s = -Negamax(np, depth - 1, -beta, -alpha, ply + 1, timed);
                if (aborted) return 0;
                if (s >= beta) return beta;
                if (s > alpha) alpha = s;
            }
            if (legal == 0) return inCheck ? -Mate + ply : 0;
            return alpha;
        }

        int Quiesce(Position p, int alpha, int beta, int qd)
        {
            int stand = Eval(p);
            if (stand >= beta) return beta;
            if (stand > alpha) alpha = stand;
            if (qd >= MaxQuiescenceDepth) return alpha;
            var caps = new List<Move>();
            p.GenPseudo(caps, true);
            foreach (var m in Ordered(p, caps))
            {
                var np = p.Make(m);
                if (np.InCheck(p.Side)) continue;
                int s = -Quiesce(np, -beta, -alpha, qd + 1);
                if (s >= beta) return beta;
                if (s > alpha) alpha = s;
            }
            return alpha;
        }

        /// <summary>Короткий поиск для решения бота о предложенной ничьей.</summary>
        public int QuickScore(Position p)
        {
            clock = Stopwatch.StartNew();
            timeLimitMs = 400;
            aborted = false;
            return Negamax(p, 2, -Inf, Inf, 0, false);
        }
    }
}
