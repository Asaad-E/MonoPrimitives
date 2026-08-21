#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Tetris;

/// <summary>
/// Small Tetris demo: no menus, no pause, no music -- a 10x20 grid, the 7 standard pieces, line
/// clears. Movement reads <see cref="PrimitiveInput"/> directly, not a camera controller (this
/// demo doesn't even need a camera -- the whole board fits one static screen). A
/// <see cref="BoxingViewportAdapter2D"/> keeps the board+sidebar layout letterboxed correctly at
/// any window size.
/// </summary>
public class Game1 : Game
{
    private const int Cols = 10;
    private const int Rows = 20;
    private const int CellSize = 28;
    private const int BoardLeft = 20;
    private const int BoardTop = 20;
    private const int VirtualWidth = 440;
    private const int VirtualHeight = 600;
    private const int WindowWidth = 660;
    private const int WindowHeight = 900;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private BoxingViewportAdapter2D _adapter = null!;

    private enum PieceType { I, O, T, S, Z, J, L }

    // Each piece's rotation states as 4 cell offsets within a small bounding box; states.Length
    // is how many distinct rotations that piece actually has (O:1, I/S/Z:2, T/J/L:4) -- rotating
    // just wraps the index modulo that count instead of storing 4 identical copies.
    private static readonly (int x, int y)[][][] Shapes =
    {
        // I
        new[] { new[] { (0, 1), (1, 1), (2, 1), (3, 1) }, new[] { (2, 0), (2, 1), (2, 2), (2, 3) } },
        // O
        new[] { new[] { (1, 0), (2, 0), (1, 1), (2, 1) } },
        // T
        new[] { new[] { (1, 0), (0, 1), (1, 1), (2, 1) }, new[] { (1, 0), (1, 1), (2, 1), (1, 2) }, new[] { (0, 1), (1, 1), (2, 1), (1, 2) }, new[] { (1, 0), (0, 1), (1, 1), (1, 2) } },
        // S
        new[] { new[] { (1, 0), (2, 0), (0, 1), (1, 1) }, new[] { (1, 0), (1, 1), (2, 1), (2, 2) } },
        // Z
        new[] { new[] { (0, 0), (1, 0), (1, 1), (2, 1) }, new[] { (2, 0), (1, 1), (2, 1), (1, 2) } },
        // J
        new[] { new[] { (0, 0), (0, 1), (1, 1), (2, 1) }, new[] { (1, 0), (2, 0), (1, 1), (1, 2) }, new[] { (0, 1), (1, 1), (2, 1), (2, 2) }, new[] { (1, 0), (1, 1), (0, 2), (1, 2) } },
        // L
        new[] { new[] { (2, 0), (0, 1), (1, 1), (2, 1) }, new[] { (1, 0), (1, 1), (1, 2), (2, 2) }, new[] { (0, 1), (1, 1), (2, 1), (0, 2) }, new[] { (0, 0), (1, 0), (1, 1), (1, 2) } },
    };

    private static readonly Color[] PieceColors = { Palette.Turquoise, Palette.Sunflower, Palette.Amethyst, Palette.Emerald, Palette.Alizarin, Palette.BelizeHole, Palette.Carrot };

    private PieceType?[,] _board = new PieceType?[Cols, Rows];
    private PieceType _currentType;
    private PieceType _nextType;
    private int _rotation;
    private int _pieceCol, _pieceRow;

    private readonly Random _rng = new();
    private float _fallTimer;
    private float _fallInterval = 0.6f;
    private int _linesCleared;
    private int _score;
    private bool _gameOver;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _adapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        ResetGame();
        base.Initialize();
    }

    private void ResetGame()
    {
        _board = new PieceType?[Cols, Rows];
        _linesCleared = 0;
        _score = 0;
        _fallInterval = 0.6f;
        _gameOver = false;
        _nextType = RandomPiece();
        SpawnPiece();
    }

    private PieceType RandomPiece() => (PieceType)_rng.Next(7);

    private void SpawnPiece()
    {
        _currentType = _nextType;
        _nextType = RandomPiece();
        _rotation = 0;
        _pieceCol = Cols / 2 - 2;
        _pieceRow = 0;

        if (!FitsAt(_pieceCol, _pieceRow, _rotation))
            _gameOver = true;
    }

    private (int x, int y)[] CurrentCells(int rotation) => Shapes[(int)_currentType][rotation % Shapes[(int)_currentType].Length];

    private bool FitsAt(int col, int row, int rotation)
    {
        foreach ((int x, int y) in CurrentCells(rotation))
        {
            int gx = col + x, gy = row + y;
            if (gx < 0 || gx >= Cols || gy < 0 || gy >= Rows) return false;
            if (_board[gx, gy].HasValue) return false;
        }
        return true;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);
        _input.Update(dt);

        if (_input.IsKeyPressed(Keys.R) && _gameOver)
            ResetGame();

        if (!_gameOver)
            UpdatePlaying(dt);

        base.Update(gameTime);
    }

    private void UpdatePlaying(float dt)
    {
        if (_input.IsKeyPressed(Keys.Left) || _input.IsKeyPressed(Keys.A))
            if (FitsAt(_pieceCol - 1, _pieceRow, _rotation)) _pieceCol--;

        if (_input.IsKeyPressed(Keys.Right) || _input.IsKeyPressed(Keys.D))
            if (FitsAt(_pieceCol + 1, _pieceRow, _rotation)) _pieceCol++;

        if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W))
            TryRotate();

        if (_input.IsKeyPressed(Keys.Space))
        {
            while (FitsAt(_pieceCol, _pieceRow + 1, _rotation)) _pieceRow++;
            LockPiece();
            return;
        }

        float fallSpeedMultiplier = (_input.IsKeyDown(Keys.Down) || _input.IsKeyDown(Keys.S)) ? 8f : 1f;
        _fallTimer += dt * fallSpeedMultiplier;
        if (_fallTimer >= _fallInterval)
        {
            _fallTimer = 0f;
            if (FitsAt(_pieceCol, _pieceRow + 1, _rotation))
                _pieceRow++;
            else
                LockPiece();
        }
    }

    // Naive kick: try the rotation in place, then nudged one cell left/right -- enough for a
    // basic demo without a full SRS kick table.
    private void TryRotate()
    {
        int next = _rotation + 1;
        Span<int> kickOffsets = stackalloc int[] { 0, -1, 1 };
        foreach (int colOffset in kickOffsets)
        {
            if (FitsAt(_pieceCol + colOffset, _pieceRow, next))
            {
                _pieceCol += colOffset;
                _rotation = next;
                return;
            }
        }
    }

    private void LockPiece()
    {
        foreach ((int x, int y) in CurrentCells(_rotation))
            _board[_pieceCol + x, _pieceRow + y] = _currentType;

        ClearLines();
        SpawnPiece();
    }

    private void ClearLines()
    {
        int cleared = 0;
        for (int row = Rows - 1; row >= 0; row--)
        {
            bool full = true;
            for (int col = 0; col < Cols; col++)
                if (!_board[col, row].HasValue) { full = false; break; }

            if (!full) continue;

            cleared++;
            for (int y = row; y > 0; y--)
                for (int col = 0; col < Cols; col++)
                    _board[col, y] = _board[col, y - 1];
            for (int col = 0; col < Cols; col++)
                _board[col, 0] = null;
            row++; // re-check this row index now that rows above shifted down into it
        }

        if (cleared > 0)
        {
            _linesCleared += cleared;
            _score += cleared switch { 1 => 100, 2 => 300, 3 => 500, _ => 800 };
            _fallInterval = MathF.Max(0.15f, 0.6f - _linesCleared * 0.02f);
        }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin(_adapter.GetScaleMatrix());

        Rectangle boardRect = new(BoardLeft, BoardTop, Cols * CellSize, Rows * CellSize);
        _batch2d.BorderRectangle(boardRect.X, boardRect.Y, boardRect.Width, boardRect.Height, Palette.Asbestos, 2f);

        for (int col = 0; col < Cols; col++)
            for (int row = 0; row < Rows; row++)
                if (_board[col, row] is PieceType placed)
                    DrawCell(col, row, PieceColors[(int)placed]);

        if (!_gameOver)
            foreach ((int x, int y) in CurrentCells(_rotation))
                DrawCell(_pieceCol + x, _pieceRow + y, PieceColors[(int)_currentType]);

        DrawSidebar();

        if (_gameOver)
        {
            _batch2d.DrawString("GAME OVER", new Vector2(BoardLeft + 40, VirtualHeight * 0.5f - 20), 2.4f, Palette.Alizarin);
            _batch2d.DrawString("press R", new Vector2(BoardLeft + 70, VirtualHeight * 0.5f + 14), 1.6f, Palette.Silver);
        }

        _batch2d.End();
        base.Draw(gameTime);
    }

    private void DrawCell(int col, int row, Color color)
    {
        float x = BoardLeft + col * CellSize;
        float y = BoardTop + row * CellSize;
        _batch2d.FillRectangleRounded(new Vector2(x + 1, y + 1), new Vector2(CellSize - 2, CellSize - 2), 3f, color);
    }

    private void DrawSidebar()
    {
        float x = BoardLeft + Cols * CellSize + 24;
        _batch2d.DrawString("NEXT", new Vector2(x, BoardTop), 1.6f, Color.White);
        foreach ((int px, int py) in Shapes[(int)_nextType][0])
        {
            float cx = x + px * (CellSize - 6);
            float cy = BoardTop + 26 + py * (CellSize - 6);
            _batch2d.FillRectangleRounded(new Vector2(cx, cy), new Vector2(CellSize - 8, CellSize - 8), 2f, PieceColors[(int)_nextType]);
        }

        _batch2d.DrawString($"SCORE\n{_score}", new Vector2(x, BoardTop + 150), 1.6f, Palette.Silver);
        _batch2d.DrawString($"LINES\n{_linesCleared}", new Vector2(x, BoardTop + 200), 1.6f, Palette.Silver);
        _batch2d.DrawString("A/D: move\nW: rotate\nS: soft drop\nSpace: hard drop", new Vector2(x, BoardTop + 260), 1.2f, Palette.Concrete);
    }
}
