using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpShot.Models;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace SharpShot.Services
{
    public class RecordingService
    {
        private readonly SettingsService _settingsService;
        private bool _isRecording;
        private bool _isPaused;
        private DateTime _recordingStartTime;
        private string _currentRecordingPath = string.Empty;
        private CancellationTokenSource? _recordingCancellationToken;
        private Process? _ffmpegProcess;

        public RecordingService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _isRecording = false;
            _isPaused = false;
        }

        public bool IsRecording => _isRecording;
        public bool IsPaused => _isPaused;

        public TimeSpan RecordingDuration => _isRecording ? DateTime.Now - _recordingStartTime : TimeSpan.Zero;

        public event Action<bool>? RecordingStateChanged;
        public event Action<TimeSpan>? RecordingTimeUpdated;

        public async Task StartRecording(Rectangle? region = null)
        {
            if (_isRecording) return;

            try
            {
                _currentRecordingPath = GenerateRecordingPath();
                _recordingStartTime = DateTime.Now;
                _isRecording = true;
                _isPaused = false;
                _recordingCancellationToken = new CancellationTokenSource();

                // Start FFmpeg recording
                await StartFFmpegRecording(region);

                RecordingStateChanged?.Invoke(true);
                StartRecordingTimer();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start recording: {ex.Message}");
                _isRecording = false;
                throw;
            }
        }

        public async Task StopRecording()
        {
            if (!_isRecording) return;

            try
            {
                LogToFile("Stopping recording...");
                Console.WriteLine("Stopping recording...");
                
                // Stop FFmpeg process with enhanced error handling
                if (_ffmpegProcess != null && !_ffmpegProcess.HasExited)
                {
                    LogToFile("Sending quit command to FFmpeg...");
                    Console.WriteLine("Sending quit command to FFmpeg...");
                    _ffmpegProcess.StandardInput.WriteLine("q");
                    _ffmpegProcess.StandardInput.Flush();
                    
                    // Wait for FFmpeg to finish with shorter timeout for better responsiveness
                    LogToFile("Waiting for FFmpeg to finish...");
                    Console.WriteLine("Waiting for FFmpeg to finish...");
                    var exitTask = _ffmpegProcess.WaitForExitAsync();
                    var timeoutTask = Task.Delay(3000); // Reduced to 3 seconds for faster response
                    
                    var completedTask = await Task.WhenAny(exitTask, timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        LogToFile("FFmpeg didn't exit gracefully, killing process...");
                        Console.WriteLine("FFmpeg didn't exit gracefully, killing process...");
                        _ffmpegProcess.Kill();
                        await _ffmpegProcess.WaitForExitAsync();
                    }
                    
                    LogToFile($"FFmpeg finished with exit code: {_ffmpegProcess.ExitCode}");
                    Console.WriteLine($"FFmpeg finished with exit code: {_ffmpegProcess.ExitCode}");
                    
                    // Don't read StandardError synchronously since we're using BeginErrorReadLine
                    // The error output is already being captured asynchronously
                    
                    // Check exit code
                    if (_ffmpegProcess.ExitCode != 0)
                    {
                        LogToFile($"Warning: FFmpeg exited with non-zero code: {_ffmpegProcess.ExitCode}");
                        Console.WriteLine($"Warning: FFmpeg exited with non-zero code: {_ffmpegProcess.ExitCode}");
                    }
                }

                _isRecording = false;
                _isPaused = false;
                RecordingStateChanged?.Invoke(false);
                
                LogToFile($"Recording stopped. File: {_currentRecordingPath}");
                Console.WriteLine($"Recording stopped. File: {_currentRecordingPath}");
                
                // Enhanced file validation
                ValidateCreatedFile();
            }
            catch (Exception ex)
            {
                var errorMsg = $"Failed to stop recording: {ex.Message}";
                LogToFile(errorMsg);
                Console.WriteLine(errorMsg);
                _isRecording = false;
                _isPaused = false;
                RecordingStateChanged?.Invoke(false);
                throw;
            }
        }

        public async Task PauseRecording()
        {
            if (!_isRecording || _isPaused) return;

            try
            {
                // Note: gdigrab doesn't support pause/resume well, so we'll just track the state
                _isPaused = true;
                System.Diagnostics.Debug.WriteLine($"Paused recording: {_currentRecordingPath}");
                await Task.CompletedTask; // Add await to satisfy async requirement
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to pause recording: {ex.Message}");
                throw;
            }
        }

        public async Task ResumeRecording()
        {
            if (!_isRecording || !_isPaused) return;

            try
            {
                // Note: gdigrab doesn't support pause/resume well, so we'll just track the state
                _isPaused = false;
                System.Diagnostics.Debug.WriteLine($"Resumed recording: {_currentRecordingPath}");
                await Task.CompletedTask; // Add await to satisfy async requirement
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to resume recording: {ex.Message}");
                throw;
            }
        }

        public async Task ToggleRecording(Rectangle? region = null)
        {
            if (_isRecording)
            {
                await StopRecording();
            }
            else
            {
                await StartRecording(region);
            }
        }

        private async Task StartFFmpegRecording(Rectangle? region)
        {
            try
            {
                var bounds = region ?? GetBoundsForSelectedScreen();
                
                // Log to both console and file
                var logMessage = $"=== ENHANCED FFmpeg Recording ===\nRecording bounds: {bounds}\nOutput path: {_currentRecordingPath}";
                Console.WriteLine(logMessage);
                LogToFile(logMessage);
                
                // Get FFmpeg path
                var ffmpegPath = GetBundledFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath))
                {
                    throw new Exception("FFmpeg not found!");
                }
                
                LogToFile($"FFmpeg path: {ffmpegPath}");
                Console.WriteLine($"FFmpeg path: {ffmpegPath}");
                
                // Test FFmpeg capabilities first
                TestFFmpegCapabilities(ffmpegPath);
                
                // Build enhanced FFmpeg command with proper video size and encoding
                var ffmpegArgs = BuildEnhancedFFmpegCommand(bounds);
                
                LogToFile($"FFmpeg command: {ffmpegPath} {ffmpegArgs}");
                Console.WriteLine($"FFmpeg command: {ffmpegPath} {ffmpegArgs}");
                
                // Start FFmpeg process with enhanced error handling
                _ffmpegProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = ffmpegArgs,
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                // Set up error data received handler
                _ffmpegProcess.ErrorDataReceived += (s, e) => 
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        var errorMsg = $"FFmpeg Error: {e.Data}";
                        Console.WriteLine(errorMsg);
                        LogToFile(errorMsg);
                    }
                };

                LogToFile("Starting FFmpeg...");
                Console.WriteLine("Starting FFmpeg...");
                _ffmpegProcess.Start();
                
                // Begin reading error output asynchronously
                _ffmpegProcess.BeginErrorReadLine();
                
                // Add a small delay to let UI settle before checking FFmpeg
                LogToFile("Waiting for UI to settle...");
                Console.WriteLine("Waiting for UI to settle...");
                await Task.Delay(1000); // 1 second delay for UI to settle
                
                // Wait shorter before checking if it exited (2 seconds)
                LogToFile("Waiting 2 seconds to check if FFmpeg started properly...");
                Console.WriteLine("Waiting 2 seconds to check if FFmpeg started properly...");
                await Task.Delay(2000);
                
                if (_ffmpegProcess.HasExited)
                {
                    var errorOutput = _ffmpegProcess.StandardError.ReadToEnd();
                    var errorMsg = $"FFmpeg failed to start!\nExit code: {_ffmpegProcess.ExitCode}\nError output: {errorOutput}";
                    LogToFile(errorMsg);
                    Console.WriteLine(errorMsg);
                    throw new Exception($"FFmpeg failed to start. Exit code: {_ffmpegProcess.ExitCode}. Error: {errorOutput}");
                }
                
                LogToFile("FFmpeg started successfully!");
                Console.WriteLine("FFmpeg started successfully!");
                LogToFile($"Process ID: {_ffmpegProcess.Id}");
                Console.WriteLine($"Process ID: {_ffmpegProcess.Id}");
                LogToFile($"Process running: {!_ffmpegProcess.HasExited}");
                Console.WriteLine($"Process running: {!_ffmpegProcess.HasExited}");
            }
            catch (Exception ex)
            {
                var errorMsg = $"Recording failed: {ex.Message}";
                LogToFile(errorMsg);
                Console.WriteLine(errorMsg);
                throw;
            }
        }
        
        private void TestFFmpegCapabilities(string ffmpegPath)
        {
            try
            {
                Console.WriteLine("Testing FFmpeg capabilities...");
                
                // Test encoders
                var encoderProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-encoders",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                encoderProcess.Start();
                var encoderOutput = encoderProcess.StandardOutput.ReadToEnd();
                encoderProcess.WaitForExit();
                
                if (encoderOutput.Contains("libx264"))
                {
                    Console.WriteLine("✅ libx264 encoder found");
                }
                else
                {
                    Console.WriteLine("❌ libx264 encoder NOT found!");
                    throw new Exception("libx264 encoder not available in FFmpeg");
                }
                
                // Test muxers
                var muxerProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-muxers",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                muxerProcess.Start();
                var muxerOutput = muxerProcess.StandardOutput.ReadToEnd();
                muxerProcess.WaitForExit();
                
                if (muxerOutput.Contains("mp4"))
                {
                    Console.WriteLine("✅ MP4 muxer found");
                }
                else
                {
                    Console.WriteLine("❌ MP4 muxer NOT found!");
                    throw new Exception("MP4 muxer not available in FFmpeg");
                }
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg capability test failed: {ex.Message}");
                throw;
            }
        }
        
        private Rectangle GetBoundsForSelectedScreen()
        {
            var selectedScreen = _settingsService.CurrentSettings.SelectedScreen;
            var allScreens = Screen.AllScreens;
            
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                return Screen.PrimaryScreen.Bounds;
            }
            
            // Handle different screen selection options
            switch (selectedScreen)
            {
                case "Primary Monitor":
                    return Screen.PrimaryScreen.Bounds;
                    
                default:
                    // Check if it's a specific monitor (e.g., "Monitor 1", "Monitor 2", etc.)
                    if (selectedScreen.StartsWith("Monitor "))
                    {
                        var monitorNumber = selectedScreen.Replace("Monitor ", "").Replace(" (Primary)", "");
                        if (int.TryParse(monitorNumber, out int index) && index > 0 && index <= allScreens.Length)
                        {
                            return allScreens[index - 1].Bounds;
                        }
                    }
                    
                    // Fallback to primary screen
                    return Screen.PrimaryScreen.Bounds;
            }
        }

        private Rectangle GetVirtualDesktopBounds()
        {
            var allScreens = Screen.AllScreens;
            if (allScreens.Length == 0)
            {
                // Fallback to primary screen if no screens detected
                return Screen.PrimaryScreen.Bounds;
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

            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        private string BuildEnhancedFFmpegCommand(Rectangle bounds)
        {
            var virtualBounds = GetVirtualDesktopBounds();
            var isFullScreen = bounds.X == virtualBounds.X && bounds.Y == virtualBounds.Y && 
                              bounds.Width == virtualBounds.Width && 
                              bounds.Height == virtualBounds.Height;

            var command = $"-f gdigrab -framerate 30";

            if (!isFullScreen)
            {
                command += $" -offset_x {bounds.X} -offset_y {bounds.Y}";
            }

            // Ensure dimensions are divisible by 2 for H.264 encoding
            var width = bounds.Width % 2 == 0 ? bounds.Width : bounds.Width - 1;
            var height = bounds.Height % 2 == 0 ? bounds.Height : bounds.Height - 1;

            // Log if dimensions were adjusted
            if (width != bounds.Width || height != bounds.Height)
            {
                var logMsg = $"Adjusted dimensions from {bounds.Width}x{bounds.Height} to {width}x{height} for H.264 compatibility";
                LogToFile(logMsg);
                Console.WriteLine(logMsg);
            }

            command += $" -video_size {width}x{height} -i desktop -c:v libx264 -preset ultrafast -crf 23 -pix_fmt yuv420p -y \"{_currentRecordingPath}\"";

            return command;
        }
        
        private bool IsDirectShowAvailable()
        {
            return false; // Always use gdigrab for simplicity
        }
        
        private void TestFFmpegVersion(string ffmpegPath)
        {
            // Simple test - just check if FFmpeg runs
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-version",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                process.WaitForExit();
                Console.WriteLine($"FFmpeg version test: Exit code {process.ExitCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg version test failed: {ex.Message}");
            }
        }
        
        private void TestFFmpegCommand(string ffmpegPath, Rectangle bounds)
        {
            // Simple test - just check if gdigrab is available
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = "-f gdigrab -list_devices true -i dummy",
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                Console.WriteLine($"gdigrab test: Exit code {process.ExitCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"gdigrab test failed: {ex.Message}");
            }
        }
        
        private bool IsDirectoryWritable(string? directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;
                
            try
            {
                var testFile = Path.Combine(directory, "test_write.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private string? GetBundledFFmpegPath()
        {
            System.Diagnostics.Debug.WriteLine("=== FFmpeg Detection Debug ===");
            
            // First, try Windows "where" command (for Windows host)
            try
            {
                System.Diagnostics.Debug.WriteLine("Trying 'where ffmpeg' command...");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "ffmpeg",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                System.Diagnostics.Debug.WriteLine($"'where' command exit code: {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"'where' output: {output}");
                System.Diagnostics.Debug.WriteLine($"'where' error: {error}");
                
                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var windowsFfmpegPath = lines[0].Trim();
                        System.Diagnostics.Debug.WriteLine($"Found Windows FFmpeg at: {windowsFfmpegPath}");
                        if (File.Exists(windowsFfmpegPath))
                        {
                            System.Diagnostics.Debug.WriteLine("Windows FFmpeg file exists!");
                            return windowsFfmpegPath;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("Windows FFmpeg file does not exist!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"'where' command failed: {ex.Message}");
            }
            
            // Try system FFmpeg (for Docker environments)
            try
            {
                System.Diagnostics.Debug.WriteLine("Trying 'which ffmpeg' command...");
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = "ffmpeg",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                System.Diagnostics.Debug.WriteLine($"'which' command exit code: {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"'which' output: {output}");
                System.Diagnostics.Debug.WriteLine($"'which' error: {error}");
                
                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 0)
                    {
                        var systemFfmpegPath = lines[0].Trim();
                        System.Diagnostics.Debug.WriteLine($"Found system FFmpeg at: {systemFfmpegPath}");
                        if (File.Exists(systemFfmpegPath))
                        {
                            System.Diagnostics.Debug.WriteLine("System FFmpeg file exists!");
                            return systemFfmpegPath;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("System FFmpeg file does not exist!");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"'which' command failed: {ex.Message}");
            }
            
            // Look for FFmpeg in the application directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            System.Diagnostics.Debug.WriteLine($"Application directory: {appDir}");
            
            var bundledFfmpegPath = Path.Combine(appDir, "ffmpeg", "bin", "ffmpeg.exe");
            System.Diagnostics.Debug.WriteLine($"Checking bundled FFmpeg at: {bundledFfmpegPath}");
            
            if (File.Exists(bundledFfmpegPath))
            {
                System.Diagnostics.Debug.WriteLine("Bundled FFmpeg found!");
                return bundledFfmpegPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Bundled FFmpeg not found!");
            }
            
            // Fallback: look for ffmpeg.exe directly in app directory
            var directFfmpegPath = Path.Combine(appDir, "ffmpeg.exe");
            System.Diagnostics.Debug.WriteLine($"Checking direct FFmpeg at: {directFfmpegPath}");
            
            if (File.Exists(directFfmpegPath))
            {
                System.Diagnostics.Debug.WriteLine("Direct FFmpeg found!");
                return directFfmpegPath;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("Direct FFmpeg not found!");
            }
            
            System.Diagnostics.Debug.WriteLine("No FFmpeg found in any location!");
            return null;
        }
        
        private void AnalyzeCreatedFile()
        {
            try
            {
                Console.WriteLine("=== File Analysis ===");
                System.Diagnostics.Debug.WriteLine("=== File Analysis ===");
                Console.WriteLine($"Checking file: {_currentRecordingPath}");
                System.Diagnostics.Debug.WriteLine($"Checking file: {_currentRecordingPath}");
                
                if (File.Exists(_currentRecordingPath))
                {
                    var fileInfo = new FileInfo(_currentRecordingPath);
                    Console.WriteLine($"File exists: Yes");
                    System.Diagnostics.Debug.WriteLine($"File exists: Yes");
                    Console.WriteLine($"File size: {fileInfo.Length} bytes");
                    System.Diagnostics.Debug.WriteLine($"File size: {fileInfo.Length} bytes");
                    Console.WriteLine($"File size (MB): {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
                    System.Diagnostics.Debug.WriteLine($"File size (MB): {fileInfo.Length / (1024.0 * 1024.0):F2} MB");
                    Console.WriteLine($"File created: {fileInfo.CreationTime}");
                    System.Diagnostics.Debug.WriteLine($"File created: {fileInfo.CreationTime}");
                    Console.WriteLine($"File modified: {fileInfo.LastWriteTime}");
                    System.Diagnostics.Debug.WriteLine($"File modified: {fileInfo.LastWriteTime}");
                    
                    // Check if file is empty or very small (likely corrupted)
                    if (fileInfo.Length < 1024)
                    {
                        Console.WriteLine("WARNING: File is very small (< 1KB), likely corrupted!");
                        System.Diagnostics.Debug.WriteLine("WARNING: File is very small (< 1KB), likely corrupted!");
                    }
                    else if (fileInfo.Length < 1024 * 1024)
                    {
                        Console.WriteLine("WARNING: File is small (< 1MB), may be incomplete!");
                        System.Diagnostics.Debug.WriteLine("WARNING: File is small (< 1MB), may be incomplete!");
                    }
                    else
                    {
                        Console.WriteLine("File size looks reasonable");
                        System.Diagnostics.Debug.WriteLine("File size looks reasonable");
                    }
                    
                    // Try to analyze with FFmpeg
                    _ = Task.Run(() => AnalyzeFileWithFFmpeg(_currentRecordingPath));
                }
                else
                {
                    Console.WriteLine("ERROR: File does not exist!");
                    System.Diagnostics.Debug.WriteLine("ERROR: File does not exist!");
                    
                    // Check if any files were created in the directory
                    var outputDir = Path.GetDirectoryName(_currentRecordingPath);
                    if (Directory.Exists(outputDir))
                    {
                        var files = Directory.GetFiles(outputDir, "*.mp4");
                        Console.WriteLine($"Found {files.Length} MP4 files in output directory:");
                        System.Diagnostics.Debug.WriteLine($"Found {files.Length} MP4 files in output directory:");
                        foreach (var file in files)
                        {
                            var info = new FileInfo(file);
                            Console.WriteLine($"  - {Path.GetFileName(file)} ({info.Length} bytes)");
                            System.Diagnostics.Debug.WriteLine($"  - {Path.GetFileName(file)} ({info.Length} bytes)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File analysis failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"File analysis failed: {ex.Message}");
            }
        }
        
        private void AnalyzeFileWithFFmpeg(string filePath)
        {
            try
            {
                var ffmpegPath = GetBundledFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath)) 
                {
                    Console.WriteLine("Cannot analyze file - FFmpeg not found");
                    System.Diagnostics.Debug.WriteLine("Cannot analyze file - FFmpeg not found");
                    return;
                }
                
                Console.WriteLine("Analyzing file with FFmpeg...");
                System.Diagnostics.Debug.WriteLine("Analyzing file with FFmpeg...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{filePath}\" -f null -",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                Console.WriteLine($"FFmpeg analysis exit code: {process.ExitCode}");
                System.Diagnostics.Debug.WriteLine($"FFmpeg analysis exit code: {process.ExitCode}");
                Console.WriteLine($"FFmpeg analysis output: {output}");
                System.Diagnostics.Debug.WriteLine($"FFmpeg analysis output: {output}");
                Console.WriteLine($"FFmpeg analysis error: {error}");
                System.Diagnostics.Debug.WriteLine($"FFmpeg analysis error: {error}");
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("File appears to be valid MP4!");
                    System.Diagnostics.Debug.WriteLine("File appears to be valid MP4!");
                }
                else
                {
                    Console.WriteLine("File appears to be corrupted or invalid!");
                    System.Diagnostics.Debug.WriteLine("File appears to be corrupted or invalid!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg file analysis failed: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"FFmpeg file analysis failed: {ex.Message}");
            }
        }

        private string GenerateRecordingPath()
        {
            var extension = "mp4";
            var fileName = $"SharpShot_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
            var savePath = Path.Combine(_settingsService.CurrentSettings.SavePath, fileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return savePath;
        }

        private void StartRecordingTimer()
        {
            var timer = new System.Windows.Forms.Timer { Interval = 2000 }; // Update every 2 seconds to reduce flickering
            timer.Tick += (sender, e) =>
            {
                if (_isRecording && !_isPaused)
                {
                    RecordingTimeUpdated?.Invoke(RecordingDuration);
                }
                else if (!_isRecording)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            };
            timer.Start();
        }

        public string GetCurrentRecordingPath()
        {
            return _currentRecordingPath ?? string.Empty;
        }

        public void Dispose()
        {
            _recordingCancellationToken?.Cancel();
            _recordingCancellationToken?.Dispose();
            _ffmpegProcess?.Dispose();
        }
        
        private void ValidateCreatedFile()
        {
            try
            {
                Console.WriteLine("=== File Validation ===");
                
                if (File.Exists(_currentRecordingPath))
                {
                    var fileInfo = new FileInfo(_currentRecordingPath);
                    Console.WriteLine($"File exists: Yes");
                    Console.WriteLine($"File size: {fileInfo.Length} bytes ({fileInfo.Length / (1024.0 * 1024.0):F2} MB)");
                    Console.WriteLine($"File created: {fileInfo.CreationTime}");
                    Console.WriteLine($"File modified: {fileInfo.LastWriteTime}");
                    
                    // Check file size
                    if (fileInfo.Length < 1024)
                    {
                        Console.WriteLine("❌ ERROR: File is very small (< 1KB), likely corrupted!");
                        Console.WriteLine("This usually means FFmpeg failed to start or exited immediately.");
                    }
                    else if (fileInfo.Length < 1024 * 1024)
                    {
                        Console.WriteLine("⚠️ WARNING: File is small (< 1MB), may be incomplete!");
                    }
                    else
                    {
                        Console.WriteLine("✅ File size looks reasonable");
                    }
                    
                    // Try to validate with FFmpeg
                    _ = Task.Run(() => ValidateFileWithFFmpeg(_currentRecordingPath));
                }
                else
                {
                    Console.WriteLine("❌ ERROR: File does not exist!");
                    
                    // Check if any files were created in the directory
                    var outputDir = Path.GetDirectoryName(_currentRecordingPath);
                    if (Directory.Exists(outputDir))
                    {
                        var files = Directory.GetFiles(outputDir, "*.mp4");
                        Console.WriteLine($"Found {files.Length} MP4 files in output directory:");
                        foreach (var file in files)
                        {
                            var info = new FileInfo(file);
                            Console.WriteLine($"  - {Path.GetFileName(file)} ({info.Length} bytes)");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File validation failed: {ex.Message}");
            }
        }
        
        private void ValidateFileWithFFmpeg(string filePath)
        {
            try
            {
                var ffmpegPath = GetBundledFFmpegPath();
                if (string.IsNullOrEmpty(ffmpegPath)) 
                {
                    Console.WriteLine("Cannot validate file - FFmpeg not found");
                    return;
                }
                
                Console.WriteLine("Validating file with FFmpeg...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"-i \"{filePath}\" -f null -",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                Console.WriteLine($"FFmpeg validation exit code: {process.ExitCode}");
                
                if (process.ExitCode == 0)
                {
                    Console.WriteLine("✅ File appears to be valid MP4!");
                }
                else
                {
                    Console.WriteLine("❌ File appears to be corrupted or invalid!");
                    Console.WriteLine($"FFmpeg validation error: {error}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FFmpeg file validation failed: {ex.Message}");
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sharpshot_debug.log");
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }
} 