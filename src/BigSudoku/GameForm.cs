using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace BigSudoku
{
    // The whole window is drawn by hand so everything scales with the window size.
    public class GameForm : Form
    {
        enum Screen { Menu, Playing, Solved }

        class Button
        {
            public Rectangle Rect;
            public string Text;
            public Action OnClick;
            public bool Enabled = true;
            public Color Back = Color.White;
            public Color Fore = Ink;
            public float FontScale = 0.45f; // font size as a fraction of button height
        }

        // Colours
        static readonly Color Paper = Color.FromArgb(250, 247, 238);
        static readonly Color Ink = Color.FromArgb(25, 25, 25);
        static readonly Color UserInk = Color.FromArgb(25, 85, 200);
        static readonly Color WrongInk = Color.FromArgb(210, 20, 20);
        static readonly Color WrongBack = Color.FromArgb(255, 215, 215);
        static readonly Color SelectedBack = Color.FromArgb(255, 222, 120);
        static readonly Color PeerBack = Color.FromArgb(228, 238, 252);
        static readonly Color SameNumberBack = Color.FromArgb(190, 214, 250);
        static readonly Color ThinLine = Color.FromArgb(160, 160, 160);
        static readonly Color GreenButton = Color.FromArgb(205, 235, 200);
        static readonly Color Disabled = Color.FromArgb(200, 200, 200);

        readonly Random rng = new Random();
        readonly string savePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BigSudoku", "save.txt");

        Screen screen = Screen.Menu;
        Game game;
        int selected = 40; // centre cell
        Rectangle board;
        readonly List<Button> buttons = new List<Button>();
        Button hover;
        Button pressed;

        public GameForm()
        {
            Text = "Sudoku";
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            MinimumSize = new Size(800, 600);
            BackColor = Paper;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            game = LoadSaved();
            if (game != null && !game.IsSolved) screen = Screen.Playing;
            else game = null;
        }

        // ---------- Saving ----------

        Game LoadSaved()
        {
            try { return File.Exists(savePath) ? Game.Deserialize(File.ReadAllText(savePath)) : null; }
            catch { return null; }
        }

        void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                if (game == null || game.IsSolved) { if (File.Exists(savePath)) File.Delete(savePath); return; }
                string tmp = savePath + ".tmp";
                File.WriteAllText(tmp, game.Serialize());
                if (File.Exists(savePath)) File.Delete(savePath);
                File.Move(tmp, savePath);
            }
            catch { /* saving is best effort */ }
        }

        // ---------- Actions ----------

        void StartNew(Difficulty level)
        {
            Cursor = Cursors.WaitCursor;
            try { game = Game.NewGame(level, rng); }
            finally { Cursor = Cursors.Default; }
            selected = 40;
            screen = Screen.Playing;
            Save();
            Invalidate();
        }

        void PlaceNumber(int value)
        {
            if (screen != Screen.Playing || game == null) return;
            if (game.Set(selected, value)) AfterChange();
        }

        void UndoMove()
        {
            if (screen != Screen.Playing || game == null) return;
            int cell = game.Undo();
            if (cell >= 0) { selected = cell; AfterChange(); }
        }

        void AfterChange()
        {
            if (game.IsSolved) screen = Screen.Solved;
            Save();
            Invalidate();
        }

        void ShowMenu()
        {
            screen = Screen.Menu;
            Invalidate();
        }

        // ---------- Input ----------

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (screen == Screen.Playing)
            {
                int r = Sudoku.Row(selected), c = Sudoku.Col(selected);
                switch (keyData)
                {
                    case Keys.Up: r = (r + 8) % 9; break;
                    case Keys.Down: r = (r + 1) % 9; break;
                    case Keys.Left: c = (c + 8) % 9; break;
                    case Keys.Right: c = (c + 1) % 9; break;
                    case Keys.Control | Keys.Z: UndoMove(); return true;
                    default: return base.ProcessCmdKey(ref msg, keyData);
                }
                selected = r * 9 + c;
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (screen != Screen.Playing) return;
            Keys k = e.KeyCode;
            if (k >= Keys.D1 && k <= Keys.D9) PlaceNumber(k - Keys.D0);
            else if (k >= Keys.NumPad1 && k <= Keys.NumPad9) PlaceNumber(k - Keys.NumPad0);
            else if (k == Keys.Delete || k == Keys.Back || k == Keys.D0 || k == Keys.NumPad0 || k == Keys.Space) PlaceNumber(0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;

            pressed = HitButton(e.Location);
            if (pressed != null) { Invalidate(); return; }

            if (screen == Screen.Playing && board.Contains(e.Location))
            {
                float cell = board.Width / 9f;
                int c = Math.Min(8, (int)((e.X - board.X) / cell));
                int r = Math.Min(8, (int)((e.Y - board.Y) / cell));
                selected = r * 9 + c;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Button b = pressed;
            pressed = null;
            if (b != null)
            {
                Invalidate();
                if (b.Enabled && b.Rect.Contains(e.Location)) b.OnClick();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Button b = HitButton(e.Location);
            if (b != hover) { hover = b; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null) { hover = null; Invalidate(); }
        }

        Button HitButton(Point p)
        {
            foreach (Button b in buttons)
                if (b.Enabled && b.Rect.Contains(p)) return b;
            return null;
        }

        // ---------- Layout and drawing ----------

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Paper);

            // Keep the hovered/pressed buttons matched up after rebuilding the list.
            string hoverText = hover != null ? hover.Text : null;
            string pressedText = pressed != null ? pressed.Text : null;
            buttons.Clear();

            if (screen == Screen.Menu) LayoutMenu(g);
            else LayoutGame(g);

            hover = buttons.Find(b => b.Text == hoverText);
            pressed = buttons.Find(b => b.Text == pressedText);
            foreach (Button b in buttons) DrawButton(g, b);
        }

        void LayoutMenu(Graphics g)
        {
            Size cs = ClientSize;
            int unit = Math.Min(cs.Height, (int)(cs.Width * 0.75));
            int w = (int)(unit * 0.7);
            int h = (int)(unit * 0.11);
            int gap = (int)(h * 0.3);
            int x = (cs.Width - w) / 2;
            int y = (int)(cs.Height * 0.06);

            DrawText(g, "Sudoku", new Rectangle(0, y, cs.Width, (int)(unit * 0.15)), unit * 0.11f, Ink, true);
            y += (int)(unit * 0.17);

            if (game != null && !game.IsSolved)
            {
                buttons.Add(new Button
                {
                    Rect = new Rectangle(x, y, w, h),
                    Text = "Back to my puzzle",
                    Back = GreenButton,
                    OnClick = () => { screen = Screen.Playing; Invalidate(); },
                });
                y += h + gap * 2;
            }

            DrawText(g, "Start a new puzzle:", new Rectangle(0, y, cs.Width, (int)(h * 0.8)), h * 0.42f, Ink, false);
            y += (int)(h * 0.9);

            AddLevelButton("Easy", Difficulty.Easy, x, ref y, w, h, gap);
            AddLevelButton("Medium", Difficulty.Medium, x, ref y, w, h, gap);
            AddLevelButton("Hard", Difficulty.Hard, x, ref y, w, h, gap);
        }

        void AddLevelButton(string text, Difficulty level, int x, ref int y, int w, int h, int gap)
        {
            buttons.Add(new Button { Rect = new Rectangle(x, y, w, h), Text = text, OnClick = () => StartNew(level) });
            y += h + gap;
        }

        void LayoutGame(Graphics g)
        {
            Size cs = ClientSize;
            int m = (int)(Math.Min(cs.Width, cs.Height) * 0.04);
            int size = Math.Min(cs.Height - 2 * m, (int)((cs.Width - 3 * m) * 0.62));
            size -= size % 9; // whole-pixel cells keep the lines crisp
            board = new Rectangle(m, (cs.Height - size) / 2, size, size);
            DrawBoard(g);

            // Right-hand panel
            int px = board.Right + m;
            int pw = cs.Width - px - m;
            int y = board.Top;
            int gap = Math.Max(6, size / 60);

            string levelName = game.Level == Difficulty.Easy ? "Easy" : game.Level == Difficulty.Medium ? "Medium" : "Hard";
            int titleH = (int)(size * 0.09);
            DrawText(g, levelName + " puzzle", new Rectangle(px, y, pw, titleH), titleH * 0.6f, Ink, true);
            y += titleH + gap;

            // Number pad, 3 x 3
            int padSize = Math.Min(pw, (int)(size * 0.52));
            int b = (padSize - 2 * gap) / 3;
            int padX = px + (pw - (3 * b + 2 * gap)) / 2;
            for (int v = 1; v <= 9; v++)
            {
                int value = v;
                bool finished = game.CorrectCount(v) >= 9;
                buttons.Add(new Button
                {
                    Rect = new Rectangle(padX + ((v - 1) % 3) * (b + gap), y + ((v - 1) / 3) * (b + gap), b, b),
                    Text = v.ToString(),
                    Fore = finished ? Disabled : UserInk,
                    FontScale = 0.62f,
                    Enabled = screen == Screen.Playing,
                    OnClick = () => PlaceNumber(value),
                });
            }
            y += 3 * b + 2 * gap + gap * 2;

            int rowH = (int)(size * 0.1);
            int half = (pw - gap) / 2;
            bool canErase = screen == Screen.Playing && !game.IsGiven(selected) && game.Cells[selected] != 0;
            buttons.Add(new Button
            {
                Rect = new Rectangle(px, y, half, rowH), Text = "Erase",
                Enabled = canErase, Fore = canErase ? Ink : Disabled,
                OnClick = () => PlaceNumber(0),
            });
            bool canUndo = screen == Screen.Playing && game.CanUndo;
            buttons.Add(new Button
            {
                Rect = new Rectangle(px + half + gap, y, pw - half - gap, rowH), Text = "Undo",
                Enabled = canUndo, Fore = canUndo ? Ink : Disabled,
                OnClick = UndoMove,
            });
            y += rowH + gap;

            buttons.Add(new Button
            {
                Rect = new Rectangle(px, y, pw, rowH), Text = "New Puzzle",
                OnClick = ShowMenu,
            });
            y += rowH + gap;

            if (screen == Screen.Playing && y + rowH <= board.Bottom)
                DrawText(g, "Click a square, then a number.", new Rectangle(px, y, pw, board.Bottom - y),
                         Math.Min(rowH * 0.32f, pw / 18f), Color.FromArgb(90, 90, 90), false);

            if (screen == Screen.Solved) DrawSolved(g);
        }

        void DrawBoard(Graphics g)
        {
            float cell = board.Width / 9f;
            int selValue = game.Cells[selected];
            bool playing = screen == Screen.Playing;

            using (var font = new Font("Segoe UI", cell * 0.62f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var bold = new Font("Segoe UI", cell * 0.62f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                for (int i = 0; i < 81; i++)
                {
                    var r = new Rectangle(board.X + (int)(Sudoku.Col(i) * cell), board.Y + (int)(Sudoku.Row(i) * cell),
                                          (int)cell, (int)cell);
                    Color back = Color.White;
                    if (playing)
                    {
                        if (Sudoku.SameUnit(i, selected)) back = PeerBack;
                        if (selValue != 0 && game.Cells[i] == selValue) back = SameNumberBack;
                    }
                    if (game.IsWrong(i)) back = WrongBack;
                    if (playing && i == selected) back = SelectedBack;
                    using (var br = new SolidBrush(back)) g.FillRectangle(br, r);

                    int v = game.Cells[i];
                    if (v != 0)
                    {
                        Color fore = game.IsGiven(i) ? Ink : game.IsWrong(i) ? WrongInk : UserInk;
                        TextRenderer.DrawText(g, v.ToString(), game.IsGiven(i) ? bold : font, r, fore,
                            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    }
                }
            }

            g.SmoothingMode = SmoothingMode.None;
            float thin = Math.Max(1f, cell / 40f);
            float thick = Math.Max(3f, cell / 12f);
            using (var thinPen = new Pen(ThinLine, thin))
            using (var thickPen = new Pen(Ink, thick))
            {
                for (int k = 0; k <= 9; k++)
                {
                    if (k % 3 == 0) continue;
                    int p = (int)(k * cell);
                    g.DrawLine(thinPen, board.X + p, board.Y, board.X + p, board.Bottom);
                    g.DrawLine(thinPen, board.X, board.Y + p, board.Right, board.Y + p);
                }
                for (int k = 0; k <= 9; k += 3)
                {
                    int p = (int)(k * cell);
                    g.DrawLine(thickPen, board.X + p, board.Y - thick / 2, board.X + p, board.Bottom + thick / 2);
                    g.DrawLine(thickPen, board.X - thick / 2, board.Y + p, board.Right + thick / 2, board.Y + p);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;
        }

        void DrawSolved(Graphics g)
        {
            int w = (int)(board.Width * 0.8), h = (int)(board.Height * 0.42);
            var card = new Rectangle(board.X + (board.Width - w) / 2, board.Y + (board.Height - h) / 2, w, h);
            using (var shade = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillRectangle(shade, board);
            FillRounded(g, card, GreenButton, Color.FromArgb(60, 120, 60), h / 10);
            DrawText(g, "Well done!", new Rectangle(card.X, card.Y + h / 10, w, h * 2 / 5), h * 0.24f, Ink, true);
            int bw = (int)(w * 0.7), bh = (int)(h * 0.28);
            buttons.Add(new Button
            {
                Rect = new Rectangle(card.X + (w - bw) / 2, card.Bottom - bh - h / 8, bw, bh),
                Text = "Play again",
                OnClick = ShowMenu,
            });
        }

        void DrawButton(Graphics g, Button b)
        {
            Color back = b.Back;
            if (b.Enabled && b == pressed) back = Blend(back, Color.FromArgb(150, 180, 230), 0.5f);
            else if (b.Enabled && b == hover) back = Blend(back, Color.FromArgb(200, 220, 250), 0.6f);
            FillRounded(g, b.Rect, back, b.Enabled ? Color.FromArgb(110, 110, 110) : Disabled,
                        Math.Max(6, b.Rect.Height / 6));
            DrawText(g, b.Text, b.Rect, Math.Min(b.Rect.Height * b.FontScale, b.Rect.Width / (b.Text.Length * 0.62f + 0.6f)),
                     b.Fore, true);
        }

        static void FillRounded(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            using (var path = new GraphicsPath())
            {
                int d = radius * 2;
                path.AddArc(r.X, r.Y, d, d, 180, 90);
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                path.CloseFigure();
                using (var br = new SolidBrush(fill)) g.FillPath(br, path);
                using (var pen = new Pen(border, Math.Max(2f, r.Height / 40f))) g.DrawPath(pen, path);
            }
        }

        static void DrawText(Graphics g, string text, Rectangle r, float px, Color color, bool bold)
        {
            using (var font = new Font("Segoe UI", Math.Max(8f, px), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, text, font, r, color,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
        }

        static Color Blend(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }
}
