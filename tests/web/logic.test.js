// Run with: node tests/web/logic.test.js
const S = require('../../web/sudoku.js');
let failures = 0;
const check = (ok, what) => { if (!ok) { failures++; console.log('FAIL: ' + what); } };

for (const level of Object.keys(S.LEVELS)) {
  let worst = 0, total = 0, min = 81, max = 0;
  for (let t = 0; t < 30; t++) {
    const start = Date.now();
    const g = S.newGame(level);
    const ms = Date.now() - start; worst = Math.max(worst, ms); total += ms;
    const givens = g.givens.filter(v => v).length;
    min = Math.min(min, givens); max = Math.max(max, givens);
    g.givens.forEach((v, i) => check(!v || v === g.solution[i], 'given matches solution'));
    for (let i = 0; i < 81; i++)
      for (let j = 0; j < 81; j++)
        if (i !== j && S.sameUnit(i, j)) check(g.solution[i] !== g.solution[j], 'solution valid');
    check(S.countSolutions(g.givens, 2) === 1, 'unique');
    if (level !== 'hard') check(S.solvableBySingles(g.givens), 'singles-solvable');
  }
  console.log(`${level.padEnd(6)}: givens ${min}-${max}, avg ${Math.round(total / 30)} ms, worst ${worst} ms`);
}

const G = S.Game;
const g = S.newGame('easy');
const empty = g.givens.indexOf(0);
const wrong = g.solution[empty] % 9 + 1;
check(G.set(g, empty, wrong) && G.isWrong(g, empty), 'wrong detected');
check(!G.set(g, g.givens.findIndex(v => v), 5), 'cannot change given');
const loaded = G.load(JSON.stringify(g));
check(loaded && loaded.cells[empty] === wrong && loaded.undo.length === 1, 'save/load');
check(G.load('garbage') === null && G.load('{"v":1}') === null, 'bad save ignored');
check(G.undo(g) === empty && g.cells[empty] === 0 && G.undo(g) === -1, 'undo');
for (let i = 0; i < 81; i++) if (!G.isGiven(g, i)) G.set(g, i, g.solution[i]);
check(G.isSolved(g), 'solved');
for (let v = 1; v <= 9; v++) check(G.correctCount(g, v) === 9, 'counts');

console.log(failures === 0 ? 'ALL TESTS PASSED' : failures + ' FAILURES');
process.exit(failures ? 1 : 0);
