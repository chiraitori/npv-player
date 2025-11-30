using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NpvPlayer;

/// <summary>
/// MainWindow partial - Menu handlers (Media, Audio, Video, Subtitle, Tools, View, Help)
/// </summary>
public partial class MainWindow
{
    #region Media Menu Handlers

    private void MenuOpenFile_Click(object sender, RoutedEventArgs e)
    {
        BtnOpen_Click(sender, e);
    }

    private void MenuOpenUrl_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new InputDialog("Open URL", "Enter media URL:");
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            try
            {
                SaveCurrentPlaybackPosition();
                _player!.Load(dialog.InputText);
                _currentMediaPath = dialog.InputText;
                _timer.Start();
                UpdatePlayPauseButton(true);
                ShowVideoView();
                AddToRecentFiles(dialog.InputText);
                RestorePlaybackPosition(dialog.InputText);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load URL:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuAddSubtitle_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null) return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Filter = "Subtitle files (*.srt;*.ass;*.ssa;*.sub;*.vtt)|*.srt;*.ass;*.ssa;*.sub;*.vtt|All files (*.*)|*.*";

        if (openFileDialog.ShowDialog() == true)
        {
            _player.LoadSubtitleFile(openFileDialog.FileName);
        }
    }

    private void MenuAddAudio_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null) return;

        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Filter = "Audio files (*.mp3;*.flac;*.wav;*.ogg;*.aac;*.m4a)|*.mp3;*.flac;*.wav;*.ogg;*.aac;*.m4a|All files (*.*)|*.*";

        if (openFileDialog.ShowDialog() == true)
        {
            _player.LoadAudioFile(openFileDialog.FileName);
        }
    }

    private void MenuMediaInfo_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null || !_player.IsMediaLoaded)
        {
            System.Windows.MessageBox.Show("No media loaded.", "Media Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var info = $"Title: {_player.MediaTitle ?? "Unknown"}\n" +
                   $"Path: {_player.CurrentFilePath ?? "Unknown"}\n" +
                   $"Duration: {_player.Duration:hh\\:mm\\:ss}\n" +
                   $"Video Codec: {_player.VideoCodec ?? "N/A"}\n" +
                   $"Audio Codec: {_player.AudioCodec ?? "N/A"}\n" +
                   $"Resolution: {_player.VideoWidth}x{_player.VideoHeight}\n" +
                   $"FPS: {_player.FPS:F2}\n" +
                   $"Volume: {_player.Volume}%\n" +
                   $"Speed: {_player.Speed:F2}x\n" +
                   $"Audio Delay: {_player.AudioDelay} ms\n" +
                   $"Subtitle Delay: {_player.SubtitleDelay} ms";

        System.Windows.MessageBox.Show(info, "Media Information", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuOpenMultipleFiles_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Multiselect = true;
        openFileDialog.Filter = "Media files (*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv;*.mp3;*.flac;*.wav;*.ogg)|" +
                               "*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv;*.mp3;*.flac;*.wav;*.ogg|" +
                               "All files (*.*)|*.*";

        if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
        {
            try
            {
                _player!.Load(openFileDialog.FileNames[0]);
                _timer.Start();
                UpdatePlayPauseButton(true);
                ShowVideoView();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load media:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var folderDialog = new System.Windows.Forms.FolderBrowserDialog();
        folderDialog.Description = "Select a folder containing media files";

        if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            try
            {
                var extensions = new[] { ".mp4", ".mkv", ".avi", ".webm", ".mov", ".wmv", ".flv", ".mp3", ".flac", ".wav", ".ogg" };
                var files = System.IO.Directory.GetFiles(folderDialog.SelectedPath)
                    .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLower()))
                    .OrderBy(f => f)
                    .ToArray();

                if (files.Length > 0)
                {
                    _player!.Load(files[0]);
                    _timer.Start();
                    UpdatePlayPauseButton(true);
                    ShowVideoView();
                }
                else
                {
                    System.Windows.MessageBox.Show("No media files found in the selected folder.", "No Media Files",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open folder:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuOpenDisc_Click(object sender, RoutedEventArgs e)
    {
        var drives = System.IO.DriveInfo.GetDrives()
            .Where(d => d.DriveType == System.IO.DriveType.CDRom && d.IsReady)
            .ToArray();

        if (drives.Length == 0)
        {
            System.Windows.MessageBox.Show("No disc found. Please insert a disc and try again.", "No Disc",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _player?.Load($"dvd:///{drives[0].Name}");
            _timer.Start();
            UpdatePlayPauseButton(true);
            ShowVideoView();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open disc:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuOpenCaptureDevice_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dialog = new InputDialog("Open Capture Device", 
            "Enter device URL:\n\nExamples:\n" +
            "• av://dshow:video=\"Webcam Name\"\n" +
            "• av://dshow:video=\"USB Camera\":audio=\"Microphone\"\n" +
            "• screen://\n" +
            "• av://gdigrab:desktop");
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            try
            {
                SaveCurrentPlaybackPosition();
                _player.Load(dialog.InputText);
                _currentMediaPath = null;
                _timer.Start();
                UpdatePlayPauseButton(true);
                ShowVideoView();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open capture device:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuOpenClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (System.Windows.Clipboard.ContainsText())
            {
                var text = System.Windows.Clipboard.GetText().Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    if (Uri.IsWellFormedUriString(text, UriKind.Absolute) || System.IO.File.Exists(text))
                    {
                        SaveCurrentPlaybackPosition();
                        _player.Load(text);
                        _currentMediaPath = text;
                        _timer.Start();
                        UpdatePlayPauseButton(true);
                        ShowVideoView();
                        AddToRecentFiles(text);
                        RestorePlaybackPosition(text);
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Clipboard does not contain a valid URL or file path.", "Invalid Location",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            else
            {
                System.Windows.MessageBox.Show("Clipboard is empty or does not contain text.", "No Location",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open from clipboard:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MenuSavePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_player == null || !_player.IsMediaLoaded)
        {
            System.Windows.MessageBox.Show("No media loaded to save.", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "M3U Playlist (*.m3u)|*.m3u|M3U8 Playlist (*.m3u8)|*.m3u8",
            DefaultExt = ".m3u"
        };

        if (saveDialog.ShowDialog() == true)
        {
            try
            {
                var content = "#EXTM3U\n";
                if (!string.IsNullOrEmpty(_player.CurrentFilePath))
                {
                    content += $"#EXTINF:-1,{_player.MediaTitle ?? Path.GetFileName(_player.CurrentFilePath)}\n";
                    content += _player.CurrentFilePath + "\n";
                }
                File.WriteAllText(saveDialog.FileName, content);
                System.Windows.MessageBox.Show("Playlist saved successfully.", "Save Playlist", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save playlist:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void MenuPlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            if (_player.IsPaused)
                _player.Resume();
            else
                _player.Pause();
        }
    }

    private void MenuStop_Click(object sender, RoutedEventArgs e)
    {
        BtnStop_Click(sender, e);
    }

    private void MenuFrameStep_Click(object sender, RoutedEventArgs e)
    {
        _player?.FrameStep();
    }

    private void MenuJumpForward_Click(object sender, RoutedEventArgs e)
    {
        _player?.SeekRelative(10);
    }

    private void MenuJumpBackward_Click(object sender, RoutedEventArgs e)
    {
        _player?.SeekRelative(-10);
    }

    private void MenuJumpForwardLarge_Click(object sender, RoutedEventArgs e)
    {
        _player?.SeekRelative(60);
    }

    private void MenuJumpBackwardLarge_Click(object sender, RoutedEventArgs e)
    {
        _player?.SeekRelative(-60);
    }

    private void MenuSpeed025_Click(object sender, RoutedEventArgs e) => SetSpeed(0.25);
    private void MenuSpeed05_Click(object sender, RoutedEventArgs e) => SetSpeed(0.5);
    private void MenuSpeed075_Click(object sender, RoutedEventArgs e) => SetSpeed(0.75);
    private void MenuSpeed1_Click(object sender, RoutedEventArgs e) => SetSpeed(1.0);
    private void MenuSpeed125_Click(object sender, RoutedEventArgs e) => SetSpeed(1.25);
    private void MenuSpeed15_Click(object sender, RoutedEventArgs e) => SetSpeed(1.5);
    private void MenuSpeed2_Click(object sender, RoutedEventArgs e) => SetSpeed(2.0);

    private void SetSpeed(double speed)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Speed = speed;
        }
    }

    private void MenuLoop_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Loop = MenuLoop.IsChecked;
        }
    }

    private void MenuQuit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Audio Menu Handlers

    private void MenuAudioTrack_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        MenuAudioTrack.Items.Clear();

        if (_player == null || !_isPlayerInitialized || !_player.IsMediaLoaded)
        {
            MenuAudioTrack.Items.Add(new MenuItem { Header = "(No media loaded)", IsEnabled = false });
            return;
        }

        var tracks = _player.GetAudioTracks();
        
        if (tracks.Count == 0)
        {
            MenuAudioTrack.Items.Add(new MenuItem { Header = "(No audio tracks)", IsEnabled = false });
            return;
        }

        var disableItem = new MenuItem 
        { 
            Header = "Disable",
            IsCheckable = true,
            IsChecked = !tracks.Exists(t => t.IsSelected)
        };
        disableItem.Click += (s, args) => _player.SetAudioTrack(0);
        MenuAudioTrack.Items.Add(disableItem);

        foreach (var track in tracks)
        {
            var item = new MenuItem 
            { 
                Header = track.DisplayName,
                IsCheckable = true,
                IsChecked = track.IsSelected,
                Tag = track.Id
            };
            item.Click += (s, args) => _player.SetAudioTrack(track.Id);
            MenuAudioTrack.Items.Add(item);
        }
    }

    private void MenuCycleAudio_Click(object sender, RoutedEventArgs e)
    {
        _player?.CycleAudioTrack();
    }

    private void MenuPrevFrame_Click(object sender, RoutedEventArgs e)
    {
        _player?.FrameBackStep();
    }

    private void MenuNextFrame_Click(object sender, RoutedEventArgs e)
    {
        _player?.FrameStep();
    }

    private void MenuMute_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Muted = MenuMute.IsChecked;
        }
    }

    private void MenuVolumeUp_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Volume = Math.Min(100, _player.Volume + 5);
            sliVolume.Value = _player.Volume / 100.0;
        }
    }

    private void MenuVolumeDown_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Volume = Math.Max(0, _player.Volume - 5);
            sliVolume.Value = _player.Volume / 100.0;
        }
    }

    private void MenuAudioDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.AudioDelay += 100;
        }
    }

    private void MenuAudioDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.AudioDelay -= 100;
        }
    }

    private void MenuAudioDelayReset_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.AudioDelay = 0;
        }
    }

    #endregion

    #region Video Menu Handlers

    private void MenuAspectDefault_Click(object sender, RoutedEventArgs e) => _player?.ResetAspectRatio();
    private void MenuAspect169_Click(object sender, RoutedEventArgs e) => _player?.SetAspectRatio("16:9");
    private void MenuAspect43_Click(object sender, RoutedEventArgs e) => _player?.SetAspectRatio("4:3");
    private void MenuAspect219_Click(object sender, RoutedEventArgs e) => _player?.SetAspectRatio("21:9");
    private void MenuAspect11_Click(object sender, RoutedEventArgs e) => _player?.SetAspectRatio("1:1");

    private void MenuRotate0_Click(object sender, RoutedEventArgs e) => _player?.RotateVideo(0);
    private void MenuRotate90_Click(object sender, RoutedEventArgs e) => _player?.RotateVideo(90);
    private void MenuRotate180_Click(object sender, RoutedEventArgs e) => _player?.RotateVideo(180);
    private void MenuRotate270_Click(object sender, RoutedEventArgs e) => _player?.RotateVideo(270);

    private void MenuZoomReset_Click(object sender, RoutedEventArgs e) => _player?.SetVideoZoom(0);
    private void MenuZoomIn_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
            _player.SetVideoZoom(0.1);
    }
    private void MenuZoomOut_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
            _player.SetVideoZoom(-0.1);
    }

    private void MenuScreenshot_Click(object sender, RoutedEventArgs e)
    {
        _player?.TakeScreenshot();
    }

    #endregion

    #region Subtitle Menu Handlers

    private void MenuSubTrack_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        MenuSubTrack.Items.Clear();

        if (_player == null || !_isPlayerInitialized || !_player.IsMediaLoaded)
        {
            MenuSubTrack.Items.Add(new MenuItem { Header = "(No media loaded)", IsEnabled = false });
            return;
        }

        var tracks = _player.GetSubtitleTracks();
        
        var disableItem = new MenuItem 
        { 
            Header = "Disable",
            IsCheckable = true,
            IsChecked = tracks.Count == 0 || !tracks.Exists(t => t.IsSelected)
        };
        disableItem.Click += (s, args) => _player.SetSubtitleTrack(0);
        MenuSubTrack.Items.Add(disableItem);

        if (tracks.Count == 0)
        {
            return;
        }

        MenuSubTrack.Items.Add(new Separator());

        foreach (var track in tracks)
        {
            var item = new MenuItem 
            { 
                Header = track.DisplayName,
                IsCheckable = true,
                IsChecked = track.IsSelected,
                Tag = track.Id
            };
            item.Click += (s, args) => _player.SetSubtitleTrack(track.Id);
            MenuSubTrack.Items.Add(item);
        }
    }

    private void MenuCycleSubtitle_Click(object sender, RoutedEventArgs e)
    {
        _player?.CycleSubtitleTrack();
    }

    private void MenuToggleSubtitle_Click(object sender, RoutedEventArgs e)
    {
        _player?.ToggleSubtitles();
    }

    private void MenuSubDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.SubtitleDelay += 100;
        }
    }

    private void MenuSubDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.SubtitleDelay -= 100;
        }
    }

    private void MenuSubDelayReset_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.SubtitleDelay = 0;
        }
    }

    #endregion

    #region Tools Menu Handlers

    private void MenuPreferences_Click(object sender, RoutedEventArgs e)
    {
        var prefsWindow = new Window
        {
            Title = "Preferences",
            Width = 400,
            Height = 350,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.NoResize
        };

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var hwdecLabel = new TextBlock { Text = "Hardware Decoding:", Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(hwdecLabel, 0);
        var hwdecCheck = new System.Windows.Controls.CheckBox { Content = "Enable hardware acceleration (auto)", IsChecked = true, Margin = new Thickness(0, 5, 0, 10) };
        Grid.SetRow(hwdecCheck, 1);

        var skipLabel = new TextBlock { Text = "Skip OP/ED Duration (seconds):", Margin = new Thickness(0, 5, 0, 5) };
        Grid.SetRow(skipLabel, 2);
        var skipSlider = new Slider { Minimum = 60, Maximum = 120, Value = 90, TickFrequency = 10, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 5) };
        var skipValue = new TextBlock { Text = "90 seconds" };
        skipSlider.ValueChanged += (s, args) => skipValue.Text = $"{(int)skipSlider.Value} seconds";
        var skipPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        skipPanel.Children.Add(skipSlider);
        skipPanel.Children.Add(skipValue);
        skipSlider.Width = 200;
        Grid.SetRow(skipPanel, 3);

        var volLabel = new TextBlock { Text = "Default Volume:", Margin = new Thickness(0, 10, 0, 5) };
        Grid.SetRow(volLabel, 4);

        var buttonPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var okButton = new System.Windows.Controls.Button { Content = "OK", Width = 75, Margin = new Thickness(5), Padding = new Thickness(5) };
        var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75, Margin = new Thickness(5), Padding = new Thickness(5) };
        okButton.Click += (s, args) => { prefsWindow.DialogResult = true; prefsWindow.Close(); };
        cancelButton.Click += (s, args) => prefsWindow.Close();
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 6);

        grid.Children.Add(hwdecLabel);
        grid.Children.Add(hwdecCheck);
        grid.Children.Add(skipLabel);
        grid.Children.Add(skipPanel);
        grid.Children.Add(volLabel);
        grid.Children.Add(buttonPanel);

        prefsWindow.Content = grid;
        prefsWindow.ShowDialog();
    }

    #endregion

    #region View Menu Handlers

    private void MenuFullscreen_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void MenuAlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        Topmost = MenuAlwaysOnTop.IsChecked;
    }

    #endregion

    #region Help Menu Handlers

    private void MenuShortcuts_Click(object sender, RoutedEventArgs e)
    {
        var shortcuts = @"Keyboard Shortcuts:

Playback:
  Space - Play/Pause
  S - Stop
  ← / → - Seek ±5 seconds
  Ctrl+← / Ctrl+→ - Seek ±1 minute
  , / . - Previous/Next frame

Audio:
  ↑ / ↓ - Volume up/down
  M - Toggle mute
  A - Cycle audio track

Video:
  F - Toggle fullscreen
  S - Take screenshot
  V - Cycle subtitle track

General:
  Ctrl+O - Open file
  Ctrl+U - Open URL
  Escape - Exit fullscreen
  Ctrl+I - Media information";

        System.Windows.MessageBox.Show(shortcuts, "Keyboard Shortcuts", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void MenuAbout_Click(object sender, RoutedEventArgs e)
    {
        var about = "NpvPlayer\n\n" +
                    "A modern video player powered by MPV.\n\n" +
                    "Built with WPF and libmpv.\n\n" +
                    "© 2024";

        System.Windows.MessageBox.Show(about, "About NpvPlayer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    #endregion

    #region Recent Files Menu

    private void MenuRecentMedia_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        MenuRecentMedia.Items.Clear();

        if (_recentFiles.Count == 0)
        {
            MenuRecentMedia.Items.Add(new MenuItem { Header = "(No recent items)", IsEnabled = false });
            return;
        }

        foreach (var file in _recentFiles)
        {
            var menuItem = new MenuItem
            {
                Header = Path.GetFileName(file),
                ToolTip = file
            };
            menuItem.Click += (s, args) => PlayRecentFile(file);
            MenuRecentMedia.Items.Add(menuItem);
        }

        MenuRecentMedia.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Clear Recent List" };
        clearItem.Click += (s, args) =>
        {
            _recentFiles.Clear();
            SaveRecentFiles();
        };
        MenuRecentMedia.Items.Add(clearItem);
    }

    private void PlayRecentFile(string filePath)
    {
        if (!_isPlayerInitialized || _player == null) return;

        try
        {
            if (File.Exists(filePath) || Uri.IsWellFormedUriString(filePath, UriKind.Absolute))
            {
                SaveCurrentPlaybackPosition();
                _player.Load(filePath);
                _currentMediaPath = filePath;
                _timer.Start();
                UpdatePlayPauseButton(true);
                ShowVideoView();
                AddToRecentFiles(filePath);
                RestorePlaybackPosition(filePath);
            }
            else
            {
                System.Windows.MessageBox.Show($"File not found:\n{filePath}", "File Not Found",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                _recentFiles.Remove(filePath);
                SaveRecentFiles();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
