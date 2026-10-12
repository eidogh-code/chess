using System;
using System.Collections.Generic;

namespace Chess.Rules
{
    // Фигуры: 1 пешка, 2 конь, 3 слон, 4 ладья, 5 ферзь, 6 король. Знак: + белые, - чёрные.
    // Клетка = ряд * 8 + столбец, ряд 0 = восьмая горизонталь, столбец 0 = вертикаль "a".
    public static class Piece
    {
        public const int Pawn = 1, Knight = 2, Bishop = 3, Rook = 4, Queen = 5, King = 6;
        public const int White = 1, Black = -1;
    }

    public struct Move
    {
        public int From, To, Promo;
        public Move(int from, int to, int promo) { From = from; To = to; Promo = promo; }

        public bool SameAs(Move other) { return From == other.From && To == other.To && Promo == other.Promo; }
    }

    public sealed class Position
    {
        public int[] B = new int[64];
        public int Side = Piece.White;
        public bool WK, WQ, BK, BQ;
        public int Ep = -1;
        public int Half;      // полуходы без взятий и ходов пешками
        public int FullMove = 1;

        static readonly int[][] KnightD = { new[]{-2,-1}, new[]{-2,1}, new[]{-1,-2}, new[]{-1,2}, new[]{1,-2}, new[]{1,2}, new[]{2,-1}, new[]{2,1} };
        static readonly int[][] KingD = { new[]{-1,-1}, new[]{-1,0}, new[]{-1,1}, new[]{0,-1}, new[]{0,1}, new[]{1,-1}, new[]{1,0}, new[]{1,1} };
        static readonly int[][] BishopD = { new[]{-1,-1}, new[]{-1,1}, new[]{1,-1}, new[]{1,1} };
        static readonly int[][] RookD = { new[]{-1,0}, new[]{1,0}, new[]{0,-1}, new[]{0,1} };

        public Position Clone()
        {
            var p = (Position)MemberwiseClone();
            p.B = (int[])B.Clone();
            return p;
        }

        public static Position Start()
        {
            return FromFen("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
        }

        public static Position FromFen(string fen)
        {
            var parts = fen.Trim().Split(' ');
            var p = new Position();
            int sq = 0;
            foreach (char ch in parts[0])
            {
                if (ch == '/') continue;
                if (char.IsDigit(ch)) { sq += ch - '0'; continue; }
                int kind = "pnbrqk".IndexOf(char.ToLowerInvariant(ch)) + 1;
                if (kind == 0 || sq > 63) throw new FormatException("Неверная FEN: " + fen);
                p.B[sq++] = char.IsUpper(ch) ? kind : -kind;
            }
            if (sq != 64) throw new FormatException("Неверная FEN: " + fen);
            p.Side = parts.Length > 1 && parts[1] == "b" ? Piece.Black : Piece.White;
            string castle = parts.Length > 2 ? parts[2] : "-";
            p.WK = castle.Contains("K"); p.WQ = castle.Contains("Q");
            p.BK = castle.Contains("k"); p.BQ = castle.Contains("q");
            p.Ep = parts.Length > 3 && parts[3] != "-" ? SquareIndex(parts[3]) : -1;
            p.Half = parts.Length > 4 ? int.Parse(parts[4]) : 0;
            p.FullMove = parts.Length > 5 ? int.Parse(parts[5]) : 1;
            return p;
        }

        public static int SquareIndex(string name)
        {
            return (8 - (name[1] - '0')) * 8 + (name[0] - 'a');
        }

        public static string SquareName(int sq)
        {
            return ((char)('a' + sq % 8)).ToString() + (8 - sq / 8);
        }

        static bool On(int r, int c) { return r >= 0 && r < 8 && c >= 0 && c < 8; }

        public bool Attacked(int sq, int by)
        {
            int r = sq / 8, c = sq % 8;
            int pr = r + (by == Piece.White ? 1 : -1);
            for (int dc = -1; dc <= 1; dc += 2)
                if (On(pr, c + dc) && B[pr * 8 + c + dc] == by) return true;
            foreach (var d in KnightD)
                if (On(r + d[0], c + d[1]) && B[(r + d[0]) * 8 + c + d[1]] == Piece.Knight * by) return true;
            foreach (var d in KingD)
                if (On(r + d[0], c + d[1]) && B[(r + d[0]) * 8 + c + d[1]] == Piece.King * by) return true;
            if (SliderHits(r, c, BishopD, Piece.Bishop * by, Piece.Queen * by)) return true;
            if (SliderHits(r, c, RookD, Piece.Rook * by, Piece.Queen * by)) return true;
            return false;
        }

        bool SliderHits(int r, int c, int[][] dirs, int slider, int queen)
        {
            foreach (var d in dirs)
            {
                int rr = r + d[0], cc = c + d[1];
                while (On(rr, cc))
                {
                    int v = B[rr * 8 + cc];
                    if (v != 0) { if (v == slider || v == queen) return true; break; }
                    rr += d[0]; cc += d[1];
                }
            }
            return false;
        }

        public int KingSq(int side)
        {
            for (int i = 0; i < 64; i++) if (B[i] == Piece.King * side) return i;
            return -1;
        }

        public bool InCheck(int side)
        {
            int k = KingSq(side);
            return k >= 0 && Attacked(k, -side);
        }

        public void GenPseudo(List<Move> list, bool capturesOnly)
        {
            for (int sq = 0; sq < 64; sq++)
            {
                int v = B[sq] * Side;
                if (v <= 0) continue;
                int r = sq / 8, c = sq % 8;
                switch (v)
                {
                    case Piece.Pawn: GenPawn(list, sq, r, c, capturesOnly); break;
                    case Piece.Knight: GenStep(list, sq, r, c, KnightD, capturesOnly); break;
                    case Piece.Bishop: GenSlide(list, sq, r, c, BishopD, capturesOnly); break;
                    case Piece.Rook: GenSlide(list, sq, r, c, RookD, capturesOnly); break;
                    case Piece.Queen: GenSlide(list, sq, r, c, BishopD, capturesOnly); GenSlide(list, sq, r, c, RookD, capturesOnly); break;
                    case Piece.King: GenStep(list, sq, r, c, KingD, capturesOnly); if (!capturesOnly) GenCastle(list); break;
                }
            }
        }

        static void AddPawn(List<Move> list, int from, int to)
        {
            int tr = to / 8;
            if (tr == 0 || tr == 7)
            {
                list.Add(new Move(from, to, Piece.Queen));
                list.Add(new Move(from, to, Piece.Rook));
                list.Add(new Move(from, to, Piece.Bishop));
                list.Add(new Move(from, to, Piece.Knight));
            }
            else list.Add(new Move(from, to, 0));
        }

        void GenPawn(List<Move> list, int sq, int r, int c, bool capOnly)
        {
            int dir = Side == Piece.White ? -1 : 1;
            int startRow = Side == Piece.White ? 6 : 1;
            int r1 = r + dir;
            if (!On(r1, c)) return;
            bool promoRow = r1 == 0 || r1 == 7;
            if (B[r1 * 8 + c] == 0 && (!capOnly || promoRow))
            {
                AddPawn(list, sq, r1 * 8 + c);
                int r2 = r + 2 * dir;
                if (!capOnly && r == startRow && B[r2 * 8 + c] == 0) list.Add(new Move(sq, r2 * 8 + c, 0));
            }
            for (int dc = -1; dc <= 1; dc += 2)
            {
                if (!On(r1, c + dc)) continue;
                int t = r1 * 8 + c + dc;
                if (B[t] * Side < 0 || t == Ep) AddPawn(list, sq, t);
            }
        }

        void GenStep(List<Move> list, int sq, int r, int c, int[][] dirs, bool capOnly)
        {
            foreach (var d in dirs)
            {
                int rr = r + d[0], cc = c + d[1];
                if (!On(rr, cc)) continue;
                int t = rr * 8 + cc;
                if (B[t] * Side > 0) continue;
                if (capOnly && B[t] == 0) continue;
                list.Add(new Move(sq, t, 0));
            }
        }

        void GenSlide(List<Move> list, int sq, int r, int c, int[][] dirs, bool capOnly)
        {
            foreach (var d in dirs)
            {
                int rr = r + d[0], cc = c + d[1];
                while (On(rr, cc))
                {
                    int t = rr * 8 + cc;
                    if (B[t] * Side > 0) break;
                    if (B[t] != 0) { list.Add(new Move(sq, t, 0)); break; }
                    if (!capOnly) list.Add(new Move(sq, t, 0));
                    rr += d[0]; cc += d[1];
                }
            }
        }

        // Рокировка: король и ладья не ходили, между ними пусто, король не под шахом
        // и не проходит через битое поле и не встаёт на него.
        void GenCastle(List<Move> list)
        {
            if (Side == Piece.White)
            {
                if (B[60] != Piece.King || Attacked(60, Piece.Black)) return;
                if (WK && B[61] == 0 && B[62] == 0 && B[63] == Piece.Rook && !Attacked(61, Piece.Black) && !Attacked(62, Piece.Black)) list.Add(new Move(60, 62, 0));
                if (WQ && B[59] == 0 && B[58] == 0 && B[57] == 0 && B[56] == Piece.Rook && !Attacked(59, Piece.Black) && !Attacked(58, Piece.Black)) list.Add(new Move(60, 58, 0));
            }
            else
            {
                if (B[4] != -Piece.King || Attacked(4, Piece.White)) return;
                if (BK && B[5] == 0 && B[6] == 0 && B[7] == -Piece.Rook && !Attacked(5, Piece.White) && !Attacked(6, Piece.White)) list.Add(new Move(4, 6, 0));
                if (BQ && B[3] == 0 && B[2] == 0 && B[1] == 0 && B[0] == -Piece.Rook && !Attacked(3, Piece.White) && !Attacked(2, Piece.White)) list.Add(new Move(4, 2, 0));
            }
        }

        public bool IsEnPassant(Move m)
        {
            return Math.Abs(B[m.From]) == Piece.Pawn && m.To == Ep && B[m.To] == 0;
        }

        public static bool IsCastle(int piece, Move m)
        {
            return Math.Abs(piece) == Piece.King && Math.Abs(m.To - m.From) == 2;
        }

        /// <summary>Фигура, которую забирает ход (со знаком), или 0.</summary>
        public int CapturedBy(Move m)
        {
            if (IsEnPassant(m)) return -Side * Piece.Pawn;
            return B[m.To];
        }

        /// <summary>Клетка, на которой стоит забираемая фигура (отличается при взятии на проходе).</summary>
        public int CaptureSquare(Move m)
        {
            return IsEnPassant(m) ? m.To + (Side == Piece.White ? 8 : -8) : m.To;
        }

        public Position Make(Move m)
        {
            var p = Clone();
            int piece = B[m.From];
            int captured = CapturedBy(m);
            int kind = Math.Abs(piece);
            if (IsEnPassant(m)) p.B[CaptureSquare(m)] = 0;
            p.B[m.To] = m.Promo != 0 ? m.Promo * Side : piece;
            p.B[m.From] = 0;
            if (IsCastle(piece, m))
            {
                int rookFrom, rookTo;
                CastleRook(m.To, out rookFrom, out rookTo);
                p.B[rookTo] = p.B[rookFrom];
                p.B[rookFrom] = 0;
            }
            if (m.From == 60 || m.To == 60) { p.WK = false; p.WQ = false; }
            if (m.From == 63 || m.To == 63) p.WK = false;
            if (m.From == 56 || m.To == 56) p.WQ = false;
            if (m.From == 4 || m.To == 4) { p.BK = false; p.BQ = false; }
            if (m.From == 7 || m.To == 7) p.BK = false;
            if (m.From == 0 || m.To == 0) p.BQ = false;
            p.Ep = (kind == Piece.Pawn && Math.Abs(m.To - m.From) == 16) ? (m.From + m.To) / 2 : -1;
            p.Half = (kind == Piece.Pawn || captured != 0) ? 0 : Half + 1;
            if (Side == Piece.Black) p.FullMove = FullMove + 1;
            p.Side = -Side;
            return p;
        }

        public static void CastleRook(int kingTo, out int rookFrom, out int rookTo)
        {
            switch (kingTo)
            {
                case 62: rookFrom = 63; rookTo = 61; break;
                case 58: rookFrom = 56; rookTo = 59; break;
                case 6: rookFrom = 7; rookTo = 5; break;
                default: rookFrom = 0; rookTo = 3; break;
            }
        }

        public List<Move> Legal()
        {
            var pseudo = new List<Move>();
            GenPseudo(pseudo, false);
            var res = new List<Move>();
            foreach (var m in pseudo)
                if (!Make(m).InCheck(Side)) res.Add(m);
            return res;
        }

        // Взятие на проходе учитывается при сравнении позиций, только если его действительно можно сделать.
        bool EnPassantPlayable()
        {
            if (Ep < 0) return false;
            foreach (var m in Legal())
                if (m.To == Ep && Math.Abs(B[m.From]) == Piece.Pawn) return true;
            return false;
        }

        /// <summary>Ключ для правила повторения: фигуры, очередь хода, права рокировки и взятия на проходе.</summary>
        public string Key()
        {
            var chars = new char[70];
            for (int i = 0; i < 64; i++) chars[i] = (char)('A' + B[i] + 6);
            chars[64] = Side == Piece.White ? 'w' : 'b';
            chars[65] = WK ? 'K' : '-';
            chars[66] = WQ ? 'Q' : '-';
            chars[67] = BK ? 'k' : '-';
            chars[68] = BQ ? 'q' : '-';
            chars[69] = EnPassantPlayable() ? (char)('0' + Ep) : '-';
            return new string(chars);
        }

        /// <summary>
        /// Мёртвая позиция по недостатку материала: король против короля, короля с конём,
        /// или только слоны (у любой стороны), стоящие на полях одного цвета.
        /// </summary>
        public bool InsufficientMaterial()
        {
            int knights = 0, bishops = 0;
            bool lightBishop = false, darkBishop = false;
            for (int i = 0; i < 64; i++)
            {
                int k = Math.Abs(B[i]);
                if (k == Piece.Pawn || k == Piece.Rook || k == Piece.Queen) return false;
                if (k == Piece.Knight) knights++;
                if (k == Piece.Bishop)
                {
                    bishops++;
                    if ((i / 8 + i % 8) % 2 == 0) lightBishop = true; else darkBishop = true;
                }
            }
            if (knights + bishops == 0) return true;
            if (knights == 1 && bishops == 0) return true;
            return knights == 0 && !(lightBishop && darkBishop);
        }
    }
}
