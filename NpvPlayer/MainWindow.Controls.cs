using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NpvPlayer;

/// <summary>
/// MainWindow partial - Control button handlers and slider handlers
/// </summary>
public partial class MainWindow
{
    #region Playback Control Buttons

    private void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        if (!_isPlayerInitialized)
        {
            System.Windows.MessageBox.Show("MPV player is not initialized.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var openFileDialog = new Microsoft.Win32.OpenFileDialog();
        openFileDialog.Filter = "Media files (*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv;*.mp3;*.flac;*.wav;*.ogg)|" +
                               "*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv;*.mp3;*.flac;*.wav;*.ogg|" +
                               "Video files (*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv)|*.mp4;*.mkv;*.avi;*.webm;*.mov;*.wmv;*.flv|" +
                               "Audio files (*.mp3;*.flac;*.wav;*.ogg)|*.mp3;*.flac;*.wav;*.ogg|" +
                               "All files (*.*)|*.*";

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                SaveCurrentPlaybackPosition();
                _player!.Load(openFileDialog.FileName);
                _currentMediaPath = openFileDialog.FileName;
                _timer.Start();
                UpdatePlayPauseButton(true);
                ShowVideoView();
                AddToRecentFiles(openFileDialog.FileName);
                RestorePlaybackPosition(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to load media:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
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
            _timer.Start();
            ShowVideoView();
        }
    }

    private void BtnPause_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Pause();
            UpdatePlayPauseButton(false);
        }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Stop();
            _timer.Stop();
            sliProgress.Value = 0;
            lblCurrentTime.Text = "00:00";
            lblTotalTime.Text = "00:00";
            UpdatePlayPauseButton(false);
            
            OpMarker.Visibility = Visibility.Hidden;
            EdMarker.Visibility = Visibility.Hidden;
            
            ShowPlaylistView();
        }
    }

    private void BtnPrevious_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Position = TimeSpan.Zero;
        }
    }

    private void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        BtnStop_Click(sender, e);
    }

    private void BtnSkipOP_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            var skipDuration = TimeSpan.FromSeconds(90);
            if (_player.Duration > skipDuration)
            {
                _player.Position = skipDuration;
            }
        }
    }

    private void BtnSkipED_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            var skipDuration = TimeSpan.FromSeconds(90);
            var endPosition = _player.Duration - skipDuration;
            
            if (endPosition > TimeSpan.Zero && _player.Position < endPosition)
            {
                _player.Position = endPosition;
            }
            else
            {
                BtnStop_Click(sender, e);
            }
        }
    }

    private void BtnFullscreenCrop_Click(object sender, RoutedEventArgs e)
    {
        ToggleFullscreen();
    }

    private void BtnEqualizer_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Equalizer coming soon.", "Equalizer", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnPlaylist_Click(object sender, RoutedEventArgs e)
    {
        TogglePlaylistPanel();
    }

    private void BtnLoop_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Loop = !_player.Loop;
        }
    }

    private void BtnShuffle_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show("Shuffle coming soon with playlist support.", "Shuffle", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnMute_Click(object sender, RoutedEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            _player.Muted = !_player.Muted;
            BtnMute.Content = _player.Muted ? "\uE74F" : "\uE767";
        }
    }

    private void UpdatePlayPauseButton(bool isPlaying)
    {
        if (isPlaying)
        {
            BtnPlay.Content = "\uE769"; // Pause icon
            BtnPlay.ToolTip = "Pause";
        }
        else
        {
            BtnPlay.Content = "\uE768"; // Play icon
            BtnPlay.ToolTip = "Play";
        }
    }

    #endregion

    #region Slider Handlers

    private void sliVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            int volume = (int)(sliVolume.Value * 100);
            _player.Volume = volume;
        }
        if (lblVolume != null) 
        {
            int volume = (int)(sliVolume.Value * 100);
            lblVolume.Text = $"{volume}%";
        }
    }

    private void sliVolume_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            int volume = (int)(sliVolume.Value * 100);
            _player.Volume = volume;
            if (lblVolume != null) lblVolume.Text = $"{volume}%";
        }
    }

    private void sliProgress_DragStarted(object sender, DragStartedEventArgs e)
    {
        _isDragging = true;
    }

    private void sliProgress_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _isDragging = false;
        if (_player != null && _isPlayerInitialized && _player.IsMediaLoaded)
        {
            _player.Seek(sliProgress.Value);
        }
    }

    private void SeekBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isSeekBarPressed = true;
        var element = sender as FrameworkElement;
        element?.CaptureMouse();
        
        SeekToMousePosition(e.GetPosition(element), element);
    }

    private void SeekBar_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isSeekBarPressed)
        {
            var element = sender as FrameworkElement;
            SeekToMousePosition(e.GetPosition(element), element);
            element?.ReleaseMouseCapture();
        }
        _isSeekBarPressed = false;
    }

    private void SeekBar_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isSeekBarPressed && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var element = sender as FrameworkElement;
            SeekToMousePosition(e.GetPosition(element), element);
        }
    }

    private void SeekToMousePosition(System.Windows.Point position, FrameworkElement? element)
    {
        if (element == null || _player == null || !_isPlayerInitialized || !_player.IsMediaLoaded)
            return;

        double width = element.ActualWidth;
        if (width <= 0 || sliProgress.Maximum <= 0) return;

        double ratio = Math.Max(0, Math.Min(1, position.X / width));
        double newValue = ratio * sliProgress.Maximum;
        
        sliProgress.Value = newValue;
        _player.Seek(newValue);
        
        UpdateSeekBarVisuals();
    }

    private void UpdateSeekBarVisuals()
    {
        if (sliProgress.Maximum <= 0) return;
        
        double ratio = sliProgress.Value / sliProgress.Maximum;
        double trackWidth = SeekTrackBackground.ActualWidth;
        
        if (trackWidth <= 0) return;
        
        SeekTrackFill.Width = trackWidth * ratio;
        
        double thumbX = (trackWidth * ratio) - 6;
        Canvas.SetLeft(SeekThumb, Math.Max(0, thumbX));
    }

    // Keep these for compatibility but they won't be used much
    private bool _isSliderPressed = false;
    private void sliProgress_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
    private void sliProgress_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e) { }
    private void sliProgress_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e) { }

    private void sliProgress_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null && _isPlayerInitialized)
        {
            var position = TimeSpan.FromSeconds(sliProgress.Value);
            lblCurrentTime.Text = position.ToString(@"mm\:ss");
            
            if (!_isSeekBarPressed)
            {
                UpdateSeekBarVisuals();
            }
        }
    }

    #endregion

    #region Timeline Markers

    private void UpdateTimelineMarkers()
    {
        if (_player == null || !_isPlayerInitialized || !_player.IsMediaLoaded || 
            _player.Duration.TotalSeconds <= 0 || sliProgress.Maximum <= 0)
        {
            OpMarker.Visibility = Visibility.Hidden;
            EdMarker.Visibility = Visibility.Hidden;
            return;
        }

        double duration = _player.Duration.TotalSeconds;
        double opTime = 90;
        double edTime = duration - 90;

        if (duration > 180)
        {
            double trackWidth = SeekTrackBackground.ActualWidth;
            if (trackWidth <= 0) return;

            double opX = (opTime / duration) * trackWidth - 4;
            double edX = (edTime / duration) * trackWidth - 4;

            Canvas.SetLeft(OpMarker, opX);
            Canvas.SetLeft(EdMarker, edX);

            OpMarker.Visibility = Visibility.Visible;
            EdMarker.Visibility = Visibility.Visible;
        }
        else
        {
            OpMarker.Visibility = Visibility.Hidden;
            EdMarker.Visibility = Visibility.Hidden;
        }
    }

    private void sliProgress_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_player != null && _player.IsMediaLoaded)
        {
            UpdateTimelineMarkers();
            UpdateSeekBarVisuals();
        }
    }

    private void SeekTrackBackground_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_player != null && _player.IsMediaLoaded)
        {
            UpdateSeekBarVisuals();
            UpdateTimelineMarkers();
        }
    }

    #endregion
}
