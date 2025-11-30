using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using SevenZip;

namespace NpvPlayer;

/// <summary>
/// MainWindow partial - MPV Player initialization and event handlers
/// </summary>
public partial class MainWindow
{
    private bool CheckLibmpvExists()
    {
        string[] searchPaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libmpv-2.dll"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mpv-2.dll"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "libmpv-2.dll"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "mpv-2.dll")
        };

        return searchPaths.Any(File.Exists);
    }

    private async Task DownloadLibmpvAsync()
    {
        var progressWindow = new Window
        {
            Title = "Downloading libmpv...",
            Width = 450,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var stackPanel = new StackPanel { Margin = new Thickness(20) };
        var statusText = new TextBlock { Text = "Preparing download...", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap };
        var progressBar = new System.Windows.Controls.ProgressBar { Height = 25, Minimum = 0, Maximum = 100 };
        stackPanel.Children.Add(statusText);
        stackPanel.Children.Add(progressBar);
        progressWindow.Content = stackPanel;
        progressWindow.Show();

        string targetPath = AppDomain.CurrentDomain.BaseDirectory;
        string dllPath = Path.Combine(targetPath, "libmpv-2.dll");

        try
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "NpvPlayer/1.0");
            httpClient.Timeout = TimeSpan.FromMinutes(10);

            statusText.Text = "Finding latest libmpv release...";
            progressBar.IsIndeterminate = true;

            string? downloadUrl = null;
            bool is7z = true;
            
            try
            {
                var apiResponse = await httpClient.GetStringAsync("https://api.github.com/repos/shinchiro/mpv-winbuild-cmake/releases/latest");
                var match = System.Text.RegularExpressions.Regex.Match(apiResponse, @"""browser_download_url""\s*:\s*""([^""]*mpv-dev-x86_64[^""]*\.7z)""");
                if (match.Success)
                {
                    downloadUrl = match.Groups[1].Value;
                }
            }
            catch { }

            downloadUrl ??= "https://github.com/shinchiro/mpv-winbuild-cmake/releases/latest/download/mpv-dev-x86_64.7z";

            string tempDir = Path.Combine(Path.GetTempPath(), "NpvPlayer_libmpv_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, is7z ? "mpv-dev.7z" : "mpv-dev.zip");

            try
            {
                statusText.Text = "Downloading libmpv package...";
                
                var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Download failed: {response.StatusCode}");
                }

                var totalBytes = response.Content.Headers.ContentLength ?? -1;
                var downloadedBytes = 0L;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    int bytesRead;
                    progressBar.IsIndeterminate = false;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        downloadedBytes += bytesRead;

                        if (totalBytes > 0)
                        {
                            var progress = (double)downloadedBytes / totalBytes * 100;
                            progressBar.Value = progress;
                            statusText.Text = $"Downloading... {downloadedBytes / 1024 / 1024:F1} MB / {totalBytes / 1024 / 1024:F1} MB";
                        }
                        else
                        {
                            statusText.Text = $"Downloading... {downloadedBytes / 1024 / 1024:F1} MB";
                        }
                    }
                }

                statusText.Text = "Download complete! Extracting...";
                progressBar.IsIndeterminate = true;

                // First try to download and use portable 7za.exe if needed
                bool extracted = false;
                if (is7z)
                {
                    extracted = await TryExtract7zAsync(tempFile, tempDir, statusText);
                }
                else
                {
                    // ZIP extraction using built-in .NET
                    System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, tempDir);
                    extracted = true;
                }
                
                if (extracted)
                {
                    var dllFiles = Directory.GetFiles(tempDir, "libmpv-2.dll", SearchOption.AllDirectories);
                    if (dllFiles.Length > 0)
                    {
                        File.Copy(dllFiles[0], dllPath, true);
                        try { Directory.Delete(tempDir, true); } catch { }
                        
                        progressWindow.Close();
                        System.Windows.MessageBox.Show(
                            "libmpv-2.dll has been downloaded and installed successfully!\n\nThe player will now initialize.",
                            "Download Complete",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }
                }

                try { Directory.Delete(tempDir, true); } catch { }
            }
            catch (Exception ex)
            {
                try { Directory.Delete(tempDir, true); } catch { }
                throw new Exception($"Download error: {ex.Message}");
            }

            progressWindow.Close();
            ShowManualDownloadInstructions();
        }
        catch (Exception ex)
        {
            progressWindow.Close();
            System.Windows.MessageBox.Show(
                $"Failed to download libmpv:\n{ex.Message}\n\nOpening manual download instructions...",
                "Download Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ShowManualDownloadInstructions();
        }
    }

    private Task<bool> TryExtract7zAsync(string archivePath, string extractPath, TextBlock statusText)
    {
        return Task.Run(() =>
        {
            try
            {
                // Use built-in 7z.Libs - set the library path
                string sevenZipDll = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "7z.dll");
                
                // Check if 7z.dll exists (should be copied by 7z.Libs package)
                if (!File.Exists(sevenZipDll))
                {
                    // Try to find it in common locations from the NuGet package
                    var possiblePaths = new[]
                    {
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runtimes", "win-x64", "native", "7z.dll"),
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "x64", "7z.dll"),
                    };
                    sevenZipDll = possiblePaths.FirstOrDefault(File.Exists) ?? sevenZipDll;
                }

                if (File.Exists(sevenZipDll))
                {
                    SevenZipBase.SetLibraryPath(sevenZipDll);
                }

                Dispatcher.Invoke(() => statusText.Text = "Extracting with built-in 7-Zip...");
                
                using var extractor = new SevenZipExtractor(archivePath);
                extractor.ExtractArchive(extractPath);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"7z extraction error: {ex.Message}");
                return false;
            }
        });
    }

    private void ShowManualDownloadInstructions()
    {
        var result = System.Windows.MessageBox.Show(
            "Automatic extraction requires 7-Zip to be installed.\n\n" +
            "Please download libmpv manually:\n\n" +
            "1. Click 'Yes' to open the download page\n" +
            "2. Download 'mpv-dev-x86_64-XXXXXXXX-git-XXXXXXX.7z'\n" +
            "3. Extract with 7-Zip\n" +
            "4. Copy 'libmpv-2.dll' to:\n" +
            $"   {AppDomain.CurrentDomain.BaseDirectory}\n\n" +
            "Open download page?",
            "Manual Download Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result == MessageBoxResult.Yes)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://sourceforge.net/projects/mpv-player-windows/files/libmpv/",
                UseShellExecute = true
            });
        }
    }

    private void InitializeMpvPlayer()
    {
        try
        {
            _player = new MpvPlayerWrapper(PlayerHost.Handle);
            _player.Initialize();

            _player.Volume = 50;

            _player.MediaLoaded += Player_MediaLoaded;
            _player.MediaFinished += Player_MediaFinished;
            _player.MediaError += Player_MediaError;

            PlayerHost.MouseClick += PlayerHost_MouseClick;
            PlayerHost.DoubleClick += PlayerHost_DoubleClick;

            _isPlayerInitialized = true;
        }
        catch (Exception ex)
        {
            _isPlayerInitialized = false;
            System.Windows.MessageBox.Show(
                $"Failed to initialize MPV player:\n{ex.Message}\n\n" +
                "Please ensure 'libmpv-2.dll' (or 'mpv-2.dll') is in the application directory or 'lib' folder.\n\n" +
                "Download from: https://sourceforge.net/projects/mpv-player-windows/files/libmpv/",
                "MPV Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Player_MediaLoaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (_player?.Duration != null)
            {
                sliProgress.Maximum = _player.Duration.TotalSeconds;
                sliProgress.Value = 0;
                Title = $"NpvPlayer - {_player.MediaTitle ?? "MPV Video Player"}";
                UpdateTimelineMarkers();
            }
            _timer.Start();
        });
    }

    private void Player_MediaFinished(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _timer.Stop();
            sliProgress.Value = 0;
            lblCurrentTime.Text = "00:00";
            lblTotalTime.Text = "00:00";
            Title = "NpvPlayer - MPV Video Player";
            UpdatePlayPauseButton(false);
        });
    }

    private void Player_MediaError(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show("An error occurred while playing the media.", "Playback Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        });
    }

    private void PlayerHost_MouseClick(object? sender, System.Windows.Forms.MouseEventArgs e)
    {
        if (e.Button == System.Windows.Forms.MouseButtons.Left)
        {
            Dispatcher.Invoke(() =>
            {
                if (_player != null && _isPlayerInitialized && _player.IsMediaLoaded)
                {
                    if (_player.IsPaused)
                    {
                        _player.Resume();
                        UpdatePlayPauseButton(true);
                    }
                    else
                    {
                        _player.Pause();
                        UpdatePlayPauseButton(false);
                    }
                }
            });
        }
    }

    private void PlayerHost_DoubleClick(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ToggleFullscreen();
        });
    }
}
