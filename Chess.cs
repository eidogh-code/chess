using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

// Фигуры: 1 пешка, 2 конь, 3 слон, 4 ладья, 5 ферзь, 6 король. Знак: + белые, - чёрные.
// Клетка = ряд * 8 + столбец, ряд 0 = восьмая горизонталь.
struct Move
{
    public int From, To, Promo;
    public Move(int f, int t, int p) { From = f; To = t; Promo = p; }
}

class Position
{
    public int[] B = new int[64];
    public int Side = 1;
    public bool WK, WQ, BK, BQ;
    public int Ep = -1;
    public int Half;

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
        var p = new Position();
        int[] back = { 4, 2, 3, 5, 6, 3, 2, 4 };
        for (int c = 0; c < 8; c++)
        {
            p.B[c] = -back[c];
            p.B[8 + c] = -1;
            p.B[48 + c] = 1;
            p.B[56 + c] = back[c];
        }
        p.WK = p.WQ = p.BK = p.BQ = true;
        return p;
    }

    static bool On(int r, int c) { return r >= 0 && r < 8 && c >= 0 && c < 8; }

    public bool Attacked(int sq, int by)
    {
        int r = sq / 8, c = sq % 8;
        int pr = r + (by == 1 ? 1 : -1);
        for (int dc = -1; dc <= 1; dc += 2)
            if (On(pr, c + dc) && B[pr * 8 + c + dc] == by) return true;
        foreach (var d in KnightD)
            if (On(r + d[0], c + d[1]) && B[(r + d[0]) * 8 + c + d[1]] == 2 * by) return true;
        foreach (var d in KingD)
            if (On(r + d[0], c + d[1]) && B[(r + d[0]) * 8 + c + d[1]] == 6 * by) return true;
        if (SliderHits(r, c, BishopD, 3 * by, 5 * by)) return true;
        if (SliderHits(r, c, RookD, 4 * by, 5 * by)) return true;
        return false;
    }

    bool SliderHits(int r, int c, int[][] dirs, int a, int q)
    {
        foreach (var d in dirs)
        {
            int rr = r + d[0], cc = c + d[1];
            while (On(rr, cc))
            {
                int v = B[rr * 8 + cc];
                if (v != 0) { if (v == a || v == q) return true; break; }
                rr += d[0]; cc += d[1];
            }
        }
        return false;
    }

    public int KingSq(int side)
    {
        for (int i = 0; i < 64; i++) if (B[i] == 6 * side) return i;
        return -1;
    }

    public bool InCheck(int side)
    {
        int k = KingSq(side);
        return k >= 0 && Attacked(k, -side);
    }

    public void GenPseudo(List<Move> list, bool capturesOnly)
    {
        int s = Side;
        for (int sq = 0; sq < 64; sq++)
        {
            int v = B[sq] * s;
            if (v <= 0) continue;
            int r = sq / 8, c = sq % 8;
            switch (v)
            {
                case 1: GenPawn(list, sq, r, c, capturesOnly); break;
                case 2: GenStep(list, sq, r, c, KnightD, capturesOnly); break;
                case 3: GenSlide(list, sq, r, c, BishopD, capturesOnly); break;
                case 4: GenSlide(list, sq, r, c, RookD, capturesOnly); break;
                case 5: GenSlide(list, sq, r, c, BishopD, capturesOnly); GenSlide(list, sq, r, c, RookD, capturesOnly); break;
                case 6: GenStep(list, sq, r, c, KingD, capturesOnly); if (!capturesOnly) GenCastle(list); break;
            }
        }
    }

    void AddPawn(List<Move> list, int from, int to)
    {
        int tr = to / 8;
        if (tr == 0 || tr == 7)
        {
            list.Add(new Move(from, to, 5));
            list.Add(new Move(from, to, 4));
            list.Add(new Move(from, to, 3));
            list.Add(new Move(from, to, 2));
        }
        else list.Add(new Move(from, to, 0));
    }

    void GenPawn(List<Move> list, int sq, int r, int c, bool capOnly)
    {
        int dir = Side == 1 ? -1 : 1;
        int startRow = Side == 1 ? 6 : 1;
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

    void GenCastle(List<Move> list)
    {
        if (Side == 1)
        {
            if (B[60] != 6 || Attacked(60, -1)) return;
            if (WK && B[61] == 0 && B[62] == 0 && B[63] == 4 && !Attacked(61, -1) && !Attacked(62, -1)) list.Add(new Move(60, 62, 0));
            if (WQ && B[59] == 0 && B[58] == 0 && B[57] == 0 && B[56] == 4 && !Attacked(59, -1) && !Attacked(58, -1)) list.Add(new Move(60, 58, 0));
        }
        else
        {
            if (B[4] != -6 || Attacked(4, 1)) return;
            if (BK && B[5] == 0 && B[6] == 0 && B[7] == -4 && !Attacked(5, 1) && !Attacked(6, 1)) list.Add(new Move(4, 6, 0));
            if (BQ && B[3] == 0 && B[2] == 0 && B[1] == 0 && B[0] == -4 && !Attacked(3, 1) && !Attacked(2, 1)) list.Add(new Move(4, 2, 0));
        }
    }

    public Position Make(Move m)
    {
        var p = Clone();
        int piece = B[m.From];
        int captured = B[m.To];
        int kind = Math.Abs(piece);
        if (kind == 1 && m.To == Ep && captured == 0)
            p.B[m.To + (Side == 1 ? 8 : -8)] = 0;
        p.B[m.To] = m.Promo != 0 ? m.Promo * Side : piece;
        p.B[m.From] = 0;
        if (kind == 6 && Math.Abs(m.To - m.From) == 2)
        {
            if (m.To == 62) { p.B[61] = p.B[63]; p.B[63] = 0; }
            else if (m.To == 58) { p.B[59] = p.B[56]; p.B[56] = 0; }
            else if (m.To == 6) { p.B[5] = p.B[7]; p.B[7] = 0; }
            else if (m.To == 2) { p.B[3] = p.B[0]; p.B[0] = 0; }
        }
        if (m.From == 60 || m.To == 60) { p.WK = false; p.WQ = false; }
        if (m.From == 63 || m.To == 63) p.WK = false;
        if (m.From == 56 || m.To == 56) p.WQ = false;
        if (m.From == 4 || m.To == 4) { p.BK = false; p.BQ = false; }
        if (m.From == 7 || m.To == 7) p.BK = false;
        if (m.From == 0 || m.To == 0) p.BQ = false;
        p.Ep = (kind == 1 && Math.Abs(m.To - m.From) == 16) ? (m.From + m.To) / 2 : -1;
        p.Half = (kind == 1 || captured != 0) ? 0 : Half + 1;
        p.Side = -Side;
        return p;
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

    public string Key()
    {
        var chars = new char[64 + 6];
        for (int i = 0; i < 64; i++) chars[i] = (char)('A' + B[i] + 6);
        chars[64] = Side == 1 ? 'w' : 'b';
        chars[65] = WK ? 'K' : '-';
        chars[66] = WQ ? 'Q' : '-';
        chars[67] = BK ? 'k' : '-';
        chars[68] = BQ ? 'q' : '-';
        chars[69] = (char)('0' + Ep + 1);
        return new string(chars);
    }

    public bool InsufficientMaterial()
    {
        int minors = 0;
        for (int i = 0; i < 64; i++)
        {
            int k = Math.Abs(B[i]);
            if (k == 1 || k == 4 || k == 5) return false;
            if (k == 2 || k == 3) minors++;
        }
        return minors <= 1;
    }
}

class Engine
{
    public const int Mate = 100000;
    const int Inf = 1000000;
    const int MaxQuiescenceDepth = 8;
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

    readonly Random rnd = new Random();

    public static int Eval(Position p)
    {
        int heavy = 0;
        for (int i = 0; i < 64; i++)
        {
            int k = Math.Abs(p.B[i]);
            if (k >= 2 && k <= 5) heavy += Val[k];
        }
        bool endgame = heavy <= 1600;
        int score = 0;
        for (int i = 0; i < 64; i++)
        {
            int v = p.B[i];
            if (v == 0) continue;
            int k = Math.Abs(v);
            int idx = v > 0 ? i : (i ^ 56);
            int[] t = (k == 6 && endgame) ? KingEndT : Tables[k];
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

    public Move? Best(Position p, int depth)
    {
        var moves = p.Legal();
        if (moves.Count == 0) return null;
        // Перемешиваем, чтобы компьютер не играл одно и то же в равных позициях.
        moves = moves.OrderBy(m => rnd.Next()).ToList();
        moves = Ordered(p, moves);
        int alpha = -Inf;
        Move best = moves[0];
        foreach (var m in moves)
        {
            int s = -Negamax(p.Make(m), depth - 1, -Inf, -alpha, 1);
            if (s > alpha) { alpha = s; best = m; }
        }
        return best;
    }

    int Negamax(Position p, int depth, int alpha, int beta, int ply)
    {
        if (depth <= 0) return Quiesce(p, alpha, beta, 0);
        var pseudo = new List<Move>();
        p.GenPseudo(pseudo, false);
        int legal = 0;
        foreach (var m in Ordered(p, pseudo))
        {
            var np = p.Make(m);
            if (np.InCheck(p.Side)) continue;
            legal++;
            int s = -Negamax(np, depth - 1, -beta, -alpha, ply + 1);
            if (s >= beta) return beta;
            if (s > alpha) alpha = s;
        }
        if (legal == 0) return p.InCheck(p.Side) ? -Mate + ply : 0;
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
}

class Snap
{
    public Position P;
    public string Note;
    public int LastFrom = -1, LastTo = -1;
}

class BoardView : Panel
{
    public BoardView() { DoubleBuffered = true; ResizeRedraw = true; }
}

class PromoDialog : Form
{
    public int Choice;

    public PromoDialog(int side)
    {
        Text = "Превращение пешки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(4 * 76 + 12, 88);
        int[] kinds = { 5, 4, 3, 2 };
        string[] names = { "Ферзь", "Ладья", "Слон", "Конь" };
        for (int i = 0; i < 4; i++)
        {
            int k = kinds[i];
            var b = new Button();
            b.Text = MainForm.Glyph(k, side);
            b.Font = new Font("Segoe UI Symbol", 30f);
            b.Bounds = new Rectangle(6 + i * 76, 6, 72, 76);
            new ToolTip().SetToolTip(b, names[i]);
            b.Click += delegate { Choice = k; DialogResult = DialogResult.OK; };
            Controls.Add(b);
        }
    }
}

class MainForm : Form
{
    static readonly Color Light = Color.FromArgb(240, 217, 181);
    static readonly Color Dark = Color.FromArgb(181, 136, 99);
    static readonly Color LastMoveTint = Color.FromArgb(110, 205, 210, 70);
    static readonly Color SelectTint = Color.FromArgb(140, 90, 160, 230);
    static readonly Color CheckTint = Color.FromArgb(170, 230, 40, 40);
    static readonly Color DotColor = Color.FromArgb(110, 30, 30, 30);

    readonly BoardView board = new BoardView();
    readonly ListBox movesList = new ListBox();
    readonly ToolStripStatusLabel status = new ToolStripStatusLabel();
    readonly List<Snap> hist = new List<Snap>();
    readonly FontFamily pieceFont;

    List<Move> curLegal = new List<Move>();
    int aiSide = -1;       // 0 = игра вдвоём
    int depth = 4;
    static readonly int[] LevelDepths = { 1, 3, 4, 5 };
    bool flipped;
    bool thinking;
    int gameId;
    int sel = -1;
    string gameOver;
    ToolStripMenuItem[] levelItems;

    Snap Cur { get { return hist[hist.Count - 1]; } }

    public MainForm()
    {
        Text = "Шахматы";
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(860, 680);
        MinimumSize = new Size(500, 420);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(48, 46, 43);
        Font = new Font("Segoe UI", 10f);

        try { pieceFont = new FontFamily("Segoe UI Symbol"); }
        catch (ArgumentException) { pieceFont = FontFamily.GenericSansSerif; }

        board.Dock = DockStyle.Fill;
        board.Paint += BoardPaint;
        board.MouseDown += BoardMouseDown;

        movesList.Dock = DockStyle.Right;
        movesList.Width = 210;
        movesList.Font = new Font("Consolas", 11f);
        movesList.BackColor = Color.FromArgb(38, 36, 33);
        movesList.ForeColor = Color.Gainsboro;
        movesList.BorderStyle = BorderStyle.None;
        movesList.IntegralHeight = false;

        var strip = new StatusStrip();
        strip.Items.Add(status);
        status.Font = new Font("Segoe UI", 11f, FontStyle.Bold);

        Controls.Add(board);
        Controls.Add(movesList);
        Controls.Add(strip);
        Controls.Add(BuildMenu());
        board.BringToFront();

        NewGame(-1);
    }

    MenuStrip BuildMenu()
    {
        var menu = new MenuStrip();
        var game = new ToolStripMenuItem("Игра");
        game.DropDownItems.Add(new ToolStripMenuItem("Новая игра за белых", null, delegate { NewGame(-1); }, Keys.Control | Keys.N));
        game.DropDownItems.Add(new ToolStripMenuItem("Новая игра за чёрных", null, delegate { NewGame(1); }));
        game.DropDownItems.Add(new ToolStripMenuItem("Игра вдвоём", null, delegate { NewGame(0); }));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(new ToolStripMenuItem("Отменить ход", null, delegate { Undo(); }, Keys.Control | Keys.Z));
        game.DropDownItems.Add(new ToolStripMenuItem("Перевернуть доску", null, delegate { flipped = !flipped; board.Invalidate(); }, Keys.Control | Keys.F));
        game.DropDownItems.Add(new ToolStripSeparator());
        game.DropDownItems.Add(new ToolStripMenuItem("Выход", null, delegate { Close(); }));

        var level = new ToolStripMenuItem("Сложность");
        string[] names = { "Новичок", "Любитель", "Сильный", "Мастер" };
        int[] depths = LevelDepths;
        levelItems = new ToolStripMenuItem[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int d = depths[i];
            var item = new ToolStripMenuItem(names[i]);
            item.Click += delegate { SetLevel(d); };
            levelItems[i] = item;
            level.DropDownItems.Add(item);
        }
        SetLevel(depth);

        menu.Items.Add(game);
        menu.Items.Add(level);
        return menu;
    }

    void SetLevel(int d)
    {
        depth = d;
        for (int i = 0; i < levelItems.Length; i++) levelItems[i].Checked = (LevelDepths[i] == d);
    }

    void NewGame(int ai)
    {
        gameId++;
        thinking = false;
        aiSide = ai;
        flipped = ai == 1;
        hist.Clear();
        hist.Add(new Snap { P = Position.Start() });
        AfterPositionChange();
        MaybeAI();
    }

    void AfterPositionChange()
    {
        sel = -1;
        curLegal = Cur.P.Legal();
        gameOver = CheckEnd();
        RefreshMoves();
        UpdateStatus();
        board.Invalidate();
    }

    string CheckEnd()
    {
        var p = Cur.P;
        if (curLegal.Count == 0)
            return p.InCheck(p.Side) ? "Мат! Победили " + (p.Side == 1 ? "чёрные" : "белые") : "Пат — ничья";
        if (p.Half >= 100) return "Ничья: правило 50 ходов";
        if (p.InsufficientMaterial()) return "Ничья: недостаточно материала";
        string key = p.Key();
        if (hist.Count(s => s.P.Key() == key) >= 3) return "Ничья: троекратное повторение";
        return null;
    }

    void UpdateStatus()
    {
        var p = Cur.P;
        if (gameOver != null) status.Text = gameOver;
        else if (thinking) status.Text = "Компьютер думает…";
        else
        {
            string s = p.Side == 1 ? "Ход белых" : "Ход чёрных";
            if (p.InCheck(p.Side)) s += " — шах!";
            status.Text = s;
        }
    }

    void RefreshMoves()
    {
        movesList.BeginUpdate();
        movesList.Items.Clear();
        for (int i = 1; i < hist.Count; i += 2)
        {
            string line = string.Format("{0,3}. {1,-9}", (i + 1) / 2, hist[i].Note);
            if (i + 1 < hist.Count) line += hist[i + 1].Note;
            movesList.Items.Add(line);
        }
        if (movesList.Items.Count > 0) movesList.TopIndex = movesList.Items.Count - 1;
        movesList.EndUpdate();
    }

    void Apply(Move m)
    {
        var p = Cur.P;
        var np = p.Make(m);
        hist.Add(new Snap { P = np, Note = Notation(p, m, np), LastFrom = m.From, LastTo = m.To });
        AfterPositionChange();
        MaybeAI();
    }

    void MaybeAI()
    {
        if (gameOver != null || aiSide == 0 || Cur.P.Side != aiSide) return;
        thinking = true;
        UpdateStatus();
        int gid = gameId;
        var p = Cur.P;
        int d = depth;
        Task.Factory.StartNew(() => new Engine().Best(p, d)).ContinueWith(t =>
        {
            if (gid != gameId) return;
            thinking = false;
            if (t.Exception != null) { status.Text = "Ошибка движка: " + t.Exception.InnerException.Message; return; }
            if (t.Result.HasValue) Apply(t.Result.Value);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    void Undo()
    {
        if (thinking || hist.Count <= 1) return;
        hist.RemoveAt(hist.Count - 1);
        if (aiSide != 0)
            while (hist.Count > 1 && Cur.P.Side == aiSide) hist.RemoveAt(hist.Count - 1);
        AfterPositionChange();
        MaybeAI();
    }

    static string SqName(int sq) { return ((char)('a' + sq % 8)).ToString() + (8 - sq / 8); }

    static readonly string[] Letters = { "", "", "К", "С", "Л", "Ф", "Кр" };

    static string Notation(Position p, Move m, Position np)
    {
        int kind = Math.Abs(p.B[m.From]);
        string s;
        if (kind == 6 && Math.Abs(m.To - m.From) == 2) s = m.To % 8 == 6 ? "0-0" : "0-0-0";
        else
        {
            bool cap = p.B[m.To] != 0 || (kind == 1 && m.To == p.Ep);
            s = Letters[kind] + SqName(m.From) + (cap ? "x" : "-") + SqName(m.To);
            if (m.Promo != 0) s += "=" + Letters[m.Promo];
        }
        if (np.InCheck(np.Side)) s += np.Legal().Count == 0 ? "#" : "+";
        return s;
    }

    public static string Glyph(int kind, int side)
    {
        // Белые: ♔♕♖♗♘♙, чёрные: ♚♛♜♝♞♟
        string white = "♙♘♗♖♕♔";
        string black = "♟♞♝♜♛♚";
        return (side == 1 ? white : black)[kind - 1].ToString();
    }

    void Geometry(out int ox, out int oy, out int sq)
    {
        int margin = 26;
        int size = Math.Min(board.ClientSize.Width, board.ClientSize.Height) - 2 * margin;
        sq = Math.Max(size / 8, 8);
        ox = (board.ClientSize.Width - sq * 8) / 2;
        oy = (board.ClientSize.Height - sq * 8) / 2;
    }

    int SquareAt(Point pt)
    {
        int ox, oy, sq;
        Geometry(out ox, out oy, out sq);
        int vc = (pt.X - ox) / sq, vr = (pt.Y - oy) / sq;
        if (pt.X < ox || pt.Y < oy || vc > 7 || vr > 7) return -1;
        int r = flipped ? 7 - vr : vr, c = flipped ? 7 - vc : vc;
        return r * 8 + c;
    }

    void BoardPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int ox, oy, sq;
        Geometry(out ox, out oy, out sq);
        var p = Cur.P;
        int checkSq = p.InCheck(p.Side) ? p.KingSq(p.Side) : -1;
        var targets = new HashSet<int>(curLegal.Where(m => m.From == sel).Select(m => m.To));

        using (var lightB = new SolidBrush(Light))
        using (var darkB = new SolidBrush(Dark))
        using (var lastB = new SolidBrush(LastMoveTint))
        using (var selB = new SolidBrush(SelectTint))
        using (var chkB = new SolidBrush(CheckTint))
        using (var dotB = new SolidBrush(DotColor))
        using (var ringPen = new Pen(DotColor, Math.Max(3, sq / 14)))
        {
            for (int vr = 0; vr < 8; vr++)
                for (int vc = 0; vc < 8; vc++)
                {
                    int r = flipped ? 7 - vr : vr, c = flipped ? 7 - vc : vc;
                    int s = r * 8 + c;
                    var rect = new Rectangle(ox + vc * sq, oy + vr * sq, sq, sq);
                    g.FillRectangle((r + c) % 2 == 0 ? lightB : darkB, rect);
                    if (s == Cur.LastFrom || s == Cur.LastTo) g.FillRectangle(lastB, rect);
                    if (s == sel) g.FillRectangle(selB, rect);
                    if (s == checkSq) g.FillEllipse(chkB, rect);
                    if (p.B[s] != 0) DrawPiece(g, p.B[s], rect);
                    if (targets.Contains(s))
                    {
                        if (p.B[s] != 0 || (s == p.Ep && Math.Abs(p.B[sel]) == 1))
                            g.DrawEllipse(ringPen, rect.X + 3, rect.Y + 3, sq - 7, sq - 7);
                        else
                            g.FillEllipse(dotB, rect.X + sq * 0.36f, rect.Y + sq * 0.36f, sq * 0.28f, sq * 0.28f);
                    }
                }

            using (var f = new Font("Segoe UI", Math.Max(8, sq / 6)))
            using (var cb = new SolidBrush(Color.FromArgb(200, 200, 190)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                for (int i = 0; i < 8; i++)
                {
                    int c = flipped ? 7 - i : i, r = flipped ? 7 - i : i;
                    g.DrawString(((char)('a' + c)).ToString(), f, cb, new RectangleF(ox + i * sq, oy + 8 * sq, sq, 24), sf);
                    g.DrawString((8 - r).ToString(), f, cb, new RectangleF(ox - 24, oy + i * sq, 24, sq), sf);
                }
            }
        }
    }

    void DrawPiece(Graphics g, int v, Rectangle rect)
    {
        int kind = Math.Abs(v);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        float em = rect.Height * 0.82f;
        var r = new RectangleF(rect.X, rect.Y + rect.Height * 0.03f, rect.Width, rect.Height);
        using (var solid = new GraphicsPath())
        using (var outline = new GraphicsPath())
        {
            solid.AddString(Glyph(kind, -1), pieceFont, 0, em, r, sf);
            if (v > 0)
            {
                // Белая фигура: заливаем силуэт белым, сверху рисуем контурный глиф.
                outline.AddString(Glyph(kind, 1), pieceFont, 0, em, r, sf);
                using (var fill = new SolidBrush(Color.FromArgb(252, 252, 248))) g.FillPath(fill, solid);
                using (var pen = new Pen(Color.FromArgb(30, 30, 30), 1.4f)) g.DrawPath(pen, solid);
                using (var ink = new SolidBrush(Color.FromArgb(30, 30, 30))) g.FillPath(ink, outline);
            }
            else
            {
                using (var fill = new SolidBrush(Color.FromArgb(28, 28, 28))) g.FillPath(fill, solid);
                using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 1.2f)) g.DrawPath(pen, solid);
            }
        }
    }

    void BoardMouseDown(object sender, MouseEventArgs e)
    {
        if (thinking || gameOver != null || e.Button != MouseButtons.Left) return;
        var p = Cur.P;
        if (aiSide != 0 && p.Side == aiSide) return;
        int s = SquareAt(e.Location);
        if (s < 0) return;

        if (sel >= 0)
        {
            var cand = curLegal.Where(m => m.From == sel && m.To == s).ToList();
            if (cand.Count > 0)
            {
                Move mv = cand[0];
                if (cand.Count > 1)
                {
                    using (var dlg = new PromoDialog(p.Side))
                    {
                        if (dlg.ShowDialog(this) != DialogResult.OK) return;
                        mv = cand.First(m => m.Promo == dlg.Choice);
                    }
                }
                Apply(mv);
                return;
            }
        }
        sel = (p.B[s] * p.Side > 0 && s != sel) ? s : -1;
        board.Invalidate();
    }

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
