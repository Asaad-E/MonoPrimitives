using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoPrimitives;

namespace MonoPrimitives.Tests
{
    /// <summary>
    /// Checks that don't depend on a real window manager applying a request -- Minimize/Maximize/Restore
    /// are exercised only for "doesn't throw" here, since a bare Xvfb CI display has no window manager
    /// to actually honor them (see Design/DECISIONS.md).
    /// </summary>
    internal static class WindowUtilTests
    {
        public static void Run(Game game, TestResults results)
        {
            GameWindow window = game.Window;

            results.Check("WindowUtil.IsAvailable/Diagnostics don't throw", () =>
            {
                _ = WindowUtil.IsAvailable;
                _ = WindowUtil.Diagnostics;
                return null;
            });

            results.Check("WindowUtil window actions (Minimize/Maximize/Restore/SetWindowOpacity/SetWindowIcon) never throw, available or not", () =>
            {
                WindowUtil.MinimizeWindow(window);
                WindowUtil.RestoreWindow(window);
                WindowUtil.MaximizeWindow(window);
                WindowUtil.RestoreWindow(window);
                WindowUtil.SetWindowOpacity(window, 1f);

                using var icon = new Texture2D(game.GraphicsDevice, 4, 4, false, SurfaceFormat.Color);
                WindowUtil.SetWindowIcon(window, icon);
                return null;
            });

            results.Check("WindowUtil.SetWindowIcon rejects a non-Color surface format", () =>
            {
                using var icon = new Texture2D(game.GraphicsDevice, 4, 4, false, SurfaceFormat.Bgra5551);
                try
                {
                    WindowUtil.SetWindowIcon(window, icon);
                    return WindowUtil.IsAvailable ? "expected ArgumentException for a non-Color texture" : null;
                }
                catch (ArgumentException) { return null; }
            });

            results.Check("WindowUtil.SetWindowOpacity/GetWindowOpacity round-trip when available", () =>
            {
                if (!WindowUtil.IsAvailable) return null; // nothing to round-trip without SDL2 resolved

                WindowUtil.SetWindowOpacity(window, 0.5f);
                float readBack = WindowUtil.GetWindowOpacity(window);
                WindowUtil.SetWindowOpacity(window, 1f); // restore, so later tests/manual runs aren't left translucent

                return Math.Abs(readBack - 0.5f) < 0.05f ? null : $"expected ~0.5 back, got {readBack}";
            });

            results.Check("WindowUtil.SetClipboardText/GetClipboardText round-trip when available", () =>
            {
                if (!WindowUtil.IsAvailable) return null;

                string text = "MonoPrimitives-test-" + Guid.NewGuid().ToString("N");
                WindowUtil.SetClipboardText(text);
                string readBack = WindowUtil.GetClipboardText();
                return readBack == text ? null : $"expected '{text}' back, got '{readBack}'";
            });

            results.Check("WindowUtil.GetMonitorCount/GetMonitorInfo/GetCurrentMonitorIndex are internally consistent when available", () =>
            {
                if (!WindowUtil.IsAvailable) return null;

                int count = WindowUtil.GetMonitorCount();
                if (count < 1) return $"expected at least 1 monitor, got {count}";

                for (int i = 0; i < count; i++)
                {
                    MonitorInfo info = WindowUtil.GetMonitorInfo(i);
                    if (info.Index != i) return $"GetMonitorInfo({i}).Index was {info.Index}";
                    if (info.Bounds.Width <= 0 || info.Bounds.Height <= 0) return $"monitor {i} reported non-positive size {info.Bounds}";
                }

                int current = WindowUtil.GetCurrentMonitorIndex(window);
                if (current < 0 || current >= count) return $"GetCurrentMonitorIndex returned {current}, outside [0,{count})";

                return null;
            });

            results.Check("WindowUtil.GetMonitorInfo throws for an out-of-range index when available", () =>
            {
                if (!WindowUtil.IsAvailable) return null;

                try
                {
                    WindowUtil.GetMonitorInfo(-1);
                    return "expected ArgumentOutOfRangeException for index -1";
                }
                catch (ArgumentOutOfRangeException) { return null; }
            });

            results.Check("WindowUtil.GetMonitorInfo/GetClipboardText throw InvalidOperationException when unavailable", () =>
            {
                if (WindowUtil.IsAvailable) return null; // this platform has SDL2 resolved -- nothing to check here

                try { WindowUtil.GetMonitorInfo(0); return "expected InvalidOperationException"; }
                catch (InvalidOperationException) { }

                try { WindowUtil.GetClipboardText(); return "expected InvalidOperationException"; }
                catch (InvalidOperationException) { }

                return null;
            });

            results.Check("WindowUtil.DisableCursor/GetCursorDelta/EnableCursor: delta reads ~zero immediately after centering", () =>
            {
                WindowUtil.DisableCursor(game);
                Point delta = WindowUtil.GetCursorDelta(window);
                WindowUtil.EnableCursor(game);
                return delta == Point.Zero ? null : $"expected a zero delta right after centering, got {delta}";
            });

            results.Check("WindowUtil null-argument checks throw ArgumentNullException", () =>
            {
                try { WindowUtil.MinimizeWindow(null!); return "expected ArgumentNullException"; }
                catch (ArgumentNullException) { }

                try { WindowUtil.SetWindowIcon(window, null!); return "expected ArgumentNullException"; }
                catch (ArgumentNullException) { }

                try { WindowUtil.DisableCursor(null!); return "expected ArgumentNullException"; }
                catch (ArgumentNullException) { }

                try { WindowUtil.GetCursorDelta(null!); return "expected ArgumentNullException"; }
                catch (ArgumentNullException) { }

                return null;
            });
        }
    }
}
