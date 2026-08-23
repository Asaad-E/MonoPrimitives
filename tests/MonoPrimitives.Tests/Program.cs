using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives.Primitives3D;
using MonoPrimitives.Tests;

using var game = new TestRunnerGame();
game.Run();

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Headless test runner: pure-math checks (Collision2D/3D, Noise, ColorUtil, Easing) need nothing
    /// but this exe, but the shape-generation and lighting-regression checks need a real
    /// GraphicsDevice (BasicEffect, DrawUserPrimitives), which MonoGame only creates inside a
    /// running Game -- so everything runs once in Draw(), then the process exits with 0 (all
    /// passed) or 1 (a failure).
    /// </summary>
    internal sealed class TestRunnerGame : Game
    {
        private readonly GraphicsDeviceManager _graphics;

        public TestRunnerGame()
        {
            _graphics = new GraphicsDeviceManager(this);
        }

        protected override void Draw(GameTime gameTime)
        {
            var results = new TestResults();

            Collision2DTests.Run(results);
            Collision3DTests.Run(results);
            NoiseTests.Run(results);
            ColorUtilTests.Run(results);
            EasingTests.Run(results);
            UnitCircleLutTests.Run(results);
            FontGlyphs5x7Tests.Run(results);
            RandomUtilTests.Run(results);
            TrigLutTests.Run(results);
            Camera3DTests.Run(results);
            Vector2ExtensionsTests.Run(results);
            FrameLimiterTests.Run(this, results);

            using var batch = new Primitive3DBatch(GraphicsDevice);
            var camera = new Camera3D(position: new Vector3(6, 6, 6), target: Vector3.Zero, up: Vector3.Up, fovy: 50f);
            ShapeTests3D.Run(batch, camera, GraphicsDevice, results);
            DebugFont3DTests.Run(batch, camera, GraphicsDevice, results);

            using (var spriteBatch = new SpriteBatch(GraphicsDevice))
                FastTextureTests.Run(GraphicsDevice, spriteBatch, results);

            bool allPassed = results.PrintSummary();
            Environment.ExitCode = allPassed ? 0 : 1;
            Exit();
        }
    }
}
