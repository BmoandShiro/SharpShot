using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using D3D = Vortice.Direct3D11.D3D11;
using DxgiFactory = Vortice.DXGI.DXGI;

namespace SharpShot.Utils
{
    /// <summary>
    /// Captures the virtual desktop via DXGI Desktop Duplication.
    /// Blits GPU staging textures directly into the composite (no per-monitor Bitmap,
    /// no unused full-desktop clone). WAIT_TIMEOUT reuses staging contents.
    /// </summary>
    internal static class DxgiDesktopCapture
    {
        private static readonly FeatureLevel[] FeatureLevels =
        {
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0,
        };

        private const int DxgiErrorWaitTimeout = unchecked((int)0x887A0027);
        private const int DxgiErrorAccessLost = unchecked((int)0x887A0026);

        private static readonly object CacheLock = new();
        private static List<OutputSession>? _sessions;
        private static bool _displayHooked;

        public static Bitmap? TryCaptureVirtualDesktop(out Rectangle bounds, out string mode, bool requireFreshFrame = false)
        {
            bounds = GetVirtualDesktopBounds();
            mode = "dxgi-failed";

            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            try
            {
                EnsureDisplayChangeHook();
                var sessions = GetOrCreateSessions();
                if (sessions.Count == 0)
                {
                    mode = "dxgi-no-outputs";
                    return null;
                }

                var virtualBounds = bounds;
                var composed = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(composed))
                {
                    g.Clear(Color.Black); // opaque; preserved by ReadWrite LockBits below
                }
                var composedData = composed.LockBits(
                    new Rectangle(0, 0, composed.Width, composed.Height),
                    ImageLockMode.ReadWrite,
                    PixelFormat.Format32bppArgb);

                int fresh = 0, reused = 0, gdi = 0, captured = 0;
                try
                {
                    var sources = new FrameSource[sessions.Count];
                    Parallel.For(0, sessions.Count, i =>
                    {
                        var session = sessions[i];
                        FrameSource source;
                        bool ok;
                        try
                        {
                            ok = session.CaptureInto(composedData, virtualBounds, out source, requireFreshFrame);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"DXGI output capture failed ({session.DeviceName}): {ex.Message}");
                            ok = false;
                            source = FrameSource.Failed;
                        }

                        if (!ok)
                        {
                            ok = BlitMonitorGdiInto(composedData, virtualBounds, session.Bounds);
                            source = ok ? FrameSource.GdiFallback : FrameSource.Failed;
                        }

                        sources[i] = source;
                    });

                    foreach (var source in sources)
                    {
                        if (source == FrameSource.Failed) continue;
                        captured++;
                        switch (source)
                        {
                            case FrameSource.FreshDxgi: fresh++; break;
                            case FrameSource.ReusedLast: reused++; break;
                            case FrameSource.GdiFallback: gdi++; break;
                        }
                    }
                }
                finally
                {
                    composed.UnlockBits(composedData);
                }

                if (captured == 0)
                {
                    composed.Dispose();
                    mode = "dxgi-no-outputs";
                    return null;
                }

                mode = gdi > 0
                    ? $"dxgi-partial-gdi:fresh={fresh},reuse={reused},gdi={gdi}"
                    : reused == captured
                        ? $"dxgi-reuse:{reused}"
                        : $"dxgi-outputs:{captured},fresh={fresh},reuse={reused}";

                if (!LooksLikeValidOpaqueFrame(composed))
                {
                    composed.Dispose();
                    mode = "dxgi-invalid-frame";
                    return null;
                }

                return composed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DXGI virtual desktop capture failed: {ex.Message}");
                InvalidateAllSessions();
                mode = "dxgi-exception";
                return null;
            }
        }

        public static Bitmap? TryCaptureRegion(Rectangle region, out string mode, bool requireFreshFrame = false)
        {
            mode = "dxgi-region-failed";
            if (region.Width <= 0 || region.Height <= 0)
                return null;

            var full = TryCaptureVirtualDesktop(out var bounds, out mode, requireFreshFrame);
            if (full == null)
                return null;

            try
            {
                var srcX = region.X - bounds.X;
                var srcY = region.Y - bounds.Y;
                var crop = Rectangle.Intersect(
                    new Rectangle(0, 0, full.Width, full.Height),
                    new Rectangle(srcX, srcY, region.Width, region.Height));
                if (crop.Width <= 0 || crop.Height <= 0)
                {
                    full.Dispose();
                    mode = "dxgi-region-miss";
                    return null;
                }

                var clone = full.Clone(crop, PixelFormat.Format32bppArgb);
                full.Dispose();
                EnsureOpaqueAlpha(clone);
                mode = mode.StartsWith("dxgi", StringComparison.Ordinal)
                    ? mode + "-region"
                    : "dxgi-region";
                return clone;
            }
            catch
            {
                full.Dispose();
                throw;
            }
        }

        public static Bitmap CaptureVirtualDesktopGdi(Rectangle bounds)
        {
            var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
            EnsureOpaqueAlpha(bmp);
            return bmp;
        }

        public static Rectangle GetVirtualDesktopBounds()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            if (screens.Length == 0)
            {
                var primary = System.Windows.Forms.Screen.PrimaryScreen;
                return primary?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;
            foreach (var screen in screens)
            {
                if (screen == null) continue;
                minX = Math.Min(minX, screen.Bounds.X);
                minY = Math.Min(minY, screen.Bounds.Y);
                maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
            }

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        public static void EnsureOpaqueAlpha(Bitmap bitmap)
        {
            if (bitmap == null) throw new ArgumentNullException(nameof(bitmap));

            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)data.Scan0;
                    int height = bitmap.Height;
                    int width = bitmap.Width;
                    int stride = data.Stride;
                    for (int y = 0; y < height; y++)
                    {
                        uint* row = (uint*)(basePtr + y * stride);
                        for (int x = 0; x < width; x++)
                            row[x] |= 0xFF000000u;
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        public static Bitmap CloneOpaque(Bitmap source, Rectangle cropRect)
        {
            var clone = source.Clone(cropRect, PixelFormat.Format32bppArgb);
            EnsureOpaqueAlpha(clone);
            return clone;
        }

        public static void Warmup()
        {
            try
            {
                EnsureDisplayChangeHook();
                GetOrCreateSessions();
                _ = TryCaptureVirtualDesktop(out _, out _);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DXGI warmup failed: {ex.Message}");
            }
        }

        private enum FrameSource
        {
            Failed,
            FreshDxgi,
            ReusedLast,
            GdiFallback
        }

        private static bool LooksLikeValidOpaqueFrame(Bitmap bitmap)
        {
            int w = bitmap.Width;
            int h = bitmap.Height;
            if (w <= 0 || h <= 0) return false;

            var data = bitmap.LockBits(
                new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);
            try
            {
                unsafe
                {
                    byte* basePtr = (byte*)data.Scan0;
                    int stride = data.Stride;
                    int opaque = 0;
                    int samples = 0;
                    int[] xs = { 0, w / 2, w - 1, w / 4, (3 * w) / 4 };
                    int[] ys = { 0, h / 2, h - 1, h / 4, (3 * h) / 4 };
                    foreach (int y in ys)
                    {
                        if (y < 0 || y >= h) continue;
                        uint* row = (uint*)(basePtr + y * stride);
                        foreach (int x in xs)
                        {
                            if (x < 0 || x >= w) continue;
                            samples++;
                            if ((row[x] >> 24) == 0xFFu)
                                opaque++;
                        }
                    }

                    return samples > 0 && opaque == samples;
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
        }

        private static unsafe void BlitMappedOpaque(
            IntPtr srcPtr,
            int srcPitch,
            BitmapData dest,
            Rectangle outputBounds,
            Rectangle virtualBounds)
        {
            int destX = outputBounds.X - virtualBounds.X;
            int destY = outputBounds.Y - virtualBounds.Y;
            int copyW = Math.Min(outputBounds.Width, dest.Width - destX);
            int copyH = Math.Min(outputBounds.Height, dest.Height - destY);
            if (copyW <= 0 || copyH <= 0 || destX < 0 || destY < 0)
                return;

            byte* srcBase = (byte*)srcPtr;
            byte* dstBase = (byte*)dest.Scan0;
            int dstStride = dest.Stride;

            for (int y = 0; y < copyH; y++)
            {
                uint* src = (uint*)(srcBase + y * srcPitch);
                uint* dst = (uint*)(dstBase + (destY + y) * dstStride + destX * 4);
                for (int x = 0; x < copyW; x++)
                    dst[x] = src[x] | 0xFF000000u;
            }
        }

        private static bool BlitMonitorGdiInto(BitmapData dest, Rectangle virtualBounds, Rectangle monitorBounds)
        {
            try
            {
                using var bmp = new Bitmap(monitorBounds.Width, monitorBounds.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.CopyFromScreen(monitorBounds.X, monitorBounds.Y, 0, 0, monitorBounds.Size, CopyPixelOperation.SourceCopy);
                }

                var data = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb);
                try
                {
                    BlitMappedOpaque(data.Scan0, data.Stride, dest, monitorBounds, virtualBounds);
                }
                finally
                {
                    bmp.UnlockBits(data);
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GDI monitor fallback failed: {ex.Message}");
                return false;
            }
        }

        private static void EnsureDisplayChangeHook()
        {
            lock (CacheLock)
            {
                if (_displayHooked) return;
                try
                {
                    Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (_, _) => InvalidateAllSessions();
                    _displayHooked = true;
                }
                catch
                {
                    // SystemEvents may be unavailable in some hosts.
                }
            }
        }

        private static List<OutputSession> GetOrCreateSessions()
        {
            lock (CacheLock)
            {
                if (_sessions != null && _sessions.Count > 0)
                    return _sessions;

                DisposeSessions_NoLock();
                _sessions = BuildSessions();
                return _sessions;
            }
        }

        private static List<OutputSession> BuildSessions()
        {
            var list = new List<OutputSession>();
            using var factory = DxgiFactory.CreateDXGIFactory1<IDXGIFactory1>();

            for (uint adapterIndex = 0; ; adapterIndex++)
            {
                Result hr = factory.EnumAdapters1(adapterIndex, out IDXGIAdapter1 adapter);
                if (hr.Failure || adapter == null)
                    break;

                try
                {
                    for (uint outputIndex = 0; ; outputIndex++)
                    {
                        hr = adapter.EnumOutputs(outputIndex, out IDXGIOutput output);
                        if (hr.Failure || output == null)
                            break;

                        try
                        {
                            var desc = output.Description;
                            if (!desc.AttachedToDesktop)
                            {
                                output.Dispose();
                                continue;
                            }

                            var coords = desc.DesktopCoordinates;
                            var bounds = Rectangle.FromLTRB(coords.Left, coords.Top, coords.Right, coords.Bottom);
                            if (bounds.Width <= 0 || bounds.Height <= 0)
                            {
                                output.Dispose();
                                continue;
                            }

                            var session = OutputSession.TryCreate(adapter, output, bounds, desc.DeviceName ?? $"output-{adapterIndex}-{outputIndex}");
                            output.Dispose();
                            if (session != null)
                                list.Add(session);
                        }
                        catch
                        {
                            output?.Dispose();
                        }
                    }
                }
                finally
                {
                    adapter.Dispose();
                }
            }

            return list;
        }

        private static void InvalidateAllSessions()
        {
            lock (CacheLock)
            {
                DisposeSessions_NoLock();
            }
        }

        private static void DisposeSessions_NoLock()
        {
            if (_sessions == null) return;
            foreach (var s in _sessions)
            {
                try { s.Dispose(); } catch { /* ignore */ }
            }
            _sessions = null;
        }

        private sealed class OutputSession : IDisposable
        {
            private readonly object _lock = new();
            private ID3D11Device? _device;
            private ID3D11DeviceContext? _context;
            private IDXGIOutputDuplication? _duplication;
            private ID3D11Texture2D? _staging;
            private bool _hasValidStaging;
            private bool _disposed;

            public Rectangle Bounds { get; }
            public string DeviceName { get; }

            private OutputSession(Rectangle bounds, string deviceName)
            {
                Bounds = bounds;
                DeviceName = deviceName;
            }

            public static OutputSession? TryCreate(IDXGIAdapter1 adapter, IDXGIOutput output, Rectangle bounds, string deviceName)
            {
                Result createHr = D3D.D3D11CreateDevice(
                    adapter,
                    DriverType.Unknown,
                    DeviceCreationFlags.BgraSupport,
                    FeatureLevels,
                    out ID3D11Device device,
                    out ID3D11DeviceContext context);

                if (createHr.Failure || device == null || context == null)
                {
                    device?.Dispose();
                    context?.Dispose();
                    return null;
                }

                try
                {
                    using var output1 = output.QueryInterface<IDXGIOutput1>();
                    var duplication = output1.DuplicateOutput(device);

                    var stagingDesc = new Texture2DDescription
                    {
                        Width = (uint)bounds.Width,
                        Height = (uint)bounds.Height,
                        MipLevels = 1,
                        ArraySize = 1,
                        Format = Format.B8G8R8A8_UNorm,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Staging,
                        BindFlags = BindFlags.None,
                        CPUAccessFlags = CpuAccessFlags.Read,
                        MiscFlags = ResourceOptionFlags.None
                    };
                    var staging = device.CreateTexture2D(stagingDesc);

                    var session = new OutputSession(bounds, deviceName)
                    {
                        _device = device,
                        _context = context,
                        _duplication = duplication,
                        _staging = staging
                    };
                    device = null!;
                    context = null!;
                    return session;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"DXGI DuplicateOutput failed ({deviceName}): {ex.Message}");
                    device?.Dispose();
                    context?.Dispose();
                    return null;
                }
            }

            /// <summary>
            /// Acquire (or reuse staging on WAIT_TIMEOUT) and blit directly into the composite.
            /// </summary>
            public bool CaptureInto(BitmapData dest, Rectangle virtualBounds, out FrameSource source, bool requireFreshFrame = false)
            {
                source = FrameSource.Failed;
                lock (_lock)
                {
                    if (_disposed || _device == null || _context == null || _duplication == null || _staging == null)
                        return false;

                    try
                    {
                        return CaptureIntoCore(dest, virtualBounds, out source, requireFreshFrame);
                    }
                    catch (Exception ex) when (
                        ex is SharpGenException sg && sg.ResultCode.Code == DxgiErrorAccessLost
                        || ex.Message.Contains("ACCESS_LOST", StringComparison.Ordinal))
                    {
                        try
                        {
                            RecreateDuplication_NoLock();
                            _hasValidStaging = false;
                            return CaptureIntoCore(dest, virtualBounds, out source, requireFreshFrame);
                        }
                        catch (Exception recreateEx)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"DXGI recreate after ACCESS_LOST failed ({DeviceName}): {recreateEx.Message}");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"DXGI CaptureInto error ({DeviceName}): {ex.Message}");
                        return false;
                    }
                }
            }

            private bool CaptureIntoCore(BitmapData dest, Rectangle virtualBounds, out FrameSource source, bool requireFreshFrame)
            {
                // A cached desktop frame still contains SharpShot if it was copied before the window was excluded.
                bool haveStaging = _hasValidStaging && !requireFreshFrame;
                int maxAttempts = requireFreshFrame ? 8 : (haveStaging ? 1 : 8);
                int timeoutMs = requireFreshFrame ? 50 : (haveStaging ? 0 : 50);

                IDXGIResource? desktopResource = null;
                Result acquireHr = Result.Fail;

                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    acquireHr = _duplication!.AcquireNextFrame((uint)timeoutMs, out _, out desktopResource);
                    if (acquireHr.Success && desktopResource != null)
                        break;

                    desktopResource?.Dispose();
                    desktopResource = null;

                    if (acquireHr.Code == DxgiErrorAccessLost)
                        throw new InvalidOperationException("DXGI_ERROR_ACCESS_LOST");

                    if (acquireHr.Code != DxgiErrorWaitTimeout)
                        break;
                }

                // Desktop unchanged: staging still holds the last CopyResource result.
                // Do not reuse it when the caller just hid or excluded a window — that frame is stale.
                if (acquireHr.Code == DxgiErrorWaitTimeout || desktopResource == null)
                {
                    if (requireFreshFrame || !_hasValidStaging)
                    {
                        source = FrameSource.Failed;
                        return false;
                    }

                    MappedSubresource mappedReuse = _context!.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        BlitMappedOpaque(mappedReuse.DataPointer, (int)mappedReuse.RowPitch, dest, Bounds, virtualBounds);
                    }
                    finally
                    {
                        _context.Unmap(_staging, 0);
                    }

                    source = FrameSource.ReusedLast;
                    return true;
                }

                if (acquireHr.Failure)
                {
                    source = FrameSource.Failed;
                    return false;
                }

                try
                {
                    using (desktopResource)
                    using (var frameTex = desktopResource.QueryInterface<ID3D11Texture2D>())
                    {
                        _context!.CopyResource(_staging!, frameTex);
                    }

                    MappedSubresource mapped = _context!.Map(_staging!, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
                    try
                    {
                        BlitMappedOpaque(mapped.DataPointer, (int)mapped.RowPitch, dest, Bounds, virtualBounds);
                        _hasValidStaging = true;
                        source = FrameSource.FreshDxgi;
                        return true;
                    }
                    finally
                    {
                        _context.Unmap(_staging, 0);
                    }
                }
                finally
                {
                    try { _duplication!.ReleaseFrame(); } catch { /* ignore */ }
                }
            }

            private void RecreateDuplication_NoLock()
            {
                try { _duplication?.Dispose(); } catch { /* ignore */ }
                _duplication = null;

                if (_device == null)
                    throw new InvalidOperationException("Device disposed");

                using var factory = DxgiFactory.CreateDXGIFactory1<IDXGIFactory1>();
                for (uint adapterIndex = 0; ; adapterIndex++)
                {
                    Result hr = factory.EnumAdapters1(adapterIndex, out IDXGIAdapter1 adapter);
                    if (hr.Failure || adapter == null)
                        break;

                    using (adapter)
                    {
                        for (uint outputIndex = 0; ; outputIndex++)
                        {
                            hr = adapter.EnumOutputs(outputIndex, out IDXGIOutput output);
                            if (hr.Failure || output == null)
                                break;

                            using (output)
                            {
                                var coords = output.Description.DesktopCoordinates;
                                var b = Rectangle.FromLTRB(coords.Left, coords.Top, coords.Right, coords.Bottom);
                                if (b != Bounds)
                                    continue;

                                using var output1 = output.QueryInterface<IDXGIOutput1>();
                                _duplication = output1.DuplicateOutput(_device);
                                return;
                            }
                        }
                    }
                }

                throw new InvalidOperationException("Could not recreate DXGI output duplication");
            }

            public void Dispose()
            {
                lock (_lock)
                {
                    if (_disposed) return;
                    _disposed = true;
                    try { _duplication?.Dispose(); } catch { /* ignore */ }
                    try { _staging?.Dispose(); } catch { /* ignore */ }
                    try { _context?.Dispose(); } catch { /* ignore */ }
                    try { _device?.Dispose(); } catch { /* ignore */ }
                    _duplication = null;
                    _staging = null;
                    _context = null;
                    _device = null;
                    _hasValidStaging = false;
                }
            }
        }
    }
}
