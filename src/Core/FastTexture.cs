using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MonoPrimitives
{
    /// <summary>
    /// A thin wrapper over a MonoGame <see cref="Texture2D"/> (or <see cref="RenderTarget2D"/>) that
    /// uploads full-texture or sub-rectangle pixel data via a direct <c>glTexSubImage2D</c> call,
    /// bypassing <see cref="Texture2D.SetData{T}(T[])"/>'s own per-call managed overhead. Measured
    /// ~2.5-2.7x faster than <c>SetData</c> for a 2500x2500 full-texture update once per real frame
    /// (DesktopGL, Windows) — see <c>tests/MonoPrimitives.Tests/FastTextureTests.cs</c>.
    ///
    /// This reaches into MonoGame's private internal GL texture handle via reflection — a
    /// supported-until-it-isn't arrangement that can break on a future MonoGame release. It is built
    /// to degrade safely instead: when the fast path can't be established (non-GL backend, renamed
    /// internal field, unsupported surface format, missing GL entry points), every <see cref="Update{T}(T[])"/>
    /// call transparently falls back to <c>SetData</c> instead of throwing. Check
    /// <see cref="IsRawUploadAvailable"/>/<see cref="Diagnostics"/> once at startup to see which path
    /// you actually got.
    ///
    /// Call <c>Update</c> only from the thread that owns the GL context (Update/Draw), never from a
    /// background task. <see cref="RenderTarget2D"/> is supported but must not be the active render
    /// target during <c>Update</c> — call <c>GraphicsDevice.SetRenderTarget(null)</c> first. Only mip
    /// level 0 is written; create textures for this with <c>mipMap: false</c>.
    /// </summary>
    public sealed unsafe class FastTexture : IDisposable
    {
        // ------------------------------------------------------------------
        // GL constants
        // ------------------------------------------------------------------
        private const uint GL_TEXTURE_2D = 0x0DE1;
        private const uint GL_TEXTURE0 = 0x84C0;
        private const uint GL_RGB = 0x1907;
        private const uint GL_RGBA = 0x1908;
        private const uint GL_UNSIGNED_BYTE = 0x1401;
        private const uint GL_UNSIGNED_SHORT_5_6_5 = 0x8363;
        private const uint GL_UNSIGNED_SHORT_4_4_4_4_REV = 0x8365;
        private const uint GL_UNSIGNED_SHORT_1_5_5_5_REV = 0x8366;
        private const uint GL_UNPACK_ALIGNMENT = 0x0CF5;
        private const uint GL_UNPACK_ROW_LENGTH = 0x0CF2;

        // ------------------------------------------------------------------
        // Cross-platform GL entry points, resolved once per process.
        //
        // Calling convention note: on x64 and arm64 (every desktop target MonoGame ships for in
        // practice) there is exactly one native calling convention, so Cdecl vs Stdcall is a
        // no-op distinction. It would matter on 32-bit Windows, where GL is __stdcall -- if you
        // ever target x86, change these to delegate* unmanaged[Stdcall].
        // ------------------------------------------------------------------
        private static delegate* unmanaged[Cdecl]<uint, uint, void> _glBindTexture;
        private static delegate* unmanaged[Cdecl]<uint, void> _glActiveTexture;
        private static delegate* unmanaged[Cdecl]<uint, int, void> _glPixelStorei;
        private static delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void> _glTexSubImage2D;

        private static bool _glResolved;
        private static string _glResolveDetail = "not attempted";
        private static readonly object _glLock = new object();

        // ------------------------------------------------------------------
        // Instance state
        // ------------------------------------------------------------------
        private readonly GraphicsDevice _device;
        private readonly Texture2D _texture;
        private readonly bool _ownsTexture;

        private readonly uint _glHandle;
        private readonly uint _glFormat;
        private readonly uint _glType;
        private readonly int _bytesPerPixel;
        private readonly bool _needsAlignmentFix;

        private bool _disposed;

        /// <summary>The underlying MonoGame texture. Draw with this as normal.</summary>
        public Texture2D Texture => _texture;

        /// <summary>Shorthand for <c>Texture.Width</c>.</summary>
        public int Width => _texture.Width;

        /// <summary>Shorthand for <c>Texture.Height</c>.</summary>
        public int Height => _texture.Height;

        /// <summary>
        /// True if the direct-GL fast path was successfully established. False means every
        /// <c>Update</c> call transparently uses <c>SetData</c> instead -- correct, just not faster.
        /// </summary>
        public bool IsRawUploadAvailable { get; }

        /// <summary>
        /// Human-readable explanation of which upload path is active and, if the fast path was
        /// unavailable, exactly why. Log this once at startup.
        /// </summary>
        public string Diagnostics { get; }

        /// <summary>
        /// When true (default), the GraphicsDevice texture-slot cache is invalidated after every
        /// raw upload. See <see cref="InvalidateDeviceTextureCache"/> for why this exists. Only set
        /// false if you have measured it and know your draw path re-binds anyway.
        /// </summary>
        public bool AutoInvalidateDeviceCache { get; set; } = true;

        // ==================================================================
        // Construction
        // ==================================================================

        /// <summary>Creates a new non-mipmapped texture of the given size and wraps it.</summary>
        public FastTexture(GraphicsDevice device, int width, int height, SurfaceFormat format = SurfaceFormat.Color)
            : this(device, new Texture2D(device, width, height, false, format), ownsTexture: true)
        {
        }

        /// <summary>
        /// Wraps an existing texture (including a <see cref="RenderTarget2D"/>).
        /// </summary>
        /// <param name="ownsTexture">
        /// If true, disposing this wrapper disposes the texture too. Pass false (the default) when
        /// the texture's lifetime is managed elsewhere.
        /// </param>
        public FastTexture(GraphicsDevice device, Texture2D texture, bool ownsTexture = false)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            _texture = texture ?? throw new ArgumentNullException(nameof(texture));
            _ownsTexture = ownsTexture;

            string why;
            if (!TryResolveGL(out string glDetail))
            {
                why = "GL entry points unavailable (" + glDetail + ")";
            }
            else if (!TryGetGlHandle(texture, out _glHandle, out string handleDetail))
            {
                why = "could not read MonoGame's internal GL texture handle (" + handleDetail + ")";
            }
            else if (!TryMapFormat(texture.Format, out _glFormat, out _glType, out _bytesPerPixel))
            {
                why = "SurfaceFormat." + texture.Format + " has no simple uncompressed GL mapping";
            }
            else
            {
                IsRawUploadAvailable = true;
                // GL's default unpack alignment is 4. That's already correct for 4-byte formats
                // (the common case, and the hot path -- zero extra GL calls). Narrower formats can
                // produce rows that aren't 4-byte aligned, so those need an explicit fix.
                _needsAlignmentFix = _bytesPerPixel != 4;

                string extra = texture.LevelCount > 1
                    ? " NOTE: texture has " + texture.LevelCount + " mip levels; only level 0 is written, lower levels will be stale."
                    : string.Empty;
                if (texture is RenderTarget2D)
                    extra += " NOTE: wrapping a RenderTarget2D -- do not Update() while it is the active render target.";

                Diagnostics = "FastTexture: raw glTexSubImage2D path ACTIVE (" + glDetail + ", format " +
                              texture.Format + ", handle " + _glHandle + ")." + extra;
                return;
            }

            IsRawUploadAvailable = false;
            Diagnostics = "FastTexture: falling back to Texture2D.SetData -- " + why +
                          ". Everything still works, just without the speedup.";
        }

        // ==================================================================
        // Public upload API
        // ==================================================================

        /// <summary>Uploads a full-texture pixel buffer. The array must contain exactly Width*Height elements.</summary>
        public void Update<T>(T[] data) where T : unmanaged
        {
            ThrowIfDisposed();
            if (data == null) throw new ArgumentNullException(nameof(data));

            int expectedElements = _texture.Width * _texture.Height;
            if (data.Length != expectedElements)
            {
                throw new ArgumentException(
                    "Expected exactly " + expectedElements + " elements for a " + _texture.Width + "x" + _texture.Height +
                    " texture, got " + data.Length + ".", nameof(data));
            }

            // Handled separately from the span overload so the fallback path can hand the array
            // straight to SetData -- no per-frame copy just to satisfy a signature.
            if (!IsRawUploadAvailable)
            {
                _texture.SetData(data);
                return;
            }

            Update(new ReadOnlySpan<T>(data));
        }

        /// <summary>Uploads a full-texture pixel buffer from a span.</summary>
        public void Update<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            ThrowIfDisposed();

            int expected = _texture.Width * _texture.Height;
            if (data.Length != expected)
            {
                throw new ArgumentException(
                    "Expected exactly " + expected + " elements for a " + _texture.Width + "x" + _texture.Height +
                    " texture, got " + data.Length + ".", nameof(data));
            }

            if (!IsRawUploadAvailable)
            {
                // Fallback path. SetData needs an array; copy only if we weren't handed one.
                _texture.SetData(data.ToArray());
                return;
            }

            if (sizeof(T) != _bytesPerPixel)
            {
                throw new ArgumentException(
                    "Element type " + typeof(T).Name + " is " + sizeof(T) + " bytes but SurfaceFormat." +
                    _texture.Format + " is " + _bytesPerPixel + " bytes per pixel.", nameof(data));
            }

            fixed (T* src = data)
            {
                UploadRaw(0, 0, _texture.Width, _texture.Height, src);
            }
        }

        /// <summary>
        /// Uploads a sub-rectangle. <paramref name="data"/> must be tightly packed for that
        /// rectangle: exactly <c>rect.Width * rect.Height</c> elements, row-major, no padding.
        /// </summary>
        public void Update<T>(Rectangle rect, ReadOnlySpan<T> data) where T : unmanaged
        {
            ThrowIfDisposed();

            if (rect.Left < 0 || rect.Top < 0 || rect.Right > _texture.Width || rect.Bottom > _texture.Height)
                throw new ArgumentOutOfRangeException(nameof(rect), "Rectangle lies outside the texture bounds.");

            int expected = rect.Width * rect.Height;
            if (data.Length != expected)
                throw new ArgumentException("Expected exactly " + expected + " elements for that rectangle, got " + data.Length + ".", nameof(data));

            if (!IsRawUploadAvailable)
            {
                _texture.SetData(0, rect, data.ToArray(), 0, data.Length);
                return;
            }

            if (sizeof(T) != _bytesPerPixel)
                throw new ArgumentException("Element size does not match the surface format's bytes per pixel.", nameof(data));

            fixed (T* src = data)
            {
                UploadRaw(rect.X, rect.Y, rect.Width, rect.Height, src);
            }
        }

        /// <summary>
        /// Uploads from an unmanaged pointer -- for data you already hold natively (decoders,
        /// native buffers, memory-mapped files) with no managed copy in between.
        /// <paramref name="sizeInBytes"/> is validated against the full texture size.
        /// </summary>
        public void Update(IntPtr data, int sizeInBytes)
        {
            ThrowIfDisposed();
            if (data == IntPtr.Zero) throw new ArgumentNullException(nameof(data));

            int bpp = BytesPerPixelOf(_texture.Format);
            if (bpp == 0)
                throw new NotSupportedException("SurfaceFormat." + _texture.Format + " has no fixed bytes-per-pixel this wrapper understands.");

            int expected = _texture.Width * _texture.Height * bpp;
            if (sizeInBytes != expected)
                throw new ArgumentException("Expected " + expected + " bytes, got " + sizeInBytes + ".", nameof(sizeInBytes));

            if (!IsRawUploadAvailable)
            {
                // No pointer-taking public API on Texture2D across all MonoGame versions, so stage
                // through a managed array on the fallback path.
                byte[] staging = new byte[sizeInBytes];
                Marshal.Copy(data, staging, 0, sizeInBytes);
                _texture.SetData(staging);
                return;
            }

            UploadRaw(0, 0, _texture.Width, _texture.Height, (void*)data);
        }

        /// <summary>
        /// Clears MonoGame's cached knowledge of which texture is bound to each sampler slot.
        ///
        /// WHY: GraphicsDevice keeps a per-slot cache of what it believes is bound, and skips a
        /// redundant glBindTexture -- along with the sampler-state application that rides with a
        /// real bind -- whenever it thinks nothing changed. Our raw glBindTexture happens entirely
        /// outside that cache, so MonoGame has no way to know the GL binding moved underneath it.
        /// Left uncorrected, a later SpriteBatch/Effect draw that reuses the same slot reference
        /// without any other texture switching in between can skip a bind it actually needed.
        /// Nulling the affected slots forces the next assignment to read as a genuine change,
        /// guaranteeing a real re-bind.
        ///
        /// Called automatically after every raw upload unless <see cref="AutoInvalidateDeviceCache"/>
        /// is turned off.
        /// </summary>
        public void InvalidateDeviceTextureCache()
        {
            // Slot 0 is what SpriteBatch uses and is the one that always matters. Higher slots only
            // matter if a custom Effect bound this texture there; clear any slot currently holding
            // it, and stop at the first out-of-range index rather than assuming a fixed count.
            for (int i = 0; i < 16; i++)
            {
                try
                {
                    if (i == 0 || ReferenceEquals(_device.Textures[i], _texture))
                        _device.Textures[i] = null;
                }
                catch (ArgumentOutOfRangeException) { break; }
                catch (IndexOutOfRangeException) { break; }
            }
        }

        // ==================================================================
        // Raw upload
        // ==================================================================

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UploadRaw(int x, int y, int width, int height, void* pixels)
        {
            // Bind to a deterministic texture unit. Without this we'd upload into whatever unit
            // MonoGame happened to leave active, which is not something we should assume.
            _glActiveTexture(GL_TEXTURE0);
            _glBindTexture(GL_TEXTURE_2D, _glHandle);

            if (_needsAlignmentFix)
            {
                _glPixelStorei(GL_UNPACK_ALIGNMENT, _bytesPerPixel);
                _glPixelStorei(GL_UNPACK_ROW_LENGTH, 0);
            }

            _glTexSubImage2D(GL_TEXTURE_2D, 0, x, y, width, height, _glFormat, _glType, pixels);

            if (_needsAlignmentFix)
            {
                // Restore GL's defaults so we don't leave surprises for MonoGame's own upload paths.
                _glPixelStorei(GL_UNPACK_ALIGNMENT, 4);
            }

            if (AutoInvalidateDeviceCache)
                InvalidateDeviceTextureCache();
        }

        /// <summary>
        /// Bytes per pixel for the formats this wrapper handles, independent of whether the fast
        /// path is active (the fallback path still needs it to validate sizes).
        /// </summary>
        private static int BytesPerPixelOf(SurfaceFormat format)
        {
            switch (format)
            {
                case SurfaceFormat.Color: return 4;
                case SurfaceFormat.Bgra5551:
                case SurfaceFormat.Bgra4444:
                case SurfaceFormat.Bgr565: return 2;
                default: return 0;
            }
        }

        // ==================================================================
        // GL entry point resolution (Windows / macOS / Linux)
        // ==================================================================

        private static bool TryResolveGL(out string detail)
        {
            lock (_glLock)
            {
                if (_glResolved || _glBindTexture != null)
                {
                    detail = _glResolveDetail;
                    return _glBindTexture != null;
                }

                _glResolved = true;

                // Preferred: ask SDL for the addresses. MonoGame's DesktopGL backend is built on
                // SDL2, SDL is already loaded in-process, and SDL_GL_GetProcAddress is the correct
                // way to resolve GL functions on every platform -- it honours the current context
                // and works for entry points the system GL library doesn't export statically.
                if (TryResolveViaSdl(out detail))
                    return true;

                string sdlFailure = detail;

                // Fallback: pull the symbols straight out of the system GL library. Works here only
                // because glBindTexture/glTexSubImage2D/glPixelStorei are OpenGL 1.1 core and
                // glActiveTexture is 1.3 -- old enough to be statically exported. Anything newer
                // would need the SDL path above.
                if (TryResolveViaSystemGL(out detail))
                    return true;

                detail = "SDL: " + sdlFailure + "; system GL: " + detail;
                _glResolveDetail = detail;
                return false;
            }
        }

        private static bool TryResolveViaSdl(out string detail)
        {
            string[] candidates = GetSdlCandidates();
            foreach (string name in candidates)
            {
                if (!NativeLibrary.TryLoad(name, out IntPtr sdl))
                    continue;
                if (!NativeLibrary.TryGetExport(sdl, "SDL_GL_GetProcAddress", out IntPtr getProcAddr))
                    continue;

                var getProc = (delegate* unmanaged[Cdecl]<byte*, IntPtr>)getProcAddr;

                IntPtr bind = SdlGetProc(getProc, "glBindTexture");
                IntPtr sub = SdlGetProc(getProc, "glTexSubImage2D");
                IntPtr active = SdlGetProc(getProc, "glActiveTexture");
                IntPtr store = SdlGetProc(getProc, "glPixelStorei");

                if (bind == IntPtr.Zero || sub == IntPtr.Zero || active == IntPtr.Zero || store == IntPtr.Zero)
                    continue;

                Assign(bind, sub, active, store);
                detail = "resolved via SDL_GL_GetProcAddress from " + name;
                _glResolveDetail = detail;
                return true;
            }

            detail = "SDL2 not loadable, or SDL_GL_GetProcAddress returned null (is a GL context current?)";
            return false;
        }

        private static IntPtr SdlGetProc(delegate* unmanaged[Cdecl]<byte*, IntPtr> getProc, string name)
        {
            Span<byte> utf8 = stackalloc byte[64];
            int n = System.Text.Encoding.UTF8.GetBytes(name, utf8);
            utf8[n] = 0;
            fixed (byte* p = utf8)
            {
                return getProc(p);
            }
        }

        private static bool TryResolveViaSystemGL(out string detail)
        {
            foreach (string name in GetGLCandidates())
            {
                if (!NativeLibrary.TryLoad(name, out IntPtr gl))
                    continue;

                if (NativeLibrary.TryGetExport(gl, "glBindTexture", out IntPtr bind) &&
                    NativeLibrary.TryGetExport(gl, "glTexSubImage2D", out IntPtr sub) &&
                    NativeLibrary.TryGetExport(gl, "glActiveTexture", out IntPtr active) &&
                    NativeLibrary.TryGetExport(gl, "glPixelStorei", out IntPtr store))
                {
                    Assign(bind, sub, active, store);
                    detail = "resolved from " + name;
                    _glResolveDetail = detail;
                    return true;
                }
            }

            detail = "no system GL library exported the required symbols";
            return false;
        }

        private static void Assign(IntPtr bind, IntPtr sub, IntPtr active, IntPtr store)
        {
            _glBindTexture = (delegate* unmanaged[Cdecl]<uint, uint, void>)bind;
            _glTexSubImage2D = (delegate* unmanaged[Cdecl]<uint, int, int, int, int, int, uint, uint, void*, void>)sub;
            _glActiveTexture = (delegate* unmanaged[Cdecl]<uint, void>)active;
            _glPixelStorei = (delegate* unmanaged[Cdecl]<uint, int, void>)store;
        }

        private static string[] GetSdlCandidates()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new[] { "SDL2.dll", "SDL2" };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new[] { "libSDL2.dylib", "libSDL2-2.0.0.dylib", "SDL2" };
            return new[] { "libSDL2-2.0.so.0", "libSDL2.so", "libSDL2-2.0.so", "SDL2" };
        }

        private static string[] GetGLCandidates()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new[] { "opengl32.dll", "opengl32" };
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new[] { "/System/Library/Frameworks/OpenGL.framework/OpenGL", "libGL.dylib" };
            return new[] { "libGL.so.1", "libGL.so" };
        }

        // ==================================================================
        // MonoGame internals
        // ==================================================================

        private static bool TryGetGlHandle(Texture2D texture, out uint handle, out string detail)
        {
            handle = 0;

            // The handle lives on the Texture base class, not Texture2D, and Type.GetField does not
            // return non-public fields declared on base types -- so walk the hierarchy explicitly.
            // Name candidates cover the variations seen across MonoGame forks and versions.
            string[] names = { "glTexture", "_glTexture", "GLTexture" };

            for (Type? t = texture.GetType(); t != null; t = t.BaseType)
            {
                foreach (string name in names)
                {
                    FieldInfo? f = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    if (f == null) continue;

                    object? value = f.GetValue(texture);
                    if (value == null) continue;

                    try
                    {
                        long raw = Convert.ToInt64(value);
                        if (raw <= 0)
                        {
                            detail = "field '" + name + "' on " + t.Name + " held " + raw +
                                     " -- the GL texture object has not been created yet";
                            return false;
                        }
                        handle = (uint)raw;
                        detail = "field '" + name + "' on " + t.Name;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        detail = "field '" + name + "' on " + t.Name + " was type " +
                                 value.GetType().Name + ", not convertible to an integer handle (" + ex.Message + ")";
                        return false;
                    }
                }
            }

            detail = "no glTexture-like field found on " + texture.GetType().Name +
                     " or its base types -- this is expected on the WindowsDX and DesktopVK backends";
            return false;
        }

        private static bool TryMapFormat(SurfaceFormat format, out uint glFormat, out uint glType, out int bytesPerPixel)
        {
            switch (format)
            {
                case SurfaceFormat.Color:
                    glFormat = GL_RGBA; glType = GL_UNSIGNED_BYTE; bytesPerPixel = 4; return true;

                case SurfaceFormat.Bgra5551:
                    glFormat = GL_RGBA; glType = GL_UNSIGNED_SHORT_1_5_5_5_REV; bytesPerPixel = 2; return true;

                case SurfaceFormat.Bgra4444:
                    glFormat = GL_RGBA; glType = GL_UNSIGNED_SHORT_4_4_4_4_REV; bytesPerPixel = 2; return true;

                case SurfaceFormat.Bgr565:
                    glFormat = GL_RGB; glType = GL_UNSIGNED_SHORT_5_6_5; bytesPerPixel = 2; return true;

                default:
                    // Compressed (DXT/BC), floating point, and depth formats need different upload
                    // entry points entirely -- not worth special-casing; SetData handles them.
                    glFormat = 0; glType = 0; bytesPerPixel = 0; return false;
            }
        }

        // ==================================================================
        // Lifetime
        // ==================================================================

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FastTexture));
        }

        /// <summary>Disposes the wrapped <see cref="Texture"/> too, but only if this instance was constructed with <c>ownsTexture: true</c>.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_ownsTexture && _texture != null && !_texture.IsDisposed)
                _texture.Dispose();
        }
    }
}
