using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NAudio.Wave;
using SharpShot.Models;
using SharpShot.Services;
// using ScreenRecorderLib; // Temporarily commented out for MSIX build

namespace SharpShot.Services
{
    public class RecordingService
    {
        private readonly SettingsService _settingsService;
        private readonly OBSRecordingService? _obsRecordingService;
        private string? _currentRecordingPath;

        private Process? _ffmpegProcess; // Track FFmpeg process for stopping
        private bool _isRecording = false;

        /// <summary>NAudio loopback → raw PCM/F32 into FFmpeg via a named pipe (avoids FFmpeg WASAPI demuxer issues).</summary>
        private WasapiLoopbackCapture? _loopbackCapture;
        private NamedPipeServerStream? _audioPipe;
        private ChannelWriter<PooledAudioChunk>? _audioChunkWriter;
        private Task? _audioPipePumpTask;

        private readonly struct PooledAudioChunk
        {
            public byte[] Buffer { get; }
            public int Length { get; }

            public PooledAudioChunk(byte[] buffer, int length)
            {
                Buffer = buffer;
                Length = length;
            }
        }

        // Events that MainWindow expects
        public event EventHandler<bool>? RecordingStateChanged;
        public event EventHandler<TimeSpan>? RecordingTimeUpdated;

        public RecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Initialize OBS recording service
            try
            {
                _obsRecordingService = new OBSRecordingService(settingsService);
                _obsRecordingService.RecordingStateChanged += OnOBSRecordingStateChanged;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize OBS recording service: {ex.Message}");
                _obsRecordingService = null;
            }
        }

        public async Task StartRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            if (_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("Recording already in progress");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"=== StartRecordingAsync called ===");
                System.Diagnostics.Debug.WriteLine($"Region parameter: {(region.HasValue ? region.Value.ToString() : "null")}");
                
                var settings = _settingsService.CurrentSettings;
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);
                
                System.Diagnostics.Debug.WriteLine($"Recording engine: {settings.RecordingEngine}");

                // Choose recording engine based on settings
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        await StartOBSRecordingAsync(region);
                        break;
                    case "FFmpeg":
                    default:
                        await StartFFmpegRecordingAsync(region);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
            }
        }

        private async Task StartOBSRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            if (_obsRecordingService == null)
            {
                throw new Exception("OBS recording service is not available. Please ensure OBS is installed.");
            }

            try
            {
                LogToFile("Starting OBS recording...");
                await _obsRecordingService.StartRecordingAsync(region);
                
                _isRecording = true;
                RecordingStateChanged?.Invoke(this, true);
                
                LogToFile("OBS recording started successfully");
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting OBS recording: {ex.Message}");
                throw;
            }
        }



        private static readonly Guid KsSubTypeIeeeFloat = new("00000003-0000-0010-8000-00aa00389b71");

        private async Task StartFFmpegRecordingAsync(System.Drawing.Rectangle? region = null)
        {
            var settings = _settingsService.CurrentSettings;
            var wantAudio = !string.IsNullOrWhiteSpace(settings.SelectedOutputAudioDevice)
                && !string.Equals(settings.SelectedOutputAudioDevice.Trim(), "No system audio", StringComparison.OrdinalIgnoreCase);

            var ffmpegExe = GetFFmpegPath();
            var videoInputArgs = BuildVideoInputArgs(region, settings, wantAudio ? 2048 : 512);

            if (!wantAudio)
            {
                DisposeLoopbackPipeAudio();
                var args = ComposeFfmpegArguments(videoInputArgs, null);
                StartFfmpegProcess(args, ffmpegExe);
                return;
            }

            var mm = NaudioLoopbackDeviceResolver.TryResolve(settings.SelectedOutputAudioDevice, out var resolveReason);
            if (mm == null)
                throw new InvalidOperationException(resolveReason ?? "No playback device available for system audio.");

            WasapiLoopbackCapture capture;
            try
            {
                capture = new WasapiLoopbackCapture(mm);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Could not open WASAPI loopback: {ex.Message}", ex);
            }

            string fmt;
            int ch, rate;
            try
            {
                (fmt, ch, rate) = GetFfmpegRawLayout(capture.WaveFormat);
            }
            catch (NotSupportedException ns)
            {
                capture.Dispose();
                throw new InvalidOperationException(ns.Message, ns);
            }

            var pipeShortName = $"SharpShotAud_{Guid.NewGuid():N}";
            var pipePath = @"\\.\pipe\" + pipeShortName;
            var audioClause = $" -thread_queue_size 4096 -f {fmt} -ac {ch} -ar {rate} -i {pipePath}";
            var ffmpegArgs = ComposeFfmpegArguments(videoInputArgs, audioClause);

            NamedPipeServerStream? pipe = null;
            try
            {
                const int pipeOutBufferBytes = 1024 * 1024;
                pipe = new NamedPipeServerStream(
                    pipeShortName,
                    PipeDirection.Out,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    inBufferSize: 0,
                    outBufferSize: pipeOutBufferBytes);
                _audioPipe = pipe;

                using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var connectTask = pipe.WaitForConnectionAsync(connectCts.Token);

                StartFfmpegProcess(ffmpegArgs, ffmpegExe, deferRecordingState: true);

                await connectTask.ConfigureAwait(true);

                var audioChannel = Channel.CreateBounded<PooledAudioChunk>(new BoundedChannelOptions(512)
                {
                    SingleReader = true,
                    SingleWriter = false,
                    FullMode = BoundedChannelFullMode.DropOldest
                });
                _audioChunkWriter = audioChannel.Writer;
                var audioReader = audioChannel.Reader;
                _audioPipePumpTask = Task.Run(() => RunAudioPipePumpAsync(pipe, audioReader));

                _loopbackCapture = capture;
                capture.DataAvailable += OnLoopbackDataAvailable;
                capture.RecordingStopped += OnLoopbackRecordingStopped;
                capture.StartRecording();

                System.Diagnostics.Debug.WriteLine($"FFmpeg system audio: NAudio loopback → pipe ({fmt}, {ch} ch, {rate} Hz) for '{settings.SelectedOutputAudioDevice}'");
            }
            catch
            {
                DisposeLoopbackPipeAudio();
                TryKillAndDisposeFfmpeg();
                capture.Dispose();
                throw;
            }

            _isRecording = true;
            RecordingStateChanged?.Invoke(this, true);
        }

        private void OnLoopbackRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (e.Exception != null)
                System.Diagnostics.Debug.WriteLine($"Loopback capture stopped with error: {e.Exception.Message}");
        }

        private void OnLoopbackDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (e.BytesRecorded <= 0)
                return;

            var writer = _audioChunkWriter;
            if (writer == null)
                return;

            var n = e.BytesRecorded;
            var buf = ArrayPool<byte>.Shared.Rent(n);
            Buffer.BlockCopy(e.Buffer, 0, buf, 0, n);
            if (!writer.TryWrite(new PooledAudioChunk(buf, n)))
                ArrayPool<byte>.Shared.Return(buf);
        }

        /// <summary>Writes pooled chunks to the pipe on a thread pool thread so NAudio's capture callback never blocks on FFmpeg.</summary>
        private static async Task RunAudioPipePumpAsync(NamedPipeServerStream pipe, ChannelReader<PooledAudioChunk> reader)
        {
            try
            {
                await foreach (var chunk in reader.ReadAllAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    try
                    {
                        if (pipe.IsConnected)
                            await pipe.WriteAsync(chunk.Buffer.AsMemory(0, chunk.Length)).ConfigureAwait(false);
                    }
                    catch (IOException)
                    {
                        // FFmpeg exited or pipe closed
                    }
                    catch (ObjectDisposedException)
                    {
                        // ignore
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(chunk.Buffer);
                    }
                }
            }
            finally
            {
                while (reader.TryRead(out var leftover))
                    ArrayPool<byte>.Shared.Return(leftover.Buffer);
            }
        }

        private void DisposeLoopbackPipeAudio()
        {
            if (_loopbackCapture != null)
            {
                try
                {
                    _loopbackCapture.DataAvailable -= OnLoopbackDataAvailable;
                    _loopbackCapture.RecordingStopped -= OnLoopbackRecordingStopped;
                }
                catch
                {
                    // ignore
                }

                try
                {
                    _loopbackCapture.StopRecording();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    _loopbackCapture.Dispose();
                }
                catch
                {
                    // ignore
                }

                _loopbackCapture = null;
            }

            try
            {
                _audioChunkWriter?.TryComplete();
            }
            catch
            {
                // ignore
            }

            _audioChunkWriter = null;

            try
            {
                _audioPipePumpTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // ignore
            }

            _audioPipePumpTask = null;

            try
            {
                _audioPipe?.Dispose();
            }
            catch
            {
                // ignore
            }

            _audioPipe = null;
        }

        private void TryKillAndDisposeFfmpeg()
        {
            try
            {
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                    _ffmpegProcess.Kill(entireProcessTree: true);
            }
            catch
            {
                // ignore
            }

            try
            {
                _ffmpegProcess?.Dispose();
            }
            catch
            {
                // ignore
            }

            _ffmpegProcess = null;
        }

        /// <summary>Maps NAudio loopback <see cref="WaveFormat"/> to FFmpeg raw demuxer flags.</summary>
        private static (string format, int channels, int rate) GetFfmpegRawLayout(WaveFormat wf)
        {
            var channels = wf.Channels;
            var rate = wf.SampleRate;

            if (wf.Encoding == WaveFormatEncoding.Pcm && wf.BitsPerSample == 16)
                return ("s16le", channels, rate);

            if (wf is WaveFormatExtensible ext && ext.SubFormat == KsSubTypeIeeeFloat && wf.BitsPerSample == 32)
                return ("f32le", channels, rate);

            if (wf.Encoding == WaveFormatEncoding.IeeeFloat && wf.BitsPerSample == 32)
                return ("f32le", channels, rate);

            throw new NotSupportedException(
                $"This output device's loopback format is not supported ({wf.Encoding}, {wf.BitsPerSample} bit, {channels} ch, {rate} Hz). Try another playback device or \"No system audio\".");
        }

        private void StartFfmpegProcess(string ffmpegArgs, string ffmpegExe, bool deferRecordingState = false)
        {
            _ffmpegProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = ffmpegExe,
                    Arguments = ffmpegArgs,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            var directory = Path.GetDirectoryName(_currentRecordingPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            _ffmpegProcess.Start();

            try
            {
                _ffmpegProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not raise FFmpeg process priority: {ex.Message}");
            }

            if (!deferRecordingState)
            {
                _isRecording = true;
                RecordingStateChanged?.Invoke(this, true);
            }

            System.Diagnostics.Debug.WriteLine($"Started FFmpeg recording to: {_currentRecordingPath}");
            System.Diagnostics.Debug.WriteLine($"FFmpeg command: {ffmpegArgs}");

            AttachFfmpegStderrLogging(_ffmpegProcess);
        }

        private static void AttachFfmpegStderrLogging(Process process)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!process.StandardError.EndOfStream)
                    {
                        var line = await process.StandardError.ReadLineAsync().ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(line))
                            System.Diagnostics.Debug.WriteLine($"FFmpeg: {line}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error reading FFmpeg stderr: {ex.Message}");
                }
            });
        }

        private string ComposeFfmpegArguments(string videoInputArgs, string? rawAudioInputClause)
        {
            var vPreset = string.IsNullOrEmpty(rawAudioInputClause) ? "fast" : "veryfast";
            var outputArgs = $"-c:v libx264 -preset {vPreset} -crf 20 -pix_fmt yuv420p -profile:v baseline -level 3.0";
            outputArgs += " -g 60 -keyint_min 30";
            if (!string.IsNullOrEmpty(rawAudioInputClause))
                outputArgs += " -map 0:v:0 -map 1:0 -c:a aac -b:a 192k";
            outputArgs += " -movflags +faststart -f mp4 -strict experimental";

            if (_currentRecordingPath == null)
                throw new InvalidOperationException("Recording path is not set.");
            var outPath = _currentRecordingPath.Replace("\"", "\"\"");
            outputArgs += $" \"{outPath}\"";

            return $"{videoInputArgs}{rawAudioInputClause ?? ""} {outputArgs}";
        }

        private string BuildVideoInputArgs(System.Drawing.Rectangle? region, Settings settings, int gdigrabThreadQueueSize = 512)
        {
            string inputArgs;
            var tq = gdigrabThreadQueueSize;

            if (region.HasValue && region.Value != System.Drawing.Rectangle.Empty)
            {
                var bounds = region.Value;

                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine($"Invalid region bounds: {bounds}, falling back to full screen");
                    var screenBounds = GetBoundsForSelectedScreen();
                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {screenBounds.X} -offset_y {screenBounds.Y} -video_size {screenBounds.Width}x{screenBounds.Height} -i desktop -probesize 10M -thread_queue_size {tq}";
                }
                else
                {
                    var adjustedWidth = bounds.Width % 2 == 0 ? bounds.Width : bounds.Width - 1;
                    var adjustedHeight = bounds.Height % 2 == 0 ? bounds.Height : bounds.Height - 1;

                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {bounds.X} -offset_y {bounds.Y} -video_size {adjustedWidth}x{adjustedHeight} -i desktop -probesize 10M -thread_queue_size {tq}";
                    System.Diagnostics.Debug.WriteLine($"Region recording: {bounds.X},{bounds.Y} {adjustedWidth}x{adjustedHeight} (original: {bounds.Width}x{bounds.Height})");
                }
            }
            else
            {
                var screenBounds = GetBoundsForSelectedScreen();

                if (settings.SelectedScreen == "All Screens" || settings.SelectedScreen == "All Monitors")
                {
                    inputArgs = $"-f gdigrab -framerate 30 -i desktop -probesize 10M -thread_queue_size {tq}";
                    System.Diagnostics.Debug.WriteLine("Recording all screens - no offset/size specified");
                }
                else
                {
                    inputArgs = $"-f gdigrab -framerate 30 -offset_x {screenBounds.X} -offset_y {screenBounds.Y} -video_size {screenBounds.Width}x{screenBounds.Height} -i desktop -probesize 10M -thread_queue_size {tq}";
                    System.Diagnostics.Debug.WriteLine($"Recording specific screen with offset: {screenBounds.X},{screenBounds.Y} size: {screenBounds.Width}x{screenBounds.Height}");
                }
            }

            return inputArgs;
        }

        private System.Drawing.Rectangle GetBoundsForSelectedScreen()
        {
            var selectedScreen = _settingsService.CurrentSettings.SelectedScreen;
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            
            System.Diagnostics.Debug.WriteLine("=== GetBoundsForSelectedScreen Debug ===");
            System.Diagnostics.Debug.WriteLine($"Selected screen from settings: '{selectedScreen}'");
            System.Diagnostics.Debug.WriteLine($"Total screens detected: {allScreens.Length}");
            
            for (int i = 0; i < allScreens.Length; i++)
            {
                var screen = allScreens[i];
                System.Diagnostics.Debug.WriteLine($"Screen {i + 1}: Bounds={screen.Bounds}, Primary={screen.Primary}, DeviceName={screen.DeviceName}");
            }
            
            if (allScreens.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("No screens detected, using fallback");
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    System.Diagnostics.Debug.WriteLine("No primary screen, using default 1920x1080");
                    return new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                System.Diagnostics.Debug.WriteLine($"Using primary screen fallback: {primaryScreen.Bounds}");
                return primaryScreen.Bounds;
            }
            
            System.Drawing.Rectangle result;
            
            switch (selectedScreen)
            {
                case "All Screens":
                case "All Monitors": // Backward compatibility
                    System.Diagnostics.Debug.WriteLine("Using All Screens mode");
                    result = GetVirtualDesktopBounds();
                    break;
                    
                case "Primary Monitor":
                    System.Diagnostics.Debug.WriteLine("Using Primary Monitor mode");
                    var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                    if (primaryScreen == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Primary screen is null, using default");
                        result = new System.Drawing.Rectangle(0, 0, 1920, 1080);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Primary screen bounds: {primaryScreen.Bounds}");
                        result = primaryScreen.Bounds;
                    }
                    break;
                    
                default:
                    System.Diagnostics.Debug.WriteLine($"Checking if '{selectedScreen}' is a monitor number");
                    if (selectedScreen.StartsWith("Monitor "))
                    {
                        var monitorNumber = selectedScreen.Replace("Monitor ", "").Replace(" (Primary)", "");
                        System.Diagnostics.Debug.WriteLine($"Extracted monitor number: '{monitorNumber}'");
                        if (int.TryParse(monitorNumber, out int index) && index > 0 && index <= allScreens.Length)
                        {
                            var targetScreen = allScreens[index - 1];
                            System.Diagnostics.Debug.WriteLine($"Using Monitor {index} with bounds: {targetScreen.Bounds}");
                            result = targetScreen.Bounds;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid monitor number, using virtual desktop");
                            result = GetVirtualDesktopBounds();
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Unknown screen selection '{selectedScreen}', using virtual desktop");
                        result = GetVirtualDesktopBounds();
                    }
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($"Final screen bounds: {result}");
            System.Diagnostics.Debug.WriteLine("=== End GetBoundsForSelectedScreen Debug ===");
            return result;
        }

        private System.Drawing.Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = System.Windows.Forms.Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;
                if (primaryScreen == null)
                {
                    return new System.Drawing.Rectangle(0, 0, 1920, 1080);
                }
                return primaryScreen.Bounds;
            }

            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            foreach (var screen in allScreens)
            {
                minX = Math.Min(minX, screen.Bounds.X);
                minY = Math.Min(minY, screen.Bounds.Y);
                maxX = Math.Max(maxX, screen.Bounds.X + screen.Bounds.Width);
                maxY = Math.Max(maxY, screen.Bounds.Y + screen.Bounds.Height);
            }

            return new System.Drawing.Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private string GetFFmpegPath()
        {
            // Look for FFmpeg in common locations
            var possiblePaths = new[]
            {
                "ffmpeg.exe",
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "bin", "ffmpeg.exe"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe")
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            return "ffmpeg.exe"; // Assume it's in PATH
        }

        public async Task StopRecordingAsync()
        {
            if (!_isRecording)
            {
                System.Diagnostics.Debug.WriteLine("No recording in progress");
                return;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        if (_obsRecordingService != null)
                        {
                            await _obsRecordingService.StopRecordingAsync();
                        }
                        break;
                    case "FFmpeg":
                        // End NAudio loopback and close the pipe so FFmpeg sees audio EOF, then tell FFmpeg to quit gdigrab.
                        DisposeLoopbackPipeAudio();

                        if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                        {
                            try
                            {
                                // Try to gracefully stop FFmpeg by sending 'q' command
                                _ffmpegProcess.StandardInput.WriteLine("q");
                                _ffmpegProcess.StandardInput.Flush();
                                _ffmpegProcess.StandardInput.Close(); // Close input to signal end
                                
                                // Wait longer for graceful shutdown and file finalization
                                if (!_ffmpegProcess.WaitForExit(5000))
                                {
                                    // If graceful shutdown failed, force kill
                                    _ffmpegProcess.Kill();
                                }
                                
                                System.Diagnostics.Debug.WriteLine("Stopped FFmpeg recording");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error stopping FFmpeg gracefully, force killing: {ex.Message}");
                                try
                                {
                                    _ffmpegProcess.Kill();
                                }
                                catch (Exception killEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error force killing FFmpeg: {killEx.Message}");
                                }
                            }
                            finally
                            {
                                _ffmpegProcess.Dispose();
                                _ffmpegProcess = null;
                            }
                        }
                        break;
                    default:
                        // Default to FFmpeg stop logic (already handled above)
                        break;
                }
                
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
                System.Diagnostics.Debug.WriteLine("Recording stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping recording: {ex.Message}");
            }
        }



        // OBS event handlers
        private void OnOBSRecordingStateChanged(object? sender, bool isRecording)
        {
            _isRecording = isRecording;
            RecordingStateChanged?.Invoke(this, isRecording);
        }

        public bool IsRecording => _isRecording;

        // Methods that MainWindow expects
        public async Task StartRecording(System.Drawing.Rectangle? region = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"=== StartRecordingAsync called ===");
                System.Diagnostics.Debug.WriteLine($"Region parameter: {(region.HasValue ? region.Value.ToString() : "null")}");
                
                var settings = _settingsService.CurrentSettings;
                System.Diagnostics.Debug.WriteLine($"Selected screen from settings: '{settings.SelectedScreen}'");
                
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                var fileName = $"recording_{timestamp}.mp4";
                _currentRecordingPath = Path.Combine(settings.SavePath, fileName);
                
                System.Diagnostics.Debug.WriteLine($"Recording engine: {settings.RecordingEngine}");

                // Choose recording engine based on settings
                switch (settings.RecordingEngine)
                {
                    case "OBS":
                        await StartOBSRecordingAsync(region);
                        break;
                    case "FFmpeg":
                    default:
                        await StartFFmpegRecordingAsync(region);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting recording: {ex.Message}");
                _isRecording = false;
                RecordingStateChanged?.Invoke(this, false);
            }
        }

        public async Task StopRecording()
        {
            await StopRecordingAsync();
        }

        public string? GetCurrentRecordingPath()
        {
            return _currentRecordingPath;
        }

        // Cleanup method for OBS
        public async Task DisconnectOBSAsync()
        {
            if (_obsRecordingService != null)
            {
                await _obsRecordingService.DisconnectAsync();
            }
        }

        // Method to setup OBS for recording (used by the OBS button)
        public async Task<bool> SetupOBSForRecordingAsync()
        {
            if (_obsRecordingService != null)
            {
                return await _obsRecordingService.SetupOBSForRecordingAsync();
            }
            return false;
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "audio_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] [RecordingService] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 