#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BoidsSwarm;

/// <summary>MonoGame <see cref="VertexDeclaration"/> matching Dear ImGui's own <see cref="ImDrawVert"/> layout (position, UV, packed RGBA color) -- stable across ImGui versions, never changed upstream.</summary>
internal static class ImGuiVertexDeclaration
{
    public static readonly int Size = Marshal.SizeOf<ImDrawVert>();

    public static readonly VertexDeclaration Declaration = new(
        Size,
        new VertexElement(0, VertexElementFormat.Vector2, VertexElementUsage.Position, 0),
        new VertexElement(8, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
        new VertexElement(16, VertexElementFormat.Color, VertexElementUsage.Color, 0));
}

/// <summary>
/// Minimal MonoGame backend for Dear ImGui (via ImGui.NET): builds/uploads the font atlas
/// texture, feeds raw mouse/keyboard state into ImGui's IO every frame, and translates ImGui's
/// draw lists into MonoGame draw calls via a plain <see cref="BasicEffect"/>.
/// </summary>
/// <remarks>Vendored rather than pulled from a third-party MonoGame-ImGui wrapper package -- ImGui.NET itself doesn't ship a MonoGame renderer, and the few community wrapper packages on NuGet are small and thinly maintained. This is the same one-file pattern most MonoGame+ImGui projects end up writing themselves.</remarks>
internal sealed class ImGuiRenderer
{
    private readonly Game _game;
    private readonly GraphicsDevice _device;

    private BasicEffect? _effect;
    private readonly RasterizerState _rasterizerState;

    private VertexBuffer? _vertexBuffer;
    private int _vertexBufferSize;
    private IndexBuffer? _indexBuffer;
    private int _indexBufferSize;

    private readonly Dictionary<IntPtr, Texture2D> _loadedTextures = new();
    private int _nextTextureId;
    private IntPtr _fontTextureId;

    private int _scrollWheelValue;
    private readonly List<Keys> _heldKeys = new();

    public unsafe ImGuiRenderer(Game game)
    {
        _game = game;
        _device = game.GraphicsDevice;

        ImGui.CreateContext();
        ImGui.GetIO().NativePtr->IniFilename = null; // don't litter the working directory with a persisted-layout imgui.ini -- this panel's layout is fixed, nothing to remember between runs

        _rasterizerState = new RasterizerState
        {
            CullMode = CullMode.None,
            DepthBias = 0,
            FillMode = FillMode.Solid,
            MultiSampleAntiAlias = false,
            ScissorTestEnable = true,
            SlopeScaleDepthBias = 0,
        };

        RebuildFontAtlas();
    }

    // ---- Font atlas ----------------------------------------------------------

    private unsafe void RebuildFontAtlas()
    {
        ImGuiIOPtr io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);

        var pixels = new byte[width * height * bytesPerPixel];
        new Span<byte>(pixelData, pixels.Length).CopyTo(pixels);

        var texture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
        texture.SetData(pixels);

        _fontTextureId = BindTexture(texture);
        io.Fonts.SetTexID(_fontTextureId);
        io.Fonts.ClearTexData();
    }

    private IntPtr BindTexture(Texture2D texture)
    {
        IntPtr id = new(_nextTextureId++);
        _loadedTextures[id] = texture;
        return id;
    }

    // ---- Per-frame entry points -----------------------------------------------

    public void BeforeLayout(GameTime gameTime)
    {
        ImGui.GetIO().DeltaTime = MathF.Max((float)gameTime.ElapsedGameTime.TotalSeconds, 1f / 1000f);
        ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(_device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);
        UpdateInput();
        ImGui.NewFrame();
    }

    public void AfterLayout()
    {
        ImGui.Render();
        RenderDrawData(ImGui.GetDrawData());
    }

    // ---- Input ------------------------------------------------------------------

    private void UpdateInput()
    {
        if (!_game.IsActive) return;

        ImGuiIOPtr io = ImGui.GetIO();
        MouseState mouse = Mouse.GetState();
        KeyboardState keyboard = Keyboard.GetState();

        // Event-based API, not the old io.MousePos/io.MouseDown[] field writes -- those still
        // compile against this ImGui.NET version but don't reliably drive click/drag-edge
        // detection for widgets (sliders, buttons, checkboxes) the way the modern Add*Event calls
        // do; same reason keyboard input below uses AddKeyEvent instead of the removed io.KeysDown.
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);

        int scrollDelta = mouse.ScrollWheelValue - _scrollWheelValue;
        if (scrollDelta != 0) io.AddMouseWheelEvent(0f, scrollDelta > 0 ? 1f : -1f);
        _scrollWheelValue = mouse.ScrollWheelValue;

        // Release every key this class itself marked down last frame, then mark this frame's
        // pressed set -- avoids needing io.KeysDown (the pre-1.87 array-indexed API, gone here).
        foreach (Keys key in _heldKeys)
            if (TryMapKey(key, out ImGuiKey mapped)) io.AddKeyEvent(mapped, false);
        _heldKeys.Clear();

        foreach (Keys key in keyboard.GetPressedKeys())
        {
            if (!TryMapKey(key, out ImGuiKey mapped)) continue;
            io.AddKeyEvent(mapped, true);
            _heldKeys.Add(key);
        }

        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt));
    }

    private static bool TryMapKey(Keys key, out ImGuiKey result)
    {
        static ImGuiKey Shift(Keys key, Keys rangeStart, ImGuiKey targetStart) => targetStart + (key - rangeStart);

        result = key switch
        {
            >= Keys.A and <= Keys.Z => Shift(key, Keys.A, ImGuiKey.A),
            >= Keys.D0 and <= Keys.D9 => Shift(key, Keys.D0, ImGuiKey._0),
            >= Keys.F1 and <= Keys.F24 => Shift(key, Keys.F1, ImGuiKey.F1),
            >= Keys.NumPad0 and <= Keys.NumPad9 => Shift(key, Keys.NumPad0, ImGuiKey.Keypad0),
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Space => ImGuiKey.Space,
            Keys.Tab => ImGuiKey.Tab,
            Keys.Back => ImGuiKey.Backspace,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.PageUp => ImGuiKey.PageUp,
            Keys.PageDown => ImGuiKey.PageDown,
            Keys.Insert => ImGuiKey.Insert,
            Keys.OemMinus => ImGuiKey.Minus,
            Keys.OemPlus => ImGuiKey.Equal,
            Keys.OemComma => ImGuiKey.Comma,
            Keys.OemPeriod => ImGuiKey.Period,
            Keys.LeftControl or Keys.RightControl => ImGuiKey.ModCtrl,
            Keys.LeftShift or Keys.RightShift => ImGuiKey.ModShift,
            Keys.LeftAlt or Keys.RightAlt => ImGuiKey.ModAlt,
            _ => ImGuiKey.None,
        };
        return result != ImGuiKey.None;
    }

    // ---- Draw-data translation -----------------------------------------------

    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        if (drawData.CmdListsCount == 0) return;

        Viewport lastViewport = _device.Viewport;
        Rectangle lastScissorBox = _device.ScissorRectangle;
        BlendState lastBlendState = _device.BlendState;
        DepthStencilState lastDepthStencilState = _device.DepthStencilState;
        RasterizerState lastRasterizerState = _device.RasterizerState;

        _device.BlendState = BlendState.NonPremultiplied;
        _device.RasterizerState = _rasterizerState;
        _device.DepthStencilState = DepthStencilState.None;
        _device.Viewport = new Viewport(0, 0, _device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);

        UpdateBuffers(drawData);
        RenderCommandLists(drawData);

        _device.Viewport = lastViewport;
        _device.ScissorRectangle = lastScissorBox;
        _device.BlendState = lastBlendState;
        _device.DepthStencilState = lastDepthStencilState;
        _device.RasterizerState = lastRasterizerState;
    }

    private unsafe void UpdateBuffers(ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount == 0) return;

        if (drawData.TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5f);
            _vertexBuffer = new VertexBuffer(_device, ImGuiVertexDeclaration.Declaration, _vertexBufferSize, BufferUsage.None);
        }

        if (drawData.TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();
            _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5f);
            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.None);
        }

        int vtxOffset = 0, idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];

            var vtxSpan = new ReadOnlySpan<ImDrawVert>((void*)cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size);
            _vertexBuffer!.SetData(vtxOffset * ImGuiVertexDeclaration.Size, vtxSpan.ToArray(), 0, cmdList.VtxBuffer.Size, ImGuiVertexDeclaration.Size);

            var idxSpan = new ReadOnlySpan<ushort>((void*)cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size);
            _indexBuffer!.SetData(idxOffset * sizeof(ushort), idxSpan.ToArray(), 0, cmdList.IdxBuffer.Size);

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    private void RenderCommandLists(ImDrawDataPtr drawData)
    {
        _device.SetVertexBuffer(_vertexBuffer);
        _device.Indices = _indexBuffer;

        int vtxOffset = 0, idxOffset = 0;
        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdLists[n];
            for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmdi];
                if (!_loadedTextures.TryGetValue(drawCmd.TextureId, out Texture2D? texture))
                    throw new InvalidOperationException($"No texture bound for id '{drawCmd.TextureId}'.");

                _device.ScissorRectangle = new Rectangle(
                    (int)drawCmd.ClipRect.X,
                    (int)drawCmd.ClipRect.Y,
                    (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                    (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y));

                Effect effect = UpdateEffect(texture);
                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    _device.DrawIndexedPrimitives(
                        primitiveType: PrimitiveType.TriangleList,
                        baseVertex: vtxOffset + (int)drawCmd.VtxOffset,
                        startIndex: idxOffset + (int)drawCmd.IdxOffset,
                        primitiveCount: (int)drawCmd.ElemCount / 3);
                }
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    private Effect UpdateEffect(Texture2D texture)
    {
        _effect ??= new BasicEffect(_device);

        ImGuiIOPtr io = ImGui.GetIO();
        _effect.World = Matrix.Identity;
        _effect.View = Matrix.Identity;
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, io.DisplaySize.X, io.DisplaySize.Y, 0f, -1f, 1f);
        _effect.TextureEnabled = true;
        _effect.Texture = texture;
        _effect.VertexColorEnabled = true;

        return _effect;
    }
}
