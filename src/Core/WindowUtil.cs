using System;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoPrimitives
{
    /// <summary>A monitor's index, name, desktop position/size, and refresh rate, as reported by the OS.</summary>
    /// <remarks><see cref="RefreshRate"/> is 0 when the OS doesn't report one.</remarks>
    public readonly struct MonitorInfo
    {
        /// <summary>This monitor's index, matching <see cref="WindowUtil.GetMonitorInfo"/>'s parameter and <see cref="WindowUtil.GetCurrentMonitorIndex"/>'s return value.</summary>
        public int Index { get; }

        /// <summary>The OS-reported display name.</summary>
        public string Name { get; }

        /// <summary>Position and size in the OS's virtual desktop coordinate space.</summary>
        public Rectangle Bounds { get; }

        /// <summary>Refresh rate in Hz, or 0 if the OS doesn't report one.</summary>
        public int RefreshRate { get; }

        internal MonitorInfo(int index, string name, Rectangle bounds, int refreshRate)
        {
            Index = index;
            Name = name;
            Bounds = bounds;
            RefreshRate = refreshRate;
        }
    }

    /// <summary>
    /// Window/monitor/clipboard operations <see cref="GameWindow"/> and <see cref="GraphicsDeviceManager"/> don't expose --
    /// minimize/maximize/restore, opacity, icon, multi-monitor enumeration, and clipboard text --
    /// plus a captured-cursor delta helper for mouse-look input.
    /// </summary>
    /// <remarks>
    /// The window/monitor/clipboard methods are Desktop GL only, resolved directly from the same
    /// SDL2 library MonoGame's DesktopGL backend already loads (mirroring <see cref="FastTexture"/>'s
    /// own below-MonoGame approach for the same reason: MonoGame's public API doesn't expose these,
    /// even though the platform underneath already can). Check <see cref="IsAvailable"/>/<see cref="Diagnostics"/>
    /// once at startup -- action methods (Minimize/Maximize/Restore/SetWindowOpacity/SetWindowIcon/SetClipboardText)
    /// silently no-op when unavailable; query methods (GetMonitorInfo, GetClipboardText, etc.) throw
    /// <see cref="InvalidOperationException"/>, since there's no honest default value to return instead.
    /// The cursor-capture methods (<see cref="DisableCursor"/>/<see cref="EnableCursor"/>/<see cref="GetCursorDelta"/>)
    /// use only public MonoGame API and work on every backend.
    /// </remarks>
    public static class WindowUtil
    {
        // ------------------------------------------------------------------
        // Native delegate shapes
        // ------------------------------------------------------------------
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void WindowAction(IntPtr window);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate uint GetWindowFlags(IntPtr window);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetWindowOpacityNative(IntPtr window, float opacity);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetWindowOpacityNative(IntPtr window, out float opacity);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void SetWindowIconNative(IntPtr window, IntPtr surface);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr CreateRgbSurfaceFrom(IntPtr pixels, int width, int height, int depth, int pitch, uint rMask, uint gMask, uint bMask, uint aMask);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeSurface(IntPtr surface);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetNumVideoDisplays();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetDisplayBounds(int displayIndex, out SdlRect rect);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetDesktopDisplayMode(int displayIndex, out SdlDisplayMode mode);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetDisplayName(int displayIndex);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetWindowDisplayIndex(IntPtr window);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetClipboardTextNative(IntPtr utf8Text);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr GetClipboardTextNative();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void FreeNative(IntPtr memory);

        [StructLayout(LayoutKind.Sequential)]
        private struct SdlRect { public int X, Y, W, H; }

        [StructLayout(LayoutKind.Sequential)]
        private struct SdlDisplayMode { public uint Format; public int W, H, RefreshRate; public IntPtr DriverData; }

        // ------------------------------------------------------------------
        // Resolved entry points -- null when the corresponding symbol wasn't found.
        // ------------------------------------------------------------------
        private static readonly WindowAction? _minimizeWindow;
        private static readonly WindowAction? _maximizeWindow;
        private static readonly WindowAction? _restoreWindow;
        private static readonly GetWindowFlags? _getWindowFlags;
        private static readonly SetWindowOpacityNative? _setWindowOpacity;
        private static readonly GetWindowOpacityNative? _getWindowOpacity;
        private static readonly SetWindowIconNative? _setWindowIcon;
        private static readonly CreateRgbSurfaceFrom? _createRgbSurfaceFrom;
        private static readonly FreeSurface? _freeSurface;
        private static readonly GetNumVideoDisplays? _getNumVideoDisplays;
        private static readonly GetDisplayBounds? _getDisplayBounds;
        private static readonly GetDesktopDisplayMode? _getDesktopDisplayMode;
        private static readonly GetDisplayName? _getDisplayName;
        private static readonly GetWindowDisplayIndex? _getWindowDisplayIndex;
        private static readonly SetClipboardTextNative? _setClipboardText;
        private static readonly GetClipboardTextNative? _getClipboardText;
        private static readonly FreeNative? _free;

        // SDL_WINDOW_* flag bits (SDL_WindowFlags), stable across the whole SDL2 ABI.
        private const uint SDL_WINDOW_MINIMIZED = 0x00000040;
        private const uint SDL_WINDOW_MAXIMIZED = 0x00000080;

        /// <summary>True if every SDL2 entry point this class needs was found. False means every action method below silently no-ops and every query method throws.</summary>
        public static bool IsAvailable { get; }

        /// <summary>Human-readable explanation of whether SDL2 was found and, if not, why. Log this once at startup.</summary>
        public static string Diagnostics { get; }

        static WindowUtil()
        {
            foreach (string name in FastTexture.GetSdlCandidates())
            {
                if (!NativeLibrary.TryLoad(name, out IntPtr sdl))
                    continue;

                bool TryGet<T>(string symbol, out T? del) where T : Delegate
                {
                    if (NativeLibrary.TryGetExport(sdl, symbol, out IntPtr addr))
                    {
                        del = Marshal.GetDelegateForFunctionPointer<T>(addr);
                        return true;
                    }
                    del = null;
                    return false;
                }

                TryGet("SDL_MinimizeWindow", out _minimizeWindow);
                TryGet("SDL_MaximizeWindow", out _maximizeWindow);
                TryGet("SDL_RestoreWindow", out _restoreWindow);
                TryGet("SDL_GetWindowFlags", out _getWindowFlags);
                TryGet("SDL_SetWindowOpacity", out _setWindowOpacity);
                TryGet("SDL_GetWindowOpacity", out _getWindowOpacity);
                TryGet("SDL_SetWindowIcon", out _setWindowIcon);
                TryGet("SDL_CreateRGBSurfaceFrom", out _createRgbSurfaceFrom);
                TryGet("SDL_FreeSurface", out _freeSurface);
                TryGet("SDL_GetNumVideoDisplays", out _getNumVideoDisplays);
                TryGet("SDL_GetDisplayBounds", out _getDisplayBounds);
                TryGet("SDL_GetDesktopDisplayMode", out _getDesktopDisplayMode);
                TryGet("SDL_GetDisplayName", out _getDisplayName);
                TryGet("SDL_GetWindowDisplayIndex", out _getWindowDisplayIndex);
                TryGet("SDL_SetClipboardText", out _setClipboardText);
                TryGet("SDL_GetClipboardText", out _getClipboardText);
                TryGet("SDL_free", out _free);

                // The window/opacity/icon/monitor/clipboard surface below only needs the core
                // window+display+clipboard entry points -- treat those as the availability bar.
                IsAvailable = _minimizeWindow != null && _maximizeWindow != null && _restoreWindow != null &&
                              _getWindowFlags != null && _getNumVideoDisplays != null && _getDisplayBounds != null &&
                              _getDesktopDisplayMode != null && _getDisplayName != null && _getWindowDisplayIndex != null;

                Diagnostics = IsAvailable
                    ? "WindowUtil: resolved from " + name
                    : "WindowUtil: loaded " + name + " but one or more required SDL2 exports were missing (unexpectedly old SDL2 build?)";
                return;
            }

            Diagnostics = "WindowUtil: SDL2 not loadable -- not running on the DesktopGL backend?";
        }

        // ==================================================================
        // Window state
        // ==================================================================

        /// <summary>Minimizes the window. No-op if <see cref="IsAvailable"/> is false.</summary>
        public static void MinimizeWindow(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            _minimizeWindow?.Invoke(handle);
        }

        /// <summary>Maximizes the window. No-op if <see cref="IsAvailable"/> is false.</summary>
        /// <remarks>On Windows, this silently does nothing unless <see cref="GameWindow.AllowUserResizing"/> is true -- verified directly: the OS ignores a maximize request for a window that was never given a maximize/thick-frame style in the first place, the same reason its title bar has no maximize button. <see cref="MinimizeWindow"/>/<see cref="RestoreWindow"/> have no such restriction.</remarks>
        public static void MaximizeWindow(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            _maximizeWindow?.Invoke(handle);
        }

        /// <summary>Restores a minimized or maximized window to its previous size and position. No-op if <see cref="IsAvailable"/> is false.</summary>
        public static void RestoreWindow(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            _restoreWindow?.Invoke(handle);
        }

        /// <summary>True if the window is currently minimized. Always false if <see cref="IsAvailable"/> is false.</summary>
        public static bool IsWindowMinimized(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            return _getWindowFlags != null && (_getWindowFlags(handle) & SDL_WINDOW_MINIMIZED) != 0;
        }

        /// <summary>True if the window is currently maximized. Always false if <see cref="IsAvailable"/> is false.</summary>
        public static bool IsWindowMaximized(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            return _getWindowFlags != null && (_getWindowFlags(handle) & SDL_WINDOW_MAXIMIZED) != 0;
        }

        /// <summary>Sets the window's opacity, from 0 (fully transparent) to 1 (fully opaque). Clamped to that range. No-op if <see cref="IsAvailable"/> is false.</summary>
        public static void SetWindowOpacity(GameWindow window, float opacity)
        {
            IntPtr handle = RequireHandle(window);
            _setWindowOpacity?.Invoke(handle, MathHelper.Clamp(opacity, 0f, 1f));
        }

        /// <summary>Reads the window's current opacity. Returns 1 (fully opaque) if unsupported.</summary>
        public static float GetWindowOpacity(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            if (_getWindowOpacity != null && _getWindowOpacity(handle, out float opacity) == 0)
                return opacity;
            return 1f;
        }

        /// <summary>
        /// Sets the window's title-bar/taskbar icon from a texture's current pixels.
        /// </summary>
        /// <remarks><paramref name="icon"/> must be <see cref="SurfaceFormat.Color"/> (the format images loaded via <c>Texture2D.FromStream</c> or the content pipeline already use). No-op if <see cref="IsAvailable"/> is false.</remarks>
        public static void SetWindowIcon(GameWindow window, Texture2D icon)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (icon == null) throw new ArgumentNullException(nameof(icon));
            if (_setWindowIcon == null || _createRgbSurfaceFrom == null || _freeSurface == null) return;

            if (icon.Format != SurfaceFormat.Color)
                throw new ArgumentException("icon must be SurfaceFormat.Color, got " + icon.Format + ".", nameof(icon));

            Color[] pixels = new Color[icon.Width * icon.Height];
            icon.GetData(pixels);

            GCHandle handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                // Color's packed layout is R in the lowest byte through A in the highest (verified
                // against the real type, not assumed) -- these masks match that exactly.
                IntPtr surface = _createRgbSurfaceFrom(
                    handle.AddrOfPinnedObject(), icon.Width, icon.Height, 32, icon.Width * 4,
                    0x000000FFu, 0x0000FF00u, 0x00FF0000u, 0xFF000000u);

                if (surface == IntPtr.Zero) return;

                // SDL_SetWindowIcon copies the surface's pixel data into its own icon representation,
                // so freeing right after is safe -- it doesn't keep a reference to this surface.
                _setWindowIcon(window.Handle, surface);
                _freeSurface(surface);
            }
            finally
            {
                handle.Free();
            }
        }

        // ==================================================================
        // Monitors
        // ==================================================================

        /// <summary>Number of connected monitors.</summary>
        /// <exception cref="InvalidOperationException"><see cref="IsAvailable"/> is false.</exception>
        public static int GetMonitorCount()
        {
            ThrowIfUnavailable();
            return _getNumVideoDisplays!();
        }

        /// <summary>The monitor containing most of the window, matching raylib's <c>GetCurrentMonitor</c>.</summary>
        /// <exception cref="InvalidOperationException"><see cref="IsAvailable"/> is false.</exception>
        public static int GetCurrentMonitorIndex(GameWindow window)
        {
            IntPtr handle = RequireHandle(window);
            ThrowIfUnavailable();
            int index = _getWindowDisplayIndex!(handle);
            return index < 0 ? 0 : index;
        }

        /// <summary>The given monitor's name, desktop position/size, and refresh rate.</summary>
        /// <exception cref="InvalidOperationException"><see cref="IsAvailable"/> is false.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not a valid monitor index.</exception>
        public static MonitorInfo GetMonitorInfo(int index)
        {
            ThrowIfUnavailable();
            if (index < 0 || index >= _getNumVideoDisplays!())
                throw new ArgumentOutOfRangeException(nameof(index));

            _getDisplayBounds!(index, out SdlRect bounds);
            _getDesktopDisplayMode!(index, out SdlDisplayMode mode);
            IntPtr namePtr = _getDisplayName!(index);
            string name = namePtr == IntPtr.Zero ? string.Empty : (Marshal.PtrToStringUTF8(namePtr) ?? string.Empty);

            return new MonitorInfo(index, name, new Rectangle(bounds.X, bounds.Y, bounds.W, bounds.H), mode.RefreshRate);
        }

        // ==================================================================
        // Clipboard
        // ==================================================================

        /// <summary>Sets the system clipboard's text. No-op if <see cref="IsAvailable"/> is false.</summary>
        public static void SetClipboardText(string text)
        {
            if (_setClipboardText == null) return;
            IntPtr ptr = Marshal.StringToCoTaskMemUTF8(text ?? string.Empty);
            try { _setClipboardText(ptr); }
            finally { Marshal.FreeCoTaskMem(ptr); }
        }

        /// <summary>Reads the system clipboard's text. Returns an empty string if there's no text on the clipboard.</summary>
        /// <exception cref="InvalidOperationException"><see cref="IsAvailable"/> is false.</exception>
        public static string GetClipboardText()
        {
            ThrowIfUnavailable();
            if (_getClipboardText == null) return string.Empty;

            IntPtr ptr = _getClipboardText();
            if (ptr == IntPtr.Zero) return string.Empty;

            string text = Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
            _free?.Invoke(ptr); // SDL_GetClipboardText hands ownership to the caller -- must free with SDL_free.
            return text;
        }

        // ==================================================================
        // Cursor capture (public MonoGame API only -- works on every backend)
        // ==================================================================

        /// <summary>
        /// Hides the cursor and centers it, the standard first step of FPS-style mouse-look input.
        /// Call <see cref="GetCursorDelta"/> once per frame afterward to read look input.
        /// </summary>
        /// <remarks>MonoGame has no real relative/captured mouse mode to switch into (confirmed missing from the framework) -- this and <see cref="GetCursorDelta"/> are the manual hide-and-recenter-every-frame technique, packaged into two calls.</remarks>
        public static void DisableCursor(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.IsMouseVisible = false;
            Rectangle bounds = game.Window.ClientBounds;
            Mouse.SetPosition(bounds.Width / 2, bounds.Height / 2);
        }

        /// <summary>Shows the cursor again and stops the captured-mouse-look convention <see cref="DisableCursor"/> started.</summary>
        public static void EnableCursor(Game game)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            game.IsMouseVisible = true;
        }

        /// <summary>
        /// Reads how far the cursor moved since the last call, then recenters it -- call this once per
        /// frame between <see cref="DisableCursor"/> and <see cref="EnableCursor"/> to drive a look camera.
        /// </summary>
        public static Point GetCursorDelta(GameWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));

            Rectangle bounds = window.ClientBounds;
            int centerX = bounds.Width / 2;
            int centerY = bounds.Height / 2;

            MouseState state = Mouse.GetState();
            Point delta = new Point(state.X - centerX, state.Y - centerY);
            Mouse.SetPosition(centerX, centerY);
            return delta;
        }

        // ==================================================================
        // Helpers
        // ==================================================================

        private static IntPtr RequireHandle(GameWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            return window.Handle;
        }

        private static void ThrowIfUnavailable()
        {
            if (!IsAvailable)
                throw new InvalidOperationException("WindowUtil is not available on this platform/backend. " + Diagnostics);
        }
    }
}
