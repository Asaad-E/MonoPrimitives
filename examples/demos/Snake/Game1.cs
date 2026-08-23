#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Snake;

/// <summary>
/// Small Snake demo: no menus, no pause, no music -- classic grid movement, growing on food,
/// game over on hitting a wall or itself. Movement reads <see cref="PrimitiveInput"/> directly.
/// A <see cref="BoxingViewportAdapter2D"/> keeps the grid letterboxed correctly at any window
/// size; no camera movement is needed at all for a single fixed-view grid game like this one.
/// </summary>
public class Game1 : Game
{
    private const int Cols = 24;
    private const int Rows = 18;
    private const int CellSize = 24;
    private const int VirtualWidth = Cols * CellSize;
    private const int VirtualHeight = Rows * CellSize;
    private const int WindowWidth = 864;
    private const int WindowHeight = 648;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private BoxingViewportAdapter2D _adapter = null!;

    private readonly List<Point> _snake = new();
    private Point _direction;
    private Point _pendingDirection;
    private Point _food;
    private readonly Random _rng = new();

    private float _moveTimer;
    private float _moveInterval = 0.14f;
    private int _score;
    private bool _gameOver;
    private bool _grow;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        _adapter = new BoxingViewportAdapter2D(GraphicsDevice, VirtualWidth, VirtualHeight);
        ResetGame();
        base.Initialize();
    }

    private void ResetGame()
    {
        _snake.Clear();
        _snake.Add(new Point(Cols / 2, Rows / 2));
        _snake.Add(new Point(Cols / 2 - 1, Rows / 2));
        _snake.Add(new Point(Cols / 2 - 2, Rows / 2));
        _direction = new Point(1, 0);
        _pendingDirection = _direction;
        _score = 0;
        _gameOver = false;
        _grow = false;
        _moveTimer = 0f;
        _moveInterval = 0.14f;
        PlaceFood();
    }

    private void PlaceFood()
    {
        Point candidate;
        do
        {
            candidate = new Point(_rng.Next(Cols), _rng.Next(Rows));
        } while (_snake.Contains(candidate));
        _food = candidate;
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
        ReadDirectionInput();

        _moveTimer += dt;
        if (_moveTimer < _moveInterval) return;
        _moveTimer = 0f;

        _direction = _pendingDirection;
        Point head = _snake[0];
        Point newHead = new(head.X + _direction.X, head.Y + _direction.Y);

        if (newHead.X < 0 || newHead.X >= Cols || newHead.Y < 0 || newHead.Y >= Rows || _snake.Contains(newHead))
        {
            _gameOver = true;
            return;
        }

        _snake.Insert(0, newHead);
        if (newHead == _food)
        {
            _score += 10;
            _grow = true;
            _moveInterval = MathF.Max(0.06f, _moveInterval - 0.004f);
        }

        if (_grow) _grow = false;
        else _snake.RemoveAt(_snake.Count - 1);

        if (newHead == _food) PlaceFood();
    }

    // A direction reversal (e.g. Down while moving Up) is rejected -- would run the snake
    // straight into its own neck -- but the input is still latched for the next tick rather
    // than dropped, so a quick double key-tap still works as expected.
    private void ReadDirectionInput()
    {
        Point requested = _pendingDirection;
        if (_input.IsKeyPressed(Keys.Left) || _input.IsKeyPressed(Keys.A)) requested = new Point(-1, 0);
        else if (_input.IsKeyPressed(Keys.Right) || _input.IsKeyPressed(Keys.D)) requested = new Point(1, 0);
        else if (_input.IsKeyPressed(Keys.Up) || _input.IsKeyPressed(Keys.W)) requested = new Point(0, -1);
        else if (_input.IsKeyPressed(Keys.Down) || _input.IsKeyPressed(Keys.S)) requested = new Point(0, 1);

        bool isReversal = requested.X == -_direction.X && requested.Y == -_direction.Y;
        if (!isReversal) _pendingDirection = requested;
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin(_adapter.GetScaleMatrix());

        for (int i = 0; i < _snake.Count; i++)
        {
            Color color = i == 0 ? Palette.Emerald : Palette.Nephritis;
            DrawCell(_snake[i], color);
        }
        DrawCell(_food, Palette.Alizarin);

        _batch2d.DrawString($"Score: {_score}", new Vector2(6, 4), 1.3f, Color.White);

        if (_gameOver)
        {
            string text = "GAME OVER -- press R";
            Vector2 size = new(text.Length * 6f * 1.6f, 7f * 1.6f);
            _batch2d.DrawString(text, new Vector2((VirtualWidth - size.X) * 0.5f, (VirtualHeight - size.Y) * 0.5f), 1.6f, Palette.Alizarin);
        }

        _batch2d.End();
        base.Draw(gameTime);
    }

    private void DrawCell(Point cell, Color color)
        => _batch2d.FillRectangleRounded(new Vector2(cell.X * CellSize + 1, cell.Y * CellSize + 1), new Vector2(CellSize - 2, CellSize - 2), 4f, color);
}
