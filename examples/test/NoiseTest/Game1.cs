#nullable enable

using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using MonoPrimitives;
using MonoPrimitives.Primitives2D;
using MonoPrimitives.Primitives3D;

namespace NoiseTest;

/// <summary>
/// Visual test for <see cref="Noise"/>'s 1D/2D/3D samples at a resolution fine enough to see the
/// noise clearly. Keys 1-4 switch scene: a 1D Fbm curve as a simple terrain silhouette, a 2D Fbm
/// heightmap terrain in 3D, a live-animated 2D grid sampling 3D noise with Z as time, and a
/// side-by-side RidgeNoise2D vs Turbulence2D comparison.
/// </summary>
public class Game1 : Game
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    private GraphicsDeviceManager _graphics;
    private PrimitiveBatch _batch2d = null!;
    private Primitive3DBatch _batch3d = null!;
    private PrimitiveInput _input = null!;

    private enum Scene { Curve1D, Terrain2D, AnimatedField3D, RidgeVsTurbulence }
    private Scene _scene = Scene.Curve1D;

    private readonly Noise _noise1d = new(unchecked((int)0x4E314400)); // "N1D" -- default 4 octaves
    private readonly Noise _noise2d = new(unchecked((int)0x4E324400), octaves: 5); // "N2D"
    private readonly Noise _noise3d = new(unchecked((int)0x4E334400)); // "N3D"
    private readonly Noise _noise4d = new(unchecked((int)0x4E344400), octaves: 5); // "N4D" -- Ridge/Turbulence

    private Camera3D _camera3d = new(new Vector3(0, 150, 230), Vector3.Zero, Vector3.Up, 45f) { Mode = CameraMode.Orbital, OrbitalSpeed = 0.25f };

    private const int TerrainGridSize = 64;
    private const float TerrainCellSize = 3.5f;
    private float[,] _terrainHeights = new float[TerrainGridSize, TerrainGridSize];
    private Color[,] _terrainColors = new Color[TerrainGridSize, TerrainGridSize];

    private const int FieldCellPixels = 8;
    private float _fieldTime;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this) { PreferredBackBufferWidth = WindowWidth, PreferredBackBufferHeight = WindowHeight };
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _batch2d = new PrimitiveBatch(GraphicsDevice);
        _batch3d = new Primitive3DBatch(GraphicsDevice) { LightingEnabled = true, AmbientLight = 0.6f, LightDirection = new Vector3(-0.4f, -1f, -0.3f) };
        _input = new PrimitiveInput();
        BuildTerrain();
        base.Initialize();
    }

    // Fbm2D height per cell, colored low-to-high (valley -> peak) via the normalized height —
    // built once since the terrain itself never changes, only the camera orbits around it.
    private void BuildTerrain()
    {
        const float frequency = 0.06f;
        const float amplitude = 20f;
        for (int z = 0; z < TerrainGridSize; z++)
        {
            for (int x = 0; x < TerrainGridSize; x++)
            {
                float h = _noise2d.Fbm2D(x * frequency, z * frequency) * amplitude;
                _terrainHeights[x, z] = h;
                float t = Math.Clamp((h / amplitude + 1f) * 0.5f, 0f, 1f);
                _terrainColors[x, z] = ColorUtil.Lerp(Palette.Emerald, Palette.Clouds, t);
            }
        }
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update(deltaTime);

        if (_input.IsKeyPressed(Keys.D1)) _scene = Scene.Curve1D;
        if (_input.IsKeyPressed(Keys.D2)) _scene = Scene.Terrain2D;
        if (_input.IsKeyPressed(Keys.D3)) _scene = Scene.AnimatedField3D;
        if (_input.IsKeyPressed(Keys.D4)) _scene = Scene.RidgeVsTurbulence;

        if (_scene == Scene.Terrain2D)
            _camera3d.UpdateWithInput(_input, deltaTime); // orbital auto-rotation only; no WASD/mouse needed
        else
            _camera3d.Update(deltaTime);

        if (_scene == Scene.AnimatedField3D)
            _fieldTime += deltaTime * 0.35f;

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Palette.Background);

        switch (_scene)
        {
            case Scene.Curve1D: DrawCurve1D(); break;
            case Scene.Terrain2D: DrawTerrain2D(); break;
            case Scene.AnimatedField3D: DrawAnimatedField3D(); break;
            case Scene.RidgeVsTurbulence: DrawRidgeVsTurbulence(); break;
        }

        DrawHud();
        base.Draw(gameTime);
    }

    // ------------------------------------------------------------------
    // Scene 1: Sample1D/Fbm1D as a simple terrain-like curve silhouette.
    // ------------------------------------------------------------------
    private void DrawCurve1D()
    {
        const float frequency = 0.006f;
        const float amplitude = 150f;
        const float baseline = 480f;
        const int sampleStep = 2; // one sample every 2px -- fine enough to see the curve clearly

        int sampleCount = WindowWidth / sampleStep + 1;
        Span<Vector2> ridge = stackalloc Vector2[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float x = i * sampleStep;
            float y = baseline - _noise1d.Fbm1D(x * frequency) * amplitude;
            ridge[i] = new Vector2(x, y);
        }

        Span<Vector2> ground = stackalloc Vector2[sampleCount + 2];
        ridge.CopyTo(ground);
        ground[sampleCount] = new Vector2(WindowWidth, WindowHeight);
        ground[sampleCount + 1] = new Vector2(0, WindowHeight);

        _batch2d.Begin();
        // Full window height, not just to baseline: the terrain can dip below baseline in
        // valleys, and a sky rect stopping exactly at baseline leaves a gap showing the clear
        // color through underneath those dips.
        _batch2d.FillRectangleGradient(0, 0, WindowWidth, WindowHeight, Palette.PeterRiver, Palette.Clouds, horizontal: false);
        _batch2d.FillPolygon(ground, Palette.Emerald);
        _batch2d.DrawLineStrip(ridge, 3f, Palette.Nephritis);
        _batch2d.End();
    }

    // ------------------------------------------------------------------
    // Scene 2: Fbm2D heightmap rendered as an actual 3D terrain, slowly orbited.
    // ------------------------------------------------------------------
    private void DrawTerrain2D()
    {
        Vector3 origin = new(-(TerrainGridSize - 1) * TerrainCellSize * 0.5f, 0f, -(TerrainGridSize - 1) * TerrainCellSize * 0.5f);

        _batch3d.Begin(_camera3d);
        _batch3d.DrawGridXZ(20, 20f);
        _batch3d.FillHeightmap(_terrainHeights, _terrainColors, origin, new Vector2(TerrainCellSize, TerrainCellSize));
        _batch3d.End();
    }

    // ------------------------------------------------------------------
    // Scene 3: Sample3D with Z as time -- a live-animated 2D noise field.
    // ------------------------------------------------------------------
    private void DrawAnimatedField3D()
    {
        const float frequency = 0.12f;
        int cols = WindowWidth / FieldCellPixels;
        int rows = WindowHeight / FieldCellPixels;

        _batch2d.Begin();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float n = _noise3d.Sample3D(x * frequency, y * frequency, _fieldTime);
                float t = Math.Clamp((n + 1f) * 0.5f, 0f, 1f);
                Color color = ColorUtil.Lerp(Palette.WetAsphalt, Palette.Turquoise, t);
                _batch2d.FillRectangle(x * FieldCellPixels, y * FieldCellPixels, FieldCellPixels, FieldCellPixels, color);
            }
        }
        _batch2d.End();
    }

    // ------------------------------------------------------------------
    // Scene 4: RidgeNoise2D (left) vs Turbulence2D (right), same coordinates -- Ridge should
    // read as sharp, thin ridgelines; Turbulence as a softer, billowy, creased look.
    // ------------------------------------------------------------------
    private void DrawRidgeVsTurbulence()
    {
        const float frequency = 0.04f;
        int cols = WindowWidth / 2 / FieldCellPixels;
        int rows = WindowHeight / FieldCellPixels;

        _batch2d.Begin();
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < cols; x++)
            {
                float ridge = _noise4d.RidgeNoise2D(x * frequency, y * frequency);
                float turbulence = _noise4d.Turbulence2D(x * frequency, y * frequency);
                Color ridgeColor = ColorUtil.Lerp(Palette.MidnightBlue, Palette.Sunflower, Math.Clamp(ridge, 0f, 1f));
                Color turbColor = ColorUtil.Lerp(Palette.MidnightBlue, Palette.Sunflower, Math.Clamp(turbulence, 0f, 1f));
                _batch2d.FillRectangle(x * FieldCellPixels, y * FieldCellPixels, FieldCellPixels, FieldCellPixels, ridgeColor);
                _batch2d.FillRectangle(WindowWidth / 2 + x * FieldCellPixels, y * FieldCellPixels, FieldCellPixels, FieldCellPixels, turbColor);
            }
        }
        _batch2d.DrawLine(new Vector2(WindowWidth / 2f, 0), new Vector2(WindowWidth / 2f, WindowHeight), 2f, Color.White);
        _batch2d.DrawString("RidgeNoise2D", new Vector2(16, WindowHeight - 34), 1.8f, Color.White);
        _batch2d.DrawString("Turbulence2D", new Vector2(WindowWidth / 2f + 16, WindowHeight - 34), 1.8f, Color.White);
        _batch2d.End();
    }

    private static readonly (string Name, string Desc)[] SceneInfo =
    {
        ("1: 1D CURVE", "Fbm1D as a terrain silhouette"),
        ("2: 2D TERRAIN", "Fbm2D heightmap, orbiting camera"),
        ("3: 3D FIELD", "Sample3D, Z = time"),
        ("4: RIDGE VS TURBULENCE", "RidgeNoise2D (left) vs Turbulence2D (right)"),
    };

    private void DrawHud()
    {
        _batch2d.Begin();
        int i = (int)_scene;
        _batch2d.DrawString($"NOISE {SceneInfo[i].Name} ({SceneInfo[i].Desc})", new Vector2(16, 16), 2f, Color.White);
        _batch2d.DrawString("1-3: switch scene", new Vector2(16, 44), 1.5f, Palette.Silver);
        _batch2d.End();
    }
}
