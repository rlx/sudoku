// Puzzle generation, solving and game state. A grid is an array of 81 numbers, row by row, 0 = empty.
// Same rules as the Windows version (src/BigSudoku/Sudoku.cs).
(function (root) {
  'use strict';

  const BOX = [];
  const PEERS = [];
  const UNITS = [];
  for (let i = 0; i < 81; i++) BOX[i] = Math.floor(i / 27) * 3 + Math.floor((i % 9) / 3);
  for (let i = 0; i < 81; i++) {
    PEERS[i] = [];
    for (let j = 0; j < 81; j++) {
      if (j !== i && (row(i) === row(j) || col(i) === col(j) || BOX[i] === BOX[j])) PEERS[i].push(j);
    }
  }
  for (let u = 0; u < 9; u++) {
    const r = [], c = [], b = [];
    for (let k = 0; k < 9; k++) {
      r.push(u * 9 + k);
      c.push(k * 9 + u);
      b.push((Math.floor(u / 3) * 3 + Math.floor(k / 3)) * 9 + (u % 3) * 3 + (k % 3));
    }
    UNITS.push(r, c, b);
  }

  function row(i) { return Math.floor(i / 9); }
  function col(i) { return i % 9; }
  function sameUnit(a, b) { return row(a) === row(b) || col(a) === col(b) || BOX[a] === BOX[b]; }

  // Bit v set (v = 1..9) when v can go in cell i.
  function candidates(g, i) {
    let used = 0;
    const p = PEERS[i];
    for (let k = 0; k < p.length; k++) used |= 1 << g[p[k]];
    return ~used & 0x3fe;
  }

  function bitCount(m) {
    let n = 0;
    while (m) { m &= m - 1; n++; }
    return n;
  }

  function shuffled(n) {
    const a = [];
    for (let i = 0; i < n; i++) a.push(i);
    for (let i = n - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      const t = a[i]; a[i] = a[j]; a[j] = t;
    }
    return a;
  }

  function fillRandom(g, cell) {
    if (cell === 81) return true;
    const m = candidates(g, cell);
    for (const v of shuffled(9)) {
      if (m & (1 << (v + 1))) {
        g[cell] = v + 1;
        if (fillRandom(g, cell + 1)) return true;
      }
    }
    g[cell] = 0;
    return false;
  }

  // Counts solutions, stopping once `limit` is reached.
  function countSolutions(puzzle, limit) {
    const g = puzzle.slice();
    let count = 0;
    (function rec() {
      let best = -1, bestMask = 0, bestN = 10;
      for (let i = 0; i < 81; i++) {
        if (g[i]) continue;
        const m = candidates(g, i);
        const n = bitCount(m);
        if (n < bestN) {
          best = i; bestMask = m; bestN = n;
          if (n === 0) return;
        }
      }
      if (best < 0) { count++; return; }
      for (let v = 1; v <= 9 && count < limit; v++) {
        if (!(bestMask & (1 << v))) continue;
        g[best] = v;
        rec();
      }
      g[best] = 0;
    })();
    return count;
  }

  // True if the puzzle can be finished using only "naked single" and "hidden single" steps.
  function solvableBySingles(puzzle) {
    const g = puzzle.slice();
    let progress = true;
    while (progress) {
      progress = false;
      for (let i = 0; i < 81; i++) {
        if (g[i]) continue;
        const m = candidates(g, i);
        if (m === 0) return false;
        if (bitCount(m) === 1) { g[i] = Math.log2(m); progress = true; }
      }
      for (const unit of UNITS) {
        for (let v = 1; v <= 9; v++) {
          let spot = -1, n = 0, present = false;
          for (const i of unit) {
            if (g[i] === v) { present = true; break; }
            if (!g[i] && (candidates(g, i) & (1 << v))) { spot = i; n++; }
          }
          if (!present && n === 1) { g[spot] = v; progress = true; }
        }
      }
    }
    return g.every(v => v !== 0);
  }

  const LEVELS = {
    easy: { name: 'Easy', givens: 42, singles: true },
    medium: { name: 'Medium', givens: 33, singles: true },
    hard: { name: 'Hard', givens: 26, singles: false },
  };

  function generate(level) {
    const cfg = LEVELS[level];
    const solution = new Array(81).fill(0);
    fillRandom(solution, 0);
    const puzzle = solution.slice();
    let givens = 81;
    for (const cell of shuffled(81)) {
      if (givens <= cfg.givens) break;
      const keep = puzzle[cell];
      puzzle[cell] = 0;
      const ok = countSolutions(puzzle, 2) === 1 && (!cfg.singles || solvableBySingles(puzzle));
      if (ok) givens--;
      else puzzle[cell] = keep;
    }
    return { puzzle, solution };
  }

  // ---------- Game state ----------

  function newGame(level) {
    const { puzzle, solution } = generate(level);
    return { v: 1, level, givens: puzzle, solution, cells: puzzle.slice(), undo: [] };
  }

  const Game = {
    isGiven: (g, i) => g.givens[i] !== 0,
    isWrong: (g, i) => g.cells[i] !== 0 && g.cells[i] !== g.solution[i],
    isSolved: g => g.cells.every((v, i) => v === g.solution[i]),
    correctCount: (g, v) => g.cells.filter((x, i) => x === v && g.solution[i] === v).length,
    set(g, cell, value) {
      if (g.givens[cell] || g.cells[cell] === value) return false;
      g.undo.push([cell, g.cells[cell]]);
      if (g.undo.length > 500) g.undo.shift();
      g.cells[cell] = value;
      return true;
    },
    // Returns the cell that changed, or -1.
    undo(g) {
      const u = g.undo.pop();
      if (!u) return -1;
      g.cells[u[0]] = u[1];
      return u[0];
    },
    // Returns a game object, or null if the saved text isn't a valid game.
    load(text) {
      try {
        const g = JSON.parse(text);
        const ok = arr => Array.isArray(arr) && arr.length === 81 && arr.every(v => Number.isInteger(v) && v >= 0 && v <= 9);
        if (!g || g.v !== 1 || !LEVELS[g.level] || !ok(g.givens) || !ok(g.solution) || !ok(g.cells)) return null;
        if (!Array.isArray(g.undo)) g.undo = [];
        return g;
      } catch (e) {
        return null;
      }
    },
  };

  const api = { LEVELS, row, col, sameUnit, countSolutions, solvableBySingles, generate, newGame, Game };
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.Sudoku = api;
})(this);
