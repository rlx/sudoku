using System;
using System.Collections.Generic;

namespace BigSudoku
{
    public enum Difficulty { Easy, Medium, Hard }

    // Puzzle generation and solving. A grid is an int[81], row by row, 0 = empty.
    public static class Sudoku
    {
        public static int Row(int i) { return i / 9; }
        public static int Col(int i) { return i % 9; }
        public static int Box(int i) { return (i / 27) * 3 + (i % 9) / 3; }

        public static bool SameUnit(int a, int b)
        {
            return Row(a) == Row(b) || Col(a) == Col(b) || Box(a) == Box(b);
        }

        public static bool CanPlace(int[] g, int cell, int value)
        {
            for (int j = 0; j < 81; j++)
                if (j != cell && g[j] == value && SameUnit(cell, j)) return false;
            return true;
        }

        public static void Generate(Difficulty level, Random rng, out int[] puzzle, out int[] solution)
        {
            solution = new int[81];
            FillRandom(solution, 0, rng);

            int targetGivens;
            bool requireSingles;
            switch (level)
            {
                case Difficulty.Easy: targetGivens = 42; requireSingles = true; break;
                case Difficulty.Medium: targetGivens = 33; requireSingles = true; break;
                default: targetGivens = 26; requireSingles = false; break;
            }

            puzzle = (int[])solution.Clone();
            int givens = 81;
            foreach (int cell in Shuffled(81, rng))
            {
                if (givens <= targetGivens) break;
                int keep = puzzle[cell];
                puzzle[cell] = 0;
                bool ok = CountSolutions(puzzle, 2) == 1
                          && (!requireSingles || SolvableBySingles(puzzle));
                if (ok) givens--;
                else puzzle[cell] = keep;
            }
        }

        static bool FillRandom(int[] g, int cell, Random rng)
        {
            if (cell == 81) return true;
            foreach (int v in Shuffled(9, rng))
            {
                if (CanPlace(g, cell, v + 1))
                {
                    g[cell] = v + 1;
                    if (FillRandom(g, cell + 1, rng)) return true;
                }
            }
            g[cell] = 0;
            return false;
        }

        // Counts solutions, stopping once `limit` is reached.
        public static int CountSolutions(int[] puzzle, int limit)
        {
            int[] g = (int[])puzzle.Clone();
            int count = 0;
            CountRec(g, ref count, limit);
            return count;
        }

        static void CountRec(int[] g, ref int count, int limit)
        {
            // Pick the empty cell with the fewest options.
            int best = -1, bestCount = 10;
            for (int i = 0; i < 81; i++)
            {
                if (g[i] != 0) continue;
                int n = 0;
                for (int v = 1; v <= 9; v++) if (CanPlace(g, i, v)) n++;
                if (n < bestCount) { best = i; bestCount = n; if (n == 0) return; }
            }
            if (best == -1) { count++; return; }

            for (int v = 1; v <= 9 && count < limit; v++)
            {
                if (!CanPlace(g, best, v)) continue;
                g[best] = v;
                CountRec(g, ref count, limit);
                g[best] = 0;
            }
        }

        // True if the puzzle can be finished using only "naked single" and
        // "hidden single" steps, the techniques a casual player uses.
        public static bool SolvableBySingles(int[] puzzle)
        {
            int[] g = (int[])puzzle.Clone();
            bool progress = true;
            while (progress)
            {
                progress = false;

                // Naked singles: a cell with only one possible value.
                for (int i = 0; i < 81; i++)
                {
                    if (g[i] != 0) continue;
                    int only = 0, n = 0;
                    for (int v = 1; v <= 9; v++)
                        if (CanPlace(g, i, v)) { only = v; n++; }
                    if (n == 0) return false;
                    if (n == 1) { g[i] = only; progress = true; }
                }

                // Hidden singles: a value that fits in only one cell of a row, column or box.
                for (int unit = 0; unit < 27; unit++)
                {
                    for (int v = 1; v <= 9; v++)
                    {
                        int spot = -1, n = 0;
                        bool present = false;
                        for (int k = 0; k < 9; k++)
                        {
                            int i = UnitCell(unit, k);
                            if (g[i] == v) { present = true; break; }
                            if (g[i] == 0 && CanPlace(g, i, v)) { spot = i; n++; }
                        }
                        if (!present && n == 1) { g[spot] = v; progress = true; }
                    }
                }
            }
            for (int i = 0; i < 81; i++) if (g[i] == 0) return false;
            return true;
        }

        // unit 0-8 = rows, 9-17 = columns, 18-26 = boxes
        static int UnitCell(int unit, int k)
        {
            if (unit < 9) return unit * 9 + k;
            if (unit < 18) return k * 9 + (unit - 9);
            int b = unit - 18;
            return ((b / 3) * 3 + k / 3) * 9 + (b % 3) * 3 + k % 3;
        }

        static int[] Shuffled(int n, Random rng)
        {
            int[] a = new int[n];
            for (int i = 0; i < n; i++) a[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int t = a[i]; a[i] = a[j]; a[j] = t;
            }
            return a;
        }
    }

    // Current game state, with undo and simple text save/load.
    public class Game
    {
        public Difficulty Level;
        public int[] Givens = new int[81];
        public int[] Solution = new int[81];
        public int[] Cells = new int[81];
        readonly Stack<int[]> undo = new Stack<int[]>(); // {cell, oldValue}

        public static Game NewGame(Difficulty level, Random rng)
        {
            var g = new Game { Level = level };
            Sudoku.Generate(level, rng, out g.Givens, out g.Solution);
            g.Cells = (int[])g.Givens.Clone();
            return g;
        }

        public bool IsGiven(int i) { return Givens[i] != 0; }
        public bool IsWrong(int i) { return Cells[i] != 0 && Cells[i] != Solution[i]; }
        public bool CanUndo { get { return undo.Count > 0; } }

        public bool IsSolved
        {
            get
            {
                for (int i = 0; i < 81; i++) if (Cells[i] != Solution[i]) return false;
                return true;
            }
        }

        // How many times a value is correctly placed (used to grey out finished numbers).
        public int CorrectCount(int value)
        {
            int n = 0;
            for (int i = 0; i < 81; i++) if (Cells[i] == value && Solution[i] == value) n++;
            return n;
        }

        public bool Set(int cell, int value)
        {
            if (IsGiven(cell) || Cells[cell] == value) return false;
            undo.Push(new[] { cell, Cells[cell] });
            Cells[cell] = value;
            return true;
        }

        // Returns the cell that changed, or -1.
        public int Undo()
        {
            if (undo.Count == 0) return -1;
            int[] u = undo.Pop();
            Cells[u[0]] = u[1];
            return u[0];
        }

        public string Serialize()
        {
            return "v1\n" + Level + "\n" + Join(Givens) + "\n" + Join(Solution) + "\n" + Join(Cells) + "\n";
        }

        public static Game Deserialize(string text)
        {
            string[] lines = text.Replace("\r", "").Split('\n');
            if (lines.Length < 5 || lines[0] != "v1") return null;
            var g = new Game
            {
                Level = (Difficulty)Enum.Parse(typeof(Difficulty), lines[1]),
                Givens = Parse(lines[2]),
                Solution = Parse(lines[3]),
                Cells = Parse(lines[4]),
            };
            if (g.Givens == null || g.Solution == null || g.Cells == null) return null;
            return g;
        }

        static string Join(int[] a)
        {
            var c = new char[81];
            for (int i = 0; i < 81; i++) c[i] = (char)('0' + a[i]);
            return new string(c);
        }

        static int[] Parse(string s)
        {
            if (s == null || s.Length != 81) return null;
            var a = new int[81];
            for (int i = 0; i < 81; i++)
            {
                if (s[i] < '0' || s[i] > '9') return null;
                a[i] = s[i] - '0';
            }
            return a;
        }
    }
}
