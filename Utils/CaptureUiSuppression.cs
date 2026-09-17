using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using SharpShot.Services;

namespace SharpShot.Utils
{
    /// <summary>
    /// Temporarily hides SharpShot windows so they are not included in GDI screen captures.
    /// </summary>
    internal static class CaptureUiSuppression
    {
        /// <param name="excludeFromHide">When set (e.g. the editor during <c>RenderToBitmap</c>), that window stays visible so the capture still reflects its content.</param>
        public static IDisposable BeginIfEnabled(SettingsService? settings, Window? excludeFromHide = null)
        {
            if (settings?.CurrentSettings == null || !settings.CurrentSettings.HideSharpShotWindowsDuringCapture)
                return NoOpDisposable.Instance;

            var app = Application.Current;
            if (app?.Dispatcher == null)
                return NoOpDisposable.Instance;

            var snapshots = new List<(Window W, Visibility Previous)>();
            var cloaked = new List<Window>();
            var temporarilyCapturable = new List<Window>();

            void HideOnUiThread()
            {
                foreach (Window w in app.Windows)
                {
                    if (!CaptureExclusion.IsSharpShotWindow(w))
                        continue;

                    // Editor recapture must stay in BitBlt; every other SharpShot HWND is excluded.
                    if (ReferenceEquals(w, excludeFromHide))
                    {
                        CaptureExclusion.TrySet(w, false);
                        CaptureExclusion.TrySetCloaked(w, false);
                        temporarilyCapturable.Add(w);
                        continue;
                    }

                    CaptureExclusion.TrySet(w, true);
                    if (CaptureExclusion.TrySetCloaked(w, true))
                        cloaked.Add(w);
                    else
                    {
                        snapshots.Add((w, w.Visibility));
                        w.Visibility = Visibility.Hidden;
                    }
                }

                if (cloaked.Count == 0 && snapshots.Count == 0)
                    return;

                app.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
                System.Threading.Thread.Sleep(50);
            }

            if (app.Dispatcher.CheckAccess())
                HideOnUiThread();
            else
                app.Dispatcher.Invoke(HideOnUiThread);

            return new RestoreScope(snapshots, cloaked, temporarilyCapturable);
        }

        private sealed class RestoreScope : IDisposable
        {
            private readonly List<(Window W, Visibility Previous)> _snapshots;
            private readonly List<Window> _cloaked;
            private readonly List<Window> _temporarilyCapturable;
            private bool _disposed;

            public RestoreScope(
                List<(Window W, Visibility Previous)> snapshots,
                List<Window> cloaked,
                List<Window> temporarilyCapturable)
            {
                _snapshots = snapshots;
                _cloaked = cloaked;
                _temporarilyCapturable = temporarilyCapturable;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;
                _disposed = true;

                var app = Application.Current;
                if (app?.Dispatcher == null)
                    return;

                void Restore()
                {
                    foreach (var (w, prev) in _snapshots)
                    {
                        try
                        {
                            w.Visibility = prev;
                        }
                        catch
                        {
                            // Window may already be closed
                        }
                    }

                    foreach (var w in _cloaked)
                    {
                        try
                        {
                            CaptureExclusion.TrySetCloaked(w, false);
                        }
                        catch
                        {
                            // Window may already be closed
                        }
                    }

                    foreach (var w in _temporarilyCapturable)
                    {
                        try
                        {
                            CaptureExclusion.TrySet(w, true);
                        }
                        catch
                        {
                            // Window may already be closed
                        }
                    }
                }

                if (app.Dispatcher.CheckAccess())
                    Restore();
                else
                    app.Dispatcher.Invoke(Restore);
            }
        }

        private sealed class NoOpDisposable : IDisposable
        {
            public static readonly NoOpDisposable Instance = new();
            public void Dispose() { }
        }
    }
}
