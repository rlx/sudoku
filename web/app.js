(function () {
  'use strict';

  const { LEVELS, Game, sameUnit, row, col } = Sudoku;
  const SAVE_KEY = 'bigSudoku.save';
  const SEEN_INSTALL_KEY = 'bigSudoku.seenInstall';

  const params = new URLSearchParams(location.search);
  const isAndroidApp = params.get('app') === 'android';
  const isStandalone = isAndroidApp || navigator.standalone === true ||
    window.matchMedia('(display-mode: standalone)').matches;
  const isAndroid = /Android/.test(navigator.userAgent);
  const isIOS = !isAndroid && (/iPhone|iPad|iPod/.test(navigator.userAgent) ||
    (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1));
  const canInstall = !isStandalone && (isIOS || isAndroid);

  const $ = sel => document.querySelector(sel);
  const screens = { menu: $('#menu'), game: $('#game'), install: $('#install') };
  const board = $('#board');
  const numButtons = Array.from(document.querySelectorAll('.num'));
  const cells = [];

  let game = loadGame();
  let selected = 40; // centre square
  let current = null;

  // ---------- Saving ----------

  function loadGame() {
    try {
      const g = Game.load(localStorage.getItem(SAVE_KEY));
      return g && !Game.isSolved(g) ? g : null;
    } catch (e) {
      return null;
    }
  }

  function saveGame() {
    try {
      if (game && !Game.isSolved(game)) localStorage.setItem(SAVE_KEY, JSON.stringify(game));
      else localStorage.removeItem(SAVE_KEY);
    } catch (e) { /* saving is best effort */ }
  }

  // ---------- Screens ----------

  function show(name) {
    current = name;
    for (const key in screens) screens[key].hidden = key !== name;
    if (name === 'menu') {
      $('#continue').hidden = !game || Game.isSolved(game);
      $('#show-install').hidden = !canInstall;
    }
    if (name === 'game') { layout(); render(); }
  }

  function startNew(level) {
    game = Sudoku.newGame(level);
    selected = 40;
    saveGame();
    show('game');
  }

  // ---------- Actions ----------

  function place(value) {
    if (current !== 'game' || !game || Game.isSolved(game)) return;
    if (Game.set(game, selected, value)) afterChange();
  }

  function undo() {
    if (current !== 'game' || !game || Game.isSolved(game)) return;
    const cell = Game.undo(game);
    if (cell >= 0) { selected = cell; afterChange(); }
  }

  function afterChange() {
    saveGame();
    render();
  }

  function select(i) {
    if (!game || Game.isSolved(game)) return;
    selected = i;
    render();
  }

  // ---------- Drawing ----------

  function buildBoard() {
    for (let i = 0; i < 81; i++) {
      const c = document.createElement('div');
      c.className = 'cell r' + row(i) + ' c' + col(i);
      c.dataset.i = i;
      board.appendChild(c);
      cells.push(c);
    }
  }

  function render() {
    if (!game) return;
    const solved = Game.isSolved(game);
    const selValue = game.cells[selected];

    $('#title').textContent = LEVELS[game.level].name + ' puzzle';
    for (let i = 0; i < 81; i++) {
      const v = game.cells[i];
      const cl = cells[i].classList;
      cells[i].textContent = v ? v : '';
      cl.toggle('given', Game.isGiven(game, i));
      cl.toggle('user', !Game.isGiven(game, i) && v !== 0);
      cl.toggle('wrong', Game.isWrong(game, i));
      cl.toggle('selected', !solved && i === selected);
      cl.toggle('peer', !solved && sameUnit(i, selected));
      cl.toggle('same', !solved && selValue !== 0 && v === selValue);
    }
    for (const b of numButtons) {
      b.classList.toggle('done', Game.correctCount(game, +b.dataset.num) >= 9);
      b.disabled = solved;
    }
    $('#erase').disabled = solved || Game.isGiven(game, selected) || game.cells[selected] === 0;
    $('#undo').disabled = solved || game.undo.length === 0;
    $('#solved').hidden = !solved;
    $('#hint').style.visibility = solved ? 'hidden' : '';
  }

  // Positions everything on the game screen to fill the available space.
  function layout() {
    const stage = screens.game;
    const W = stage.clientWidth, H = stage.clientHeight;
    if (!W || !H) return;
    const short = Math.min(W, H);
    const m = Math.round(short * 0.025) + 4;
    const gap = Math.max(6, Math.round(short * 0.016));

    if (H > W * 1.1) layoutPortrait(W, H, m, gap);
    else layoutLandscape(W, H, m, gap);
  }

  function layoutPortrait(W, H, m, gap) {
    const innerW = W - 2 * m;
    const titleH = clamp(H * 0.055, 30, 60);
    const btnW = (innerW - 4 * gap) / 5;
    const wantBtnH = Math.min(btnW * 1.1, 110);
    let size = Math.min(innerW, H - 2 * m - titleH - 4 * gap - 3 * wantBtnH);
    size = Math.floor(size / 9) * 9;
    const btnH = Math.min(btnW * 1.3, 120, (H - 2 * m - titleH - 4 * gap - size) / 3);
    const total = titleH + gap + size + gap + 3 * btnH + 2 * gap;
    let y = Math.max(m, (H - total) / 2);

    place$('#title', m, y, innerW, titleH, titleH * 0.62);
    y += titleH + gap;
    placeBoard((W - size) / 2, y, size);
    y += size + gap;

    const x0 = (W - (5 * btnW + 4 * gap)) / 2;
    numButtons.forEach((b, k) => {
      const r = k < 5 ? 0 : 1, c = k < 5 ? k : k - 5;
      placeButton(b, x0 + c * (btnW + gap), y + r * (btnH + gap), btnW, btnH, 0.62);
    });
    placeButton($('#erase'), x0 + 4 * (btnW + gap), y + btnH + gap, btnW, btnH);
    y += 2 * (btnH + gap);
    const rowW = 5 * btnW + 4 * gap;
    const undoW = 2 * btnW + gap;
    placeButton($('#undo'), x0, y, undoW, btnH);
    placeButton($('#new'), x0 + undoW + gap, y, rowW - undoW - gap, btnH);
    $('#hint').hidden = true;
  }

  function layoutLandscape(W, H, m, gap) {
    let size = Math.min(H - 2 * m, (W - 3 * m) * 0.62);
    size = Math.floor(size / 9) * 9;
    const pw = Math.min(W - size - 3 * m, size * 0.8);
    const x = (W - (size + m + pw)) / 2;
    const top = (H - size) / 2;
    placeBoard(x, top, size);

    const px = x + size + m;
    let y = top;
    const titleH = size * 0.09;
    place$('#title', px, y, pw, titleH, Math.min(titleH * 0.6, pw / 8));
    y += titleH + gap;

    const pad = Math.min(pw, size * 0.52);
    const b = (pad - 2 * gap) / 3;
    const padX = px + (pw - (3 * b + 2 * gap)) / 2;
    numButtons.forEach((btn, k) => {
      placeButton(btn, padX + (k % 3) * (b + gap), y + Math.floor(k / 3) * (b + gap), b, b, 0.62);
    });
    y += 3 * b + 4 * gap;

    const rowH = size * 0.12;
    const half = (pw - gap) / 2;
    placeButton($('#erase'), px, y, half, rowH);
    placeButton($('#undo'), px + half + gap, y, pw - half - gap, rowH);
    y += rowH + gap;
    placeButton($('#new'), px, y, pw, rowH);
    y += rowH + gap;

    const hint = $('#hint');
    hint.hidden = y + rowH > top + size;
    place$('#hint', px, y, pw, top + size - y, Math.min(rowH * 0.32, pw / 16));
  }

  function placeBoard(x, y, size) {
    const cell = size / 9;
    place$('#board', x, y, size, size, cell * 0.62);
    board.style.setProperty('--thick', Math.max(3, Math.round(cell / 12)) + 'px');
    place$('#solved', x, y, size, size, size * 0.07);
  }

  function placeButton(el, x, y, w, h, fontScale) {
    const text = el.textContent.trim();
    const font = fontScale
      ? Math.min(w, h) * fontScale
      : Math.min(h * 0.42, w / (text.length * 0.6 + 0.6));
    place$(el, x, y, w, h, font);
    el.style.borderRadius = Math.max(8, Math.min(w, h) / 6) + 'px';
  }

  function place$(elOrSel, x, y, w, h, fontPx) {
    const el = typeof elOrSel === 'string' ? $(elOrSel) : elOrSel;
    el.style.left = Math.round(x) + 'px';
    el.style.top = Math.round(y) + 'px';
    el.style.width = Math.round(w) + 'px';
    el.style.height = Math.round(h) + 'px';
    if (fontPx) el.style.fontSize = Math.max(12, fontPx) + 'px';
  }

  function clamp(v, lo, hi) { return Math.max(lo, Math.min(hi, v)); }

  // ---------- Events ----------

  board.addEventListener('click', e => {
    const c = e.target.closest('.cell');
    if (c) select(+c.dataset.i);
  });
  numButtons.forEach(b => b.addEventListener('click', () => place(+b.dataset.num)));
  $('#erase').addEventListener('click', () => place(0));
  $('#undo').addEventListener('click', undo);
  $('#new').addEventListener('click', () => show('menu'));
  $('#again').addEventListener('click', () => show('menu'));
  $('#continue').addEventListener('click', () => show('game'));
  document.querySelectorAll('[data-level]').forEach(b =>
    b.addEventListener('click', () => startNew(b.dataset.level)));
  $('#show-install').addEventListener('click', () => show('install'));
  $('#install-done').addEventListener('click', () => show(game ? 'game' : 'menu'));

  document.addEventListener('keydown', e => {
    if (current !== 'game') return;
    const r = row(selected), c = col(selected);
    if (e.key >= '1' && e.key <= '9') place(+e.key);
    else if (e.key === 'Backspace' || e.key === 'Delete' || e.key === '0' || e.key === ' ') place(0);
    else if (e.key === 'ArrowUp') select(((r + 8) % 9) * 9 + c);
    else if (e.key === 'ArrowDown') select(((r + 1) % 9) * 9 + c);
    else if (e.key === 'ArrowLeft') select(r * 9 + (c + 8) % 9);
    else if (e.key === 'ArrowRight') select(r * 9 + (c + 1) % 9);
    else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') undo();
    else return;
    e.preventDefault();
  });

  window.addEventListener('resize', () => { if (current === 'game') layout(); });

  // ---------- Start ----------

  $('#install-ios').hidden = !isIOS;
  $('#install-android').hidden = !isAndroid;
  if (isAndroid) {
    $('#install-title').textContent = 'Get the Sudoku app';
    $('#show-install').textContent = 'Get the Sudoku app';
  }
  buildBoard();

  let seenInstall = false;
  try { seenInstall = localStorage.getItem(SEEN_INSTALL_KEY) === '1'; } catch (e) { }
  if (canInstall && !seenInstall) {
    try { localStorage.setItem(SEEN_INSTALL_KEY, '1'); } catch (e) { }
    show('install');
  } else {
    show(game ? 'game' : 'menu');
  }

  if ('serviceWorker' in navigator && !isAndroidApp && location.protocol === 'https:') {
    navigator.serviceWorker.register('sw.js').catch(() => { });
  }
})();
