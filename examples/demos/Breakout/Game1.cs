#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace Breakout;

/// <summary>
/// Small Breakout/Arkanoid demo: no menus, no pause, no music -- just paddle, ball, bricks.
/// Movement reads <see cref="PrimitiveInput"/> directly (not <c>Camera2D.UpdateWithInput</c>'s
/// convenience controller, which is a prototyping shortcut, not how a real game drives input).
/// A <see cref="BoxingViewportAdapter2D"/> keeps the portrait playfield letterboxed correctly at
/// any window size; <see cref="Camera2D"/> is used passively, purely for its screen-shake juice
/// on brick hits, never for movement.
/// </summary>
public class Game1 : Game
{
    private const int VirtualWidth = 480;
    private const int VirtualHeight = 640;
    private const int WindowWidth = 720;
    private const int WindowHeight = 960;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;
    private BoxingViewportAdapter2D _adapter = null!;
    private Camera2D _camera2d = null!;

    private const float PaddleWidth = 90f;
    private const float PaddleHeight = 14f;
    private const float PaddleSpeed = 420f;
    private const float BallRadius = 8f;
    private const float BallSpeed = 320f;

    private Vector2 _paddlePos;
    private Vector2 _ballPos;
    private Vector2 _ballVelocity;
    private int _lives;
    private bool _gameOver;
    private bool _won;

    private const int BrickCols = 8;
    private const int BrickRows = 5;
    private const float BrickWidth = 52f;
    private const float BrickHeight = 20f;
    private const float BrickGap = 4f;
    private const float BrickTop = 60f;
    private bool[,] _bricksAlive = new bool[BrickCols, BrickRows];
    private int _bricksRemaining;

    private static readonly Color[] RowColors = { Palette.Alizarin, Palette.Carrot, Palette.Sunflower, Palette.Emerald, Palette.PeterRiver };

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
        _camera2d = new Camera2D(_adapter, target: new Vector2(VirtualWidth * 0.5f, VirtualHeight * 0.5f));
        ResetGame();
        base.Initialize();
    }

    private void ResetGame()
    {
        _paddlePos = new Vector2(VirtualWidth * 0.5f, VirtualHeight - 40f);
        LaunchBall();
        _lives = 3;
        _gameOver = false;
        _won = false;

        _bricksRemaining = 0;
        for (int row = 0; row < BrickRows; row++)
            for (int col = 0; col < BrickCols; col++)
            {
                _bricksAlive[col, row] = true;
                _bricksRemaining++;
            }
    }

    private void LaunchBall()
    {
        _ballPos = _paddlePos - new Vector2(0, PaddleHeight * 0.5f + BallRadius + 1f);
        _ballVelocity = Vector2.Normalize(new Vector2(0.5f, -1f)) * BallSpeed;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 30f);
        _input.Update(dt);
        _camera2d.Update(dt); // passive: only decays screen-shake trauma, no movement

        if (_input.IsKeyPressed(Keys.R) && (_gameOver || _won))
            ResetGame();

        if (!_gameOver && !_won)
        {
            UpdatePaddle(dt);
            UpdateBall(dt);
        }

        base.Update(gameTime);
    }

    private void UpdatePaddle(float dt)
    {
        float axis = _input.GetAxis(Keys.A, Keys.D) + _input.GetAxis(Keys.Left, Keys.Right);
        _paddlePos.X = Math.Clamp(_paddlePos.X + axis * PaddleSpeed * dt, PaddleWidth * 0.5f, VirtualWidth - PaddleWidth * 0.5f);
    }

    private void UpdateBall(float dt)
    {
        _ballPos += _ballVelocity * dt;

        if (_ballPos.X - BallRadius < 0f) { _ballPos.X = BallRadius; _ballVelocity.X = MathF.Abs(_ballVelocity.X); }
        else if (_ballPos.X + BallRadius > VirtualWidth) { _ballPos.X = VirtualWidth - BallRadius; _ballVelocity.X = -MathF.Abs(_ballVelocity.X); }
        if (_ballPos.Y - BallRadius < 0f) { _ballPos.Y = BallRadius; _ballVelocity.Y = MathF.Abs(_ballVelocity.Y); }

        if (_ballPos.Y - BallRadius > VirtualHeight)
        {
            _lives--;
            if (_lives <= 0) _gameOver = true;
            else LaunchBall();
            return;
        }

        Rectangle paddleRect = RectAround(_paddlePos, PaddleWidth, PaddleHeight);
        if (_ballVelocity.Y > 0f && Collision2D.CheckCollisionCircleRec(_ballPos, BallRadius, paddleRect))
        {
            // Classic paddle bounce: hit position relative to paddle center steers the angle.
            float hit = Math.Clamp((_ballPos.X - _paddlePos.X) / (PaddleWidth * 0.5f), -1f, 1f);
            Vector2 dir = Vector2.Normalize(new Vector2(hit, -1f));
            _ballVelocity = dir * BallSpeed;
            _ballPos.Y = paddleRect.Top - BallRadius;
        }

        for (int row = 0; row < BrickRows; row++)
        {
            for (int col = 0; col < BrickCols; col++)
            {
                if (!_bricksAlive[col, row]) continue;
                Rectangle brickRect = BrickRect(col, row);
                if (!Collision2D.CheckCollisionCircleRec(_ballPos, BallRadius, brickRect)) continue;

                _bricksAlive[col, row] = false;
                _bricksRemaining--;
                _camera2d.AddTrauma(0.25f);
                ReflectOffRect(brickRect);

                if (_bricksRemaining <= 0) _won = true;
                row = BrickRows; // one brick per frame is enough; avoids double-hits off the same corner
                break;
            }
        }
    }

    // Reflects _ballVelocity off whichever side of rect is closest to the ball -- a simple
    // closest-point-on-AABB normal, good enough for a brick-breaker's blocky collisions.
    private void ReflectOffRect(Rectangle rect)
    {
        float closestX = Math.Clamp(_ballPos.X, rect.Left, rect.Right);
        float closestY = Math.Clamp(_ballPos.Y, rect.Top, rect.Bottom);
        Vector2 delta = _ballPos - new Vector2(closestX, closestY);

        if (MathF.Abs(delta.X) > MathF.Abs(delta.Y))
            _ballVelocity.X = MathF.Sign(delta.X == 0f ? _ballVelocity.X : delta.X) * MathF.Abs(_ballVelocity.X);
        else
            _ballVelocity.Y = MathF.Sign(delta.Y == 0f ? _ballVelocity.Y : delta.Y) * MathF.Abs(_ballVelocity.Y);
    }

    private static Rectangle RectAround(Vector2 center, float width, float height)
        => new((int)(center.X - width * 0.5f), (int)(center.Y - height * 0.5f), (int)width, (int)height);

    private static Rectangle BrickRect(int col, int row)
    {
        float totalWidth = BrickCols * (BrickWidth + BrickGap) - BrickGap;
        float left = (VirtualWidth - totalWidth) * 0.5f;
        float x = left + col * (BrickWidth + BrickGap);
        float y = BrickTop + row * (BrickHeight + BrickGap);
        return new Rectangle((int)x, (int)y, (int)BrickWidth, (int)BrickHeight);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        _batch2d.Begin(_camera2d.GetTransformMatrix());

        _batch2d.BorderRectangle(0, 0, VirtualWidth, VirtualHeight, Palette.Asbestos, 2f);

        for (int row = 0; row < BrickRows; row++)
            for (int col = 0; col < BrickCols; col++)
                if (_bricksAlive[col, row])
                {
                    Rectangle r = BrickRect(col, row);
                    _batch2d.FillRectangleRounded(new Vector2(r.X, r.Y), new Vector2(r.Width, r.Height), 3f, RowColors[row % RowColors.Length]);
                }

        _batch2d.FillRectangleRounded(_paddlePos - new Vector2(PaddleWidth, PaddleHeight) * 0.5f, new Vector2(PaddleWidth, PaddleHeight), 5f, Palette.Clouds);
        _batch2d.FillCircle(_ballPos, BallRadius, Palette.Sunflower);

        _batch2d.DrawString($"Lives: {_lives}   Bricks: {_bricksRemaining}", new Vector2(10, VirtualHeight - 24), 1.4f, Palette.Silver);

        if (_gameOver)
            CenteredMessage("GAME OVER -- press R", Palette.Alizarin);
        if (_won)
            CenteredMessage("YOU WIN -- press R", Palette.Emerald);

        _batch2d.End();
        base.Draw(gameTime);
    }

    private void CenteredMessage(string text, Color color)
    {
        Vector2 size = DebugFont5x7TextSize(text, 2.2f);
        _batch2d.DrawString(text, new Vector2((VirtualWidth - size.X) * 0.5f, VirtualHeight * 0.5f), 2.2f, color);
    }

    private static Vector2 DebugFont5x7TextSize(string text, float pixelSize) => new(text.Length * 6f * pixelSize, 7f * pixelSize);
}
