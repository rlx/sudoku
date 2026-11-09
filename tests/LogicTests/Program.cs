using System;
using System.Diagnostics;
using BigSudoku;

int failures = 0;
void Check(bool ok, string what) { if (!ok) { failures++; Console.WriteLine("FAIL: " + what); } }

var rng = new Random(1234);
foreach (Difficulty level in Enum.GetValues(typeof(Difficulty)))
{
    var sw = Stopwatch.StartNew();
    long worst = 0; int minGivens = 81, maxGivens = 0;
    for (int t = 0; t < 30; t++)
    {
        long start = sw.ElapsedMilliseconds;
        var game = Game.NewGame(level, rng);
        worst = Math.Max(worst, sw.ElapsedMilliseconds - start);

        int givens = 0;
        for (int i = 0; i < 81; i++)
        {
            if (game.Givens[i] != 0) { givens++; Check(game.Givens[i] == game.Solution[i], "given matches solution"); }
            Check(game.Solution[i] >= 1 && game.Solution[i] <= 9, "solution filled");
        }
        var sol = (int[])game.Solution.Clone();
        for (int i = 0; i < 81; i++) Check(Sudoku.CanPlace(sol, i, sol[i]), "solution valid");
        Check(Sudoku.CountSolutions(game.Givens, 2) == 1, "unique solution");
        if (level != Difficulty.Hard) Check(Sudoku.SolvableBySingles(game.Givens), "easy-style solvable");
        minGivens = Math.Min(minGivens, givens); maxGivens = Math.Max(maxGivens, givens);
    }
    Console.WriteLine($"{level,-6}: givens {minGivens}-{maxGivens}, avg {sw.ElapsedMilliseconds / 30} ms, worst {worst} ms");
}

// Play, undo, save/load, solve
var g = Game.NewGame(Difficulty.Easy, rng);
int empty = Array.IndexOf(g.Givens, 0);
int wrong = g.Solution[empty] % 9 + 1;
Check(g.Set(empty, wrong), "set");
Check(g.IsWrong(empty), "wrong detected");
Check(!g.Set(Array.FindIndex(g.Givens, v => v != 0), 5), "cannot change given");
var loaded = Game.Deserialize(g.Serialize());
Check(loaded != null && loaded.Cells[empty] == wrong && loaded.Level == Difficulty.Easy, "save/load");
Check(Game.Deserialize("garbage") == null, "bad save ignored");
Check(g.Undo() == empty && g.Cells[empty] == 0 && !g.CanUndo, "undo");
for (int i = 0; i < 81; i++) if (!g.IsGiven(i)) g.Set(i, g.Solution[i]);
Check(g.IsSolved, "solved");
for (int v = 1; v <= 9; v++) Check(g.CorrectCount(v) == 9, "counts");

Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : $"{failures} FAILURES");
return failures == 0 ? 0 : 1;
