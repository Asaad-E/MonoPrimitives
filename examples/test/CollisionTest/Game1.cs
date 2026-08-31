#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;

namespace CollisionTest;

/// <summary>
/// Visual test for every <see cref="Collision2D"/> overlap/ray check, including the polygon and
/// mixed-shape ones (Polys/RecPoly/RecTriangle/Triangles via SAT, CirclePoly/CircleTriangle
/// via the non-SAT closest-point approach — the star-shaped polygon in modes 9/I is deliberately
/// concave, to show those still work where SAT wouldn't) and every Capsule check (CircleCapsule,
/// Capsules, CapsuleRectangle/Triangle/Polygon, all via closest-point-between-segments): two
/// controllable points, A and B, reinterpreted per mode as a circle/rectangle/line/triangle/
/// polygon/capsule/ray-origin. One of the two always follows the mouse; the other is nudged with
/// W/A/S/D. Space swaps which is which, so "the fixed one you move with the keyboard" and "the one
/// the mouse moves" can be tried both ways round. Shapes turn red on overlap, green otherwise.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private GraphicsDeviceManager _graphics;
    private Primitive2DBatch _batch2d = null!;
    private PrimitiveInput _input = null!;

    private enum Mode
    {
        CircleCircle, CircleRectangle, CircleLine, PointTriangle, PointPolygon, RayCircle,
        PolygonPolygon, RectanglePolygon, CirclePolygon, TriangleTriangle, RectangleTriangle, CircleTriangle,
        CircleCapsule, CapsuleCapsule, CapsuleRectangle, CapsuleTriangle, CapsulePolygon
    }
    private Mode _mode = Mode.CircleCircle;

    private Vector2 _posA = new(400, 360);
    private Vector2 _posB = new(800, 360);
    private bool _mouseControlsA = true;

    private const float KeyboardMoveSpeed = 260f;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new Primitive2DBatch(GraphicsDevice);
        _input = new PrimitiveInput();
        base.Initialize();
    }

    protected override void Update(GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update(dt);

        if (_input.IsKeyPressed(Keys.D1)) _mode = Mode.CircleCircle;
        if (_input.IsKeyPressed(Keys.D2)) _mode = Mode.CircleRectangle;
        if (_input.IsKeyPressed(Keys.D3)) _mode = Mode.CircleLine;
        if (_input.IsKeyPressed(Keys.D4)) _mode = Mode.PointTriangle;
        if (_input.IsKeyPressed(Keys.D5)) _mode = Mode.PointPolygon;
        if (_input.IsKeyPressed(Keys.D6)) _mode = Mode.RayCircle;
        if (_input.IsKeyPressed(Keys.D7)) _mode = Mode.PolygonPolygon;
        if (_input.IsKeyPressed(Keys.D8)) _mode = Mode.RectanglePolygon;
        if (_input.IsKeyPressed(Keys.D9)) _mode = Mode.CirclePolygon;
        if (_input.IsKeyPressed(Keys.D0)) _mode = Mode.TriangleTriangle;
        if (_input.IsKeyPressed(Keys.Q)) _mode = Mode.RectangleTriangle;
        if (_input.IsKeyPressed(Keys.E)) _mode = Mode.CircleTriangle;
        if (_input.IsKeyPressed(Keys.R)) _mode = Mode.CircleCapsule;
        if (_input.IsKeyPressed(Keys.T)) _mode = Mode.CapsuleCapsule;
        if (_input.IsKeyPressed(Keys.Y)) _mode = Mode.CapsuleRectangle;
        if (_input.IsKeyPressed(Keys.U)) _mode = Mode.CapsuleTriangle;
        if (_input.IsKeyPressed(Keys.I)) _mode = Mode.CapsulePolygon;
        if (_input.IsKeyPressed(Keys.Space)) _mouseControlsA = !_mouseControlsA;

        Vector2 keyboardMove = _input.GetVector2(Keys.A, Keys.D, Keys.W, Keys.S) * KeyboardMoveSpeed * dt;

        if (_mouseControlsA)
        {
            _posA = _input.MousePosition;
            _posB += keyboardMove;
        }
        else
        {
            _posB = _input.MousePosition;
            _posA += keyboardMove;
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        bool colliding = _mode switch
        {
            Mode.CircleCircle => Collision2D.CheckCollisionCircles(_posA, 40f, _posB, 34f),
            Mode.CircleRectangle => Collision2D.CheckCollisionCircleRec(_posA, 36f, RectAround(_posB, 150f, 100f)),
            Mode.CircleLine => Collision2D.CheckCollisionCircleLine(_posA, 30f, _posB + new Vector2(-110, 0), _posB + new Vector2(110, 0)),
            Mode.PointTriangle => Collision2D.CheckCollisionPointTriangle(_posA, _posB + new Vector2(0, -70), _posB + new Vector2(-70, 60), _posB + new Vector2(70, 60)),
            Mode.PointPolygon => Collision2D.CheckCollisionPointPoly(_posA, StarAround(_posB, 90f, 38f)),
            Mode.RayCircle => Collision2D.CheckCollisionRayCircle(_posA, Vector2.UnitX, _posB, 40f, out _, out _),
            Mode.PolygonPolygon => Collision2D.CheckCollisionPolys(QuadAround(_posA, 60f), PentagonAround(_posB, 70f)),
            Mode.RectanglePolygon => Collision2D.CheckCollisionRecPoly(RectAround(_posA, 140f, 90f), PentagonAround(_posB, 70f)),
            Mode.CirclePolygon => Collision2D.CheckCollisionCirclePoly(_posA, 40f, StarAround(_posB, 90f, 38f)),
            Mode.TriangleTriangle => Collision2D.CheckCollisionTriangles(
                _posA + new Vector2(0, -60), _posA + new Vector2(-51, 42), _posA + new Vector2(51, 42),
                _posB + new Vector2(0, -60), _posB + new Vector2(-51, 42), _posB + new Vector2(51, 42)),
            Mode.RectangleTriangle => Collision2D.CheckCollisionRecTriangle(RectAround(_posA, 140f, 90f),
                _posB + new Vector2(0, -60), _posB + new Vector2(-51, 42), _posB + new Vector2(51, 42)),
            Mode.CircleTriangle => Collision2D.CheckCollisionCircleTriangle(_posA, 40f,
                _posB + new Vector2(0, -60), _posB + new Vector2(-51, 42), _posB + new Vector2(51, 42)),
            Mode.CircleCapsule => Collision2D.CheckCollisionCircleCapsule(_posA, 30f,
                _posB + new Vector2(-50, 0), _posB + new Vector2(50, 0), 25f),
            Mode.CapsuleCapsule => Collision2D.CheckCollisionCapsules(
                _posA + new Vector2(-45, -25), _posA + new Vector2(45, 25), 28f,
                _posB + new Vector2(-45, 25), _posB + new Vector2(45, -25), 28f),
            Mode.CapsuleRectangle => Collision2D.CheckCollisionCapsuleRec(
                _posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, RectAround(_posB, 140f, 90f)),
            Mode.CapsuleTriangle => Collision2D.CheckCollisionCapsuleTriangle(
                _posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f,
                _posB + new Vector2(0, -60), _posB + new Vector2(-51, 42), _posB + new Vector2(51, 42)),
            Mode.CapsulePolygon => Collision2D.CheckCollisionCapsulePoly(
                _posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, StarAround(_posB, 90f, 38f)),
            _ => false,
        };

        Color shapeColor = colliding ? Palette.Alizarin : Palette.Nephritis;

        _batch2d.Begin();
        DrawShapes(shapeColor);
        DrawMarkers();
        DrawHud(colliding);
        _batch2d.End();

        base.Draw(gameTime);
    }

    private void DrawShapes(Color color)
    {
        switch (_mode)
        {
            case Mode.CircleCircle:
                _batch2d.FillCircle(_posA, 40f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 40f, color, 2.5f);
                _batch2d.FillCircle(_posB, 34f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posB, 34f, color, 2.5f);
                break;

            case Mode.CircleRectangle:
                _batch2d.FillCircle(_posA, 36f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 36f, color, 2.5f);
                Rectangle rect = RectAround(_posB, 150f, 100f);
                _batch2d.FillRectangle(rect.X, rect.Y, rect.Width, rect.Height, ToAlpha(color, 0.5f));
                _batch2d.BorderRectangle(rect.X, rect.Y, rect.Width, rect.Height, color, 2.5f);
                break;

            case Mode.CircleLine:
                _batch2d.FillCircle(_posA, 30f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 30f, color, 2.5f);
                _batch2d.DrawLine(_posB + new Vector2(-110, 0), _posB + new Vector2(110, 0), 4f, color);
                break;

            case Mode.PointTriangle:
                Vector2 t1 = _posB + new Vector2(0, -70), t2 = _posB + new Vector2(-70, 60), t3 = _posB + new Vector2(70, 60);
                _batch2d.FillTriangle(t1, t2, t3, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(t1, t2, t3, color, 2.5f);
                _batch2d.FillCircle(_posA, 5f, color);
                break;

            case Mode.PointPolygon:
                Span<Vector2> star = stackalloc Vector2[10];
                StarAround(_posB, 90f, 38f).CopyTo(star);
                _batch2d.FillPolygon(star, ToAlpha(color, 0.5f));
                for (int i = 0; i < star.Length; i++)
                    _batch2d.DrawLine(star[i], star[(i + 1) % star.Length], 2.5f, color);
                _batch2d.FillCircle(_posA, 5f, color);
                break;

            case Mode.RayCircle:
                _batch2d.FillCircle(_posB, 40f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posB, 40f, color, 2.5f);
                _batch2d.DrawLine(_posA, _posA + Vector2.UnitX * WindowWidth, 2.5f, color);
                _batch2d.FillCircle(_posA, 6f, color);
                break;

            case Mode.PolygonPolygon:
                DrawPoly(QuadAround(_posA, 60f), color);
                DrawPoly(PentagonAround(_posB, 70f), color);
                break;

            case Mode.RectanglePolygon:
                Rectangle recPoly = RectAround(_posA, 140f, 90f);
                _batch2d.FillRectangle(recPoly.X, recPoly.Y, recPoly.Width, recPoly.Height, ToAlpha(color, 0.5f));
                _batch2d.BorderRectangle(recPoly.X, recPoly.Y, recPoly.Width, recPoly.Height, color, 2.5f);
                DrawPoly(PentagonAround(_posB, 70f), color);
                break;

            case Mode.CirclePolygon:
                _batch2d.FillCircle(_posA, 40f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 40f, color, 2.5f);
                DrawPoly(StarAround(_posB, 90f, 38f), color);
                break;

            case Mode.TriangleTriangle:
                Vector2 tt1a = _posA + new Vector2(0, -60), tt2a = _posA + new Vector2(-51, 42), tt3a = _posA + new Vector2(51, 42);
                Vector2 tt1b = _posB + new Vector2(0, -60), tt2b = _posB + new Vector2(-51, 42), tt3b = _posB + new Vector2(51, 42);
                _batch2d.FillTriangle(tt1a, tt2a, tt3a, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(tt1a, tt2a, tt3a, color, 2.5f);
                _batch2d.FillTriangle(tt1b, tt2b, tt3b, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(tt1b, tt2b, tt3b, color, 2.5f);
                break;

            case Mode.RectangleTriangle:
                Rectangle recTri = RectAround(_posA, 140f, 90f);
                _batch2d.FillRectangle(recTri.X, recTri.Y, recTri.Width, recTri.Height, ToAlpha(color, 0.5f));
                _batch2d.BorderRectangle(recTri.X, recTri.Y, recTri.Width, recTri.Height, color, 2.5f);
                Vector2 rt1 = _posB + new Vector2(0, -60), rt2 = _posB + new Vector2(-51, 42), rt3 = _posB + new Vector2(51, 42);
                _batch2d.FillTriangle(rt1, rt2, rt3, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(rt1, rt2, rt3, color, 2.5f);
                break;

            case Mode.CircleTriangle:
                _batch2d.FillCircle(_posA, 40f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 40f, color, 2.5f);
                Vector2 ct1 = _posB + new Vector2(0, -60), ct2 = _posB + new Vector2(-51, 42), ct3 = _posB + new Vector2(51, 42);
                _batch2d.FillTriangle(ct1, ct2, ct3, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(ct1, ct2, ct3, color, 2.5f);
                break;

            case Mode.CircleCapsule:
                _batch2d.FillCircle(_posA, 30f, ToAlpha(color, 0.5f));
                _batch2d.BorderCircle(_posA, 30f, color, 2.5f);
                _batch2d.FillCapsule(_posB + new Vector2(-50, 0), _posB + new Vector2(50, 0), 25f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posB + new Vector2(-50, 0), _posB + new Vector2(50, 0), 25f, color, 2.5f);
                break;

            case Mode.CapsuleCapsule:
                _batch2d.FillCapsule(_posA + new Vector2(-45, -25), _posA + new Vector2(45, 25), 28f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posA + new Vector2(-45, -25), _posA + new Vector2(45, 25), 28f, color, 2.5f);
                _batch2d.FillCapsule(_posB + new Vector2(-45, 25), _posB + new Vector2(45, -25), 28f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posB + new Vector2(-45, 25), _posB + new Vector2(45, -25), 28f, color, 2.5f);
                break;

            case Mode.CapsuleRectangle:
                _batch2d.FillCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, color, 2.5f);
                Rectangle recCap = RectAround(_posB, 140f, 90f);
                _batch2d.FillRectangle(recCap.X, recCap.Y, recCap.Width, recCap.Height, ToAlpha(color, 0.5f));
                _batch2d.BorderRectangle(recCap.X, recCap.Y, recCap.Width, recCap.Height, color, 2.5f);
                break;

            case Mode.CapsuleTriangle:
                _batch2d.FillCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, color, 2.5f);
                Vector2 capT1 = _posB + new Vector2(0, -60), capT2 = _posB + new Vector2(-51, 42), capT3 = _posB + new Vector2(51, 42);
                _batch2d.FillTriangle(capT1, capT2, capT3, ToAlpha(color, 0.5f));
                _batch2d.BorderTriangle(capT1, capT2, capT3, color, 2.5f);
                break;

            case Mode.CapsulePolygon:
                _batch2d.FillCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, ToAlpha(color, 0.5f));
                _batch2d.BorderCapsule(_posA + new Vector2(-45, 0), _posA + new Vector2(45, 0), 26f, color, 2.5f);
                DrawPoly(StarAround(_posB, 90f, 38f), color);
                break;
        }
    }

    private void DrawPoly(ReadOnlySpan<Vector2> points, Color color)
    {
        _batch2d.FillPolygon(points, ToAlpha(color, 0.5f));
        for (int i = 0; i < points.Length; i++)
            _batch2d.DrawLine(points[i], points[(i + 1) % points.Length], 2.5f, color);
    }

    private void DrawMarkers()
    {
        Vector2 mousePos = _mouseControlsA ? _posA : _posB;
        Vector2 keyboardPos = _mouseControlsA ? _posB : _posA;
        _batch2d.BorderCircle(mousePos, 8f, Palette.PeterRiver, 2f);
        _batch2d.BorderCircle(keyboardPos, 8f, Palette.Sunflower, 2f);
    }

    private void DrawHud(bool colliding)
    {
        string[] modeNames =
        {
            "1: CIRCLE vs CIRCLE", "2: CIRCLE vs RECTANGLE", "3: CIRCLE vs LINE", "4: POINT vs TRIANGLE", "5: POINT vs POLYGON", "6: RAY vs CIRCLE",
            "7: POLYGON vs POLYGON", "8: RECTANGLE vs POLYGON", "9: CIRCLE vs POLYGON", "0: TRIANGLE vs TRIANGLE", "Q: RECTANGLE vs TRIANGLE", "E: CIRCLE vs TRIANGLE",
            "R: CIRCLE vs CAPSULE", "T: CAPSULE vs CAPSULE", "Y: CAPSULE vs RECTANGLE", "U: CAPSULE vs TRIANGLE", "I: CAPSULE vs POLYGON (concave)"
        };
        _batch2d.DrawString(modeNames[(int)_mode], new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("1-9,0,Q,E,R,T,Y,U,I: switch check   Space: swap mouse/keyboard control   WASD: move the keyboard shape", new Vector2(16, 44), 1.5f, Palette.Silver);
        _batch2d.DrawString($"blue ring = mouse-controlled   yellow ring = keyboard-controlled", new Vector2(16, 66), 1.5f, Palette.Silver);
        _batch2d.DrawString(colliding ? "COLLIDING" : "not colliding", new Vector2(16, 96), 2.5f, colliding ? Palette.Alizarin : Palette.Nephritis);
    }

    private static Rectangle RectAround(Vector2 center, float width, float height)
        => new((int)(center.X - width * 0.5f), (int)(center.Y - height * 0.5f), (int)width, (int)height);

    private static Vector2[] StarAround(Vector2 center, float outerRadius, float innerRadius)
    {
        const int points = 5;
        var result = new Vector2[points * 2];
        for (int i = 0; i < points * 2; i++)
        {
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            float angle = MathHelper.PiOver2 * -1f + i * MathF.PI / points;
            result[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return result;
    }

    private static Color ToAlpha(Color color, float alphaFraction) => new(color.R, color.G, color.B, (byte)(color.A * alphaFraction));

    private static Vector2[] PentagonAround(Vector2 center, float radius) => RegularPolygonAround(center, radius, 5);
    private static Vector2[] QuadAround(Vector2 center, float radius) => RegularPolygonAround(center, radius, 4);

    private static Vector2[] RegularPolygonAround(Vector2 center, float radius, int sides)
    {
        var result = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = -MathHelper.PiOver2 + i * MathHelper.TwoPi / sides;
            result[i] = center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
        }
        return result;
    }
}
