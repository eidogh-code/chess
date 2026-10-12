using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess.Rules
{
    public enum GameMode { VsBot, TwoPlayers }

    public sealed class GameResult
    {
        public string Title;
        public string Detail;
        public int Winner;   // 1 белые, -1 чёрные, 0 ничья
    }

    public sealed class HistoryEntry
    {
        public Position P;
        public Move? Move;
        public string San;
        public int Captured;   // взятая этим ходом фигура (со знаком) или 0
        public string Key;
    }

    /// <summary>Партия: история ходов, правила окончания игры по правилам ФИДЕ.</summary>
    public sealed class GameSession
    {
        public const int FiftyMoveHalfMoves = 100;
        public const int SeventyFiveMoveHalfMoves = 150;

        public readonly GameMode Mode;
        public readonly Difficulty Level;
        public readonly int BotSide;   // 0, если играют двое
        readonly List<HistoryEntry> history = new List<HistoryEntry>();

        public List<Move> Legal { get; private set; }
        public GameResult Result { get; private set; }

        public GameSession(GameMode mode, Difficulty level, int botSide)
        {
            Mode = mode;
            Level = level;
            BotSide = mode == GameMode.VsBot ? botSide : 0;
            var start = Position.Start();
            history.Add(new HistoryEntry { P = start, Key = start.Key() });
            Refresh();
        }

        public Position Current { get { return history[history.Count - 1].P; } }
        public HistoryEntry Last { get { return history[history.Count - 1]; } }
        public IList<HistoryEntry> History { get { return history.AsReadOnly(); } }
        public bool IsOver { get { return Result != null; } }
        public bool IsBotTurn { get { return !IsOver && BotSide != 0 && Current.Side == BotSide; } }

        public static string SideName(int side, bool nominative)
        {
            if (nominative) return side == Piece.White ? "Белые" : "Чёрные";
            return side == Piece.White ? "белых" : "чёрных";
        }

        public HistoryEntry Apply(Move m)
        {
            if (IsOver) throw new InvalidOperationException("Партия уже окончена.");
            var p = Current;
            if (!Legal.Any(l => l.SameAs(m))) throw new ArgumentException("Недопустимый ход.");
            var np = p.Make(m);
            var entry = new HistoryEntry { P = np, Move = m, San = Notation.San(p, m), Captured = p.CapturedBy(m), Key = np.Key() };
            history.Add(entry);
            Refresh();
            return entry;
        }

        /// <summary>Отменяет ход; против бота — до ближайшей позиции, где ходит человек.</summary>
        public bool Undo()
        {
            if (history.Count <= 1) return false;
            history.RemoveAt(history.Count - 1);
            if (BotSide != 0)
                while (history.Count > 1 && Current.Side == BotSide) history.RemoveAt(history.Count - 1);
            Result = null;
            Refresh();
            return true;
        }

        public bool CanUndo
        {
            get
            {
                if (history.Count <= 1) return false;
                if (BotSide == 0) return true;
                // Против бота есть смысл отменять, только если человек уже сделал ход.
                return history.Skip(1).Any(h => -h.P.Side != BotSide);
            }
        }

        int RepetitionCount()
        {
            string key = Last.Key;
            return history.Count(h => h.Key == key);
        }

        void Refresh()
        {
            Legal = Current.Legal();
            Result = AutomaticResult();
        }

        // Окончания, которые наступают сами, без заявления игрока.
        GameResult AutomaticResult()
        {
            var p = Current;
            if (Legal.Count == 0)
            {
                if (p.InCheck(p.Side))
                    return new GameResult { Title = "Мат", Detail = "Победили " + SideName(-p.Side, true).ToLowerInvariant(), Winner = -p.Side };
                return new GameResult { Title = "Пат", Detail = "Ничья: у " + SideName(p.Side, false) + " нет ходов", Winner = 0 };
            }
            if (p.InsufficientMaterial())
                return new GameResult { Title = "Ничья", Detail = "Ни одна из сторон не может поставить мат", Winner = 0 };
            if (RepetitionCount() >= 5)
                return new GameResult { Title = "Ничья", Detail = "Позиция повторилась пять раз", Winner = 0 };
            if (p.Half >= SeventyFiveMoveHalfMoves)
                return new GameResult { Title = "Ничья", Detail = "75 ходов без взятий и ходов пешками", Winner = 0 };
            return null;
        }

        /// <summary>Причина, по которой игрок вправе потребовать ничью, или null.</summary>
        public string DrawClaimReason
        {
            get
            {
                if (IsOver) return null;
                if (RepetitionCount() >= 3) return "Позиция повторилась три раза";
                if (Current.Half >= FiftyMoveHalfMoves) return "50 ходов без взятий и ходов пешками";
                return null;
            }
        }

        public void ClaimDraw()
        {
            string reason = DrawClaimReason;
            if (reason == null) throw new InvalidOperationException("Нет оснований требовать ничью.");
            Result = new GameResult { Title = "Ничья", Detail = reason, Winner = 0 };
        }

        public void AgreeDraw()
        {
            if (IsOver) return;
            Result = new GameResult { Title = "Ничья", Detail = "По соглашению сторон", Winner = 0 };
        }

        public void Resign(int side)
        {
            if (IsOver) return;
            Result = new GameResult { Title = SideName(side, true) + " сдались", Detail = "Победили " + SideName(-side, true).ToLowerInvariant(), Winner = -side };
        }

        /// <summary>Фигуры, взятые стороной side (виды фигур, по убыванию ценности).</summary>
        public List<int> CapturedBy(int side)
        {
            return history.Skip(1)
                .Where(h => h.Captured != 0 && Math.Sign(h.Captured) == -side)
                .Select(h => Math.Abs(h.Captured))
                .OrderByDescending(k => Engine.PieceValue(k))
                .ToList();
        }

        // Классическая шкала ценности: пешка 1, конь и слон 3, ладья 5, ферзь 9.
        static readonly int[] Points = { 0, 1, 3, 3, 5, 9, 0 };

        /// <summary>Материальный перевес белых в пешках (с учётом превращений).</summary>
        public int MaterialBalance()
        {
            int sum = 0;
            foreach (int v in Current.B)
                if (v != 0) sum += Math.Sign(v) * Points[Math.Abs(v)];
            return sum;
        }
    }
}
