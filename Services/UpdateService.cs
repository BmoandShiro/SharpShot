using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using SharpShot.Models;

namespace SharpShot.Services
{
    public class UpdateService : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly HttpClient _httpClient;
        private const string GitHubApiBase = "https://api.github.com/repos";
        private const string DefaultRepoOwner = "YourGitHubUsername"; // TODO: Replace with your GitHub username
        private const string DefaultRepoName = "SharpShot"; // TODO: Replace with your repo name if different
        
        // Update check settings
        private const int UpdateCheckIntervalHours = 24; // Check once per day
        private const string LastUpdateCheckFile = "last_update_check.txt";

        public UpdateService(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SharpShot-Updater/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets the current application version
        /// </summary>
        public Version GetCurrentVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version ?? new Version(1, 0, 0, 0);
        }

        /// <summary>
        /// Checks if an update is available
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync(bool forceCheck = false)
        {
            try
            {
                // Check if we should skip update check (too soon since last check)
                if (!forceCheck && !ShouldCheckForUpdates())
                {
                    return null;
                }

                // Get latest release from GitHub
                var latestRelease = await GetLatestReleaseAsync();
                if (latestRelease == null)
                {
                    return null;
                }

                var latestVersion = ParseVersion(latestRelease.TagName);
                var currentVersion = GetCurrentVersion();

                if (latestVersion > currentVersion)
                {
                    // Save last check time
                    SaveLastUpdateCheckTime();

                    return new UpdateInfo
                    {
                        Version = latestVersion.ToString(),
                        ReleaseName = latestRelease.Name ?? latestRelease.TagName,
                        ReleaseNotes = latestRelease.Body ?? "No release notes available.",
                        DownloadUrl = GetDownloadUrl(latestRelease),
                        PublishedAt = latestRelease.PublishedAt ?? DateTimeOffset.Now
                    };
                }

                // Save last check time even if no update
                SaveLastUpdateCheckTime();
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking for updates: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Downloads and applies an update
        /// </summary>
        public async Task<bool> DownloadAndApplyUpdateAsync(UpdateInfo updateInfo, IProgress<UpdateProgress>? progress = null)
        {
            try
            {
                progress?.Report(new UpdateProgress { Status = "Downloading update...", Percentage = 0 });

                // Get application directory
                var appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var tempDirectory = Path.Combine(Path.GetTempPath(), "SharpShot_Update");
                
                // Clean up any previous update attempts
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
                Directory.CreateDirectory(tempDirectory);

                // Download the update zip
                var zipPath = Path.Combine(tempDirectory, "update.zip");
                await DownloadFileAsync(updateInfo.DownloadUrl, zipPath, progress);

                progress?.Report(new UpdateProgress { Status = "Extracting update...", Percentage = 50 });

                // Extract to temp directory
                var extractPath = Path.Combine(tempDirectory, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractPath);

                progress?.Report(new UpdateProgress { Status = "Preparing update...", Percentage = 75 });

                // Create update script
                var updateScriptPath = CreateUpdateScript(appDirectory, extractPath);

                progress?.Report(new UpdateProgress { Status = "Update ready! Restarting application...", Percentage = 100 });

                // Launch update script and exit
                var scriptProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = updateScriptPath,
                        UseShellExecute = true,
                        CreateNoWindow = false
                    }
                };
                scriptProcess.Start();

                // Give the script a moment to start, then exit
                await Task.Delay(500);
                Application.Current.Shutdown();

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying update: {ex.Message}");
                progress?.Report(new UpdateProgress { Status = $"Error: {ex.Message}", Percentage = 0 });
                return false;
            }
        }

        private async Task<GitHubRelease?> GetLatestReleaseAsync()
        {
            try
            {
                // Try to get repo info from settings or use defaults
                var repoOwner = _settingsService.CurrentSettings.UpdateRepoOwner ?? DefaultRepoOwner;
                var repoName = _settingsService.CurrentSettings.UpdateRepoName ?? DefaultRepoName;

                var url = $"{GitHubApiBase}/{repoOwner}/{repoName}/releases/latest";
                var response = await _httpClient.GetStringAsync(url);
                return JsonConvert.DeserializeObject<GitHubRelease>(response);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching latest release: {ex.Message}");
                return null;
            }
        }

        private string GetDownloadUrl(GitHubRelease release)
        {
            // Look for a zip file asset
            foreach (var asset in release.Assets ?? new GitHubAsset[0])
            {
                if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    return asset.BrowserDownloadUrl;
                }
            }

            // Fallback: try to construct download URL from release tag
            var repoOwner = _settingsService.CurrentSettings.UpdateRepoOwner ?? DefaultRepoOwner;
            var repoName = _settingsService.CurrentSettings.UpdateRepoName ?? DefaultRepoName;
            return $"https://github.com/{repoOwner}/{repoName}/archive/refs/tags/{release.TagName}.zip";
        }

        private async Task DownloadFileAsync(string url, string destinationPath, IProgress<UpdateProgress>? progress)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes > 0 && progress != null;

            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var contentStream = await response.Content.ReadAsStreamAsync();
            
            var totalBytesRead = 0L;
            var buffer = new byte[8192];
            var isMoreToRead = true;

            do
            {
                var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                {
                    isMoreToRead = false;
                }
                else
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (canReportProgress)
                    {
                        var percentage = (int)((totalBytesRead * 100) / totalBytes);
                        progress?.Report(new UpdateProgress 
                        { 
                            Status = $"Downloading... {FormatBytes(totalBytesRead)} / {FormatBytes(totalBytes)}",
                            Percentage = Math.Min(percentage, 50) // Cap at 50% since extraction is the other 50%
                        });
                    }
                }
            } while (isMoreToRead);
        }

        private string CreateUpdateScript(string appDirectory, string extractPath)
        {
            var scriptPath = Path.Combine(Path.GetTempPath(), "SharpShot_Update", "apply_update.ps1");
            var scriptContent = $@"
# SharpShot Update Script
# This script applies the update after the application closes

Write-Host ""Applying SharpShot update..."" -ForegroundColor Green

# Wait for SharpShot to fully close
Start-Sleep -Seconds 2

# Find the extracted update files
$updateSource = ""{extractPath.Replace("\\", "\\\\")}""
$appTarget = ""{appDirectory.Replace("\\", "\\\\")}""

# Find the actual SharpShot folder in the extracted zip (could be in a subfolder)
$sharpShotFolder = Get-ChildItem -Path $updateSource -Directory | Where-Object {{ $_.Name -like ""*SharpShot*"" }} | Select-Object -First 1

if ($sharpShotFolder) {{
    $updateSource = $sharpShotFolder.FullName
}}

Write-Host ""Copying files from: $updateSource""
Write-Host ""To: $appTarget""

# Copy all files except settings and user data
$excludeItems = @(""settings.json"", ""OBS-Studio"", ""ffmpeg"", ""*.log"")

Get-ChildItem -Path $updateSource -Recurse | ForEach-Object {{
    $relativePath = $_.FullName.Substring($updateSource.Length + 1)
    $targetPath = Join-Path $appTarget $relativePath
    
    # Skip excluded items
    $shouldExclude = $false
    foreach ($exclude in $excludeItems) {{
        if ($relativePath -like $exclude -or $_.Name -like $exclude) {{
            $shouldExclude = $true
            break
        }}
    }}
    
    if ($shouldExclude) {{
        Write-Host ""Skipping: $relativePath""
        return
    }}
    
    if ($_.PSIsContainer) {{
        if (!(Test-Path $targetPath)) {{
            New-Item -ItemType Directory -Path $targetPath -Force | Out-Null
        }}
    }} else {{
        $targetDir = Split-Path $targetPath -Parent
        if (!(Test-Path $targetDir)) {{
            New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        }}
        Copy-Item $_.FullName $targetPath -Force
        Write-Host ""Updated: $relativePath""
    }}
}}

Write-Host ""Update applied successfully!"" -ForegroundColor Green

# Clean up temp files
Start-Sleep -Seconds 1
Remove-Item -Path ""{Path.GetTempPath().Replace("\\", "\\\\")}SharpShot_Update"" -Recurse -Force -ErrorAction SilentlyContinue

# Restart SharpShot
$exePath = Join-Path ""{appDirectory.Replace("\\", "\\\\")}"" ""SharpShot.exe""
if (Test-Path $exePath) {{
    Start-Process -FilePath $exePath
    Write-Host ""SharpShot restarted!"" -ForegroundColor Green
}} else {{
    Write-Host ""Error: SharpShot.exe not found at $exePath"" -ForegroundColor Red
}}

# Close this window after a moment
Start-Sleep -Seconds 3
";

            File.WriteAllText(scriptPath, scriptContent);
            return scriptPath;
        }

        private Version ParseVersion(string versionString)
        {
            // Remove 'v' prefix if present
            versionString = versionString.TrimStart('v', 'V');
            
            if (Version.TryParse(versionString, out var version))
            {
                return version;
            }

            // Try to extract version from string like "v1.2.3" or "1.2.3.4"
            var parts = versionString.Split('.');
            if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
                {
                    var build = parts.Length > 2 && int.TryParse(parts[2], out var b) ? b : 0;
                    var revision = parts.Length > 3 && int.TryParse(parts[3], out var r) ? r : 0;
                    return new Version(major, minor, build, revision);
                }
            }

            return new Version(1, 0, 0, 0);
        }

        private bool ShouldCheckForUpdates()
        {
            var lastCheckFile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SharpShot",
                LastUpdateCheckFile);

            if (!File.Exists(lastCheckFile))
            {
                return true;
            }

            try
            {
                var lastCheckTime = DateTime.Parse(File.ReadAllText(lastCheckFile));
                return DateTime.Now - lastCheckTime > TimeSpan.FromHours(UpdateCheckIntervalHours);
            }
            catch
            {
                return true;
            }
        }

        private void SaveLastUpdateCheckTime()
        {
            try
            {
                var appDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SharpShot");
                
                if (!Directory.Exists(appDataDir))
                {
                    Directory.CreateDirectory(appDataDir);
                }

                var lastCheckFile = Path.Combine(appDataDir, LastUpdateCheckFile);
                File.WriteAllText(lastCheckFile, DateTime.Now.ToString("O"));
            }
            catch
            {
                // Ignore errors saving check time
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }

    public class UpdateInfo
    {
        public string Version { get; set; } = string.Empty;
        public string ReleaseName { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public DateTimeOffset PublishedAt { get; set; }
    }

    public class UpdateProgress
    {
        public string Status { get; set; } = string.Empty;
        public int Percentage { get; set; }
    }

    // GitHub API models
    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("body")]
        public string? Body { get; set; }

        [JsonProperty("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonProperty("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    public class GitHubAsset
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;
    }
}

