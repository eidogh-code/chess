using System;
using System.Collections.Generic;
using System.Linq;

namespace Chess.Rules
{
    /// <summary>Краткая алгебраическая нотация с русскими обозначениями фигур (Кр, Ф, Л, С, К).</summary>
    public static class Notation
    {
        static readonly string[] Letters = { "", "", "К", "С", "Л", "Ф", "Кр" };

        public static string Letter(int kind) { return Letters[kind]; }

        public static string San(Position p, Move m)
        {
            int piece = p.B[m.From];
            int kind = Math.Abs(piece);
            var after = p.Make(m);
            string suffix = "";
            if (after.InCheck(after.Side)) suffix = after.Legal().Count == 0 ? "#" : "+";

            if (Position.IsCastle(piece, m))
                return (m.To % 8 == 6 ? "0-0" : "0-0-0") + suffix;

            bool capture = p.CapturedBy(m) != 0;
            string to = Position.SquareName(m.To);
            if (kind == Piece.Pawn)
            {
                string s = capture ? Position.SquareName(m.From).Substring(0, 1) + "x" + to : to;
                if (m.Promo != 0) s += "=" + Letters[m.Promo];
                return s + suffix;
            }
            return Letters[kind] + Disambiguation(p, m, piece) + (capture ? "x" : "") + to + suffix;
        }

        // Если такая же фигура может пойти на то же поле, добавляем вертикаль, горизонталь или обе.
        static string Disambiguation(Position p, Move m, int piece)
        {
            var rivals = p.Legal().Where(o => o.To == m.To && o.From != m.From && p.B[o.From] == piece).ToList();
            if (rivals.Count == 0) return "";
            string from = Position.SquareName(m.From);
            bool sameFile = rivals.Any(o => o.From % 8 == m.From % 8);
            bool sameRank = rivals.Any(o => o.From / 8 == m.From / 8);
            if (!sameFile) return from.Substring(0, 1);
            if (!sameRank) return from.Substring(1, 1);
            return from;
        }
    }
}
