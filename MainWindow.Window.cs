using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NpvPlayer;

/// <summary>
/// MainWindow partial - Window events (keyboard, drag-drop, closing, fullscreen)
/// </summary>
public partial class MainWindow
{
    #region Fullscreen

    private bool _isFullscreen = false;
    private WindowState _previousWindowState;
    private bool _sidebarWasVisible = false;

    private void ToggleFullscreen()
    {
        if (_isFullscreen)
        {
            _isFullscreen = false;
            
            MainMenu.Visibility = Visibility.Visible;
            ControlBar.Visibility = Visibility.Visible;
            MenuRow.Height = GridLength.Auto;
            ControlsRow.Height = GridLength.Auto;
            
            MainGrid.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F0F0"));
            ContentArea.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F0F0F0"));
            
            Topmost = false;
            ResizeMode = ResizeMode.CanResize;
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _previousWindowState;
            
            if (_sidebarWasVisible)
            {
                ShowSidebar();
            }
        }
        else
        {
            _isFullscreen = true;
            _previousWindowState = WindowState;
            _sidebarWasVisible = SidebarPanel.Visibility == Visibility.Visible;
            
            MainMenu.Visibility = Visibility.Collapsed;
            ControlBar.Visibility = Visibility.Collapsed;
            MenuRow.Height = new GridLength(0);
            ControlsRow.Height = new GridLength(0);
            
            MainGrid.Background = System.Windows.Media.Brushes.Black;
            ContentArea.Background = System.Windows.Media.Brushes.Black;
            
            HideSidebar();
            
            if (_player != null && _isPlayerInitialized && _player.IsMediaLoaded)
            {
                PlayerHostContainer.Visibility = Visibility.Visible;
                LogoView.Visibility = Visibility.Hidden;
            }
            
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Normal;
            Topmost = true;
            Left = 0;
            Top = 0;
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;
        }
    }

    #endregion

    #region Sidebar Management

    private void TogglePlaylistPanel()
    {
        if (SidebarPanel.Visibility == Visibility.Visible)
        {
            HideSidebar();
        }
        else
        {
            ShowSidebar();
        }
    }

    private void HideSidebar()
    {
        SidebarPanel.Visibility = Visibility.Collapsed;
        SidebarSplitter.Visibility = Visibility.Collapsed;
        SidebarColumn.Width = new GridLength(0);
        SplitterColumn.Width = new GridLength(0);
        
        PlaylistView.Visibility = Visibility.Hidden;
        
        if (_player != null && _isPlayerInitialized && _player.IsMediaLoaded)
        {
            PlayerHostContainer.Visibility = Visibility.Visible;
            LogoView.Visibility = Visibility.Hidden;
        }
        else
        {
            PlayerHostContainer.Visibility = Visibility.Hidden;
            LogoView.Visibility = Visibility.Visible;
        }
    }

    private void ShowSidebar()
    {
        SidebarPanel.Visibility = Visibility.Visible;
        SidebarSplitter.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(200);
        SplitterColumn.Width = GridLength.Auto;
        
        LogoView.Visibility = Visibility.Hidden;
        
        if (_player != null && _isPlayerInitialized && _player.IsMediaLoaded)
        {
            PlaylistView.Visibility = Visibility.Hidden;
            PlayerHostContainer.Visibility = Visibility.Visible;
        }
        else
        {
            PlaylistView.Visibility = Visibility.Visible;
            PlayerHostContainer.Visibility = Visibility.Hidden;
        }
    }

    private void ShowVideoView()
    {
        HideSidebar();
        if (PlayerHostContainer != null) PlayerHostContainer.Visibility = Visibility.Visible;
        if (LogoView != null) LogoView.Visibility = Visibility.Hidden;
    }

    private void ShowPlaylistView()
    {
        ShowSidebar();
        if (PlayerHostContainer != null) PlayerHostContainer.Visibility = Visibility.Hidden;
        if (PlaylistView != null) PlaylistView.Visibility = Visibility.Visible;
    }

    #endregion

    #region Window Event Handlers

    private void Window_Closing(object sender, CancelEventArgs e)
    {
        SaveCurrentPlaybackPosition();
        SavePlaybackPositions();
        
        _timer.Stop();
        if (_player != null)
        {
            _player.Dispose();
            _player = null;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_player == null || !_isPlayerInitialized) return;

        switch (e.Key)
        {
            case Key.Space:
                if (_player.IsPaused)
                    _player.Resume();
                else
                    _player.Pause();
                e.Handled = true;
                break;

            case Key.Left:
                if (_player.IsMediaLoaded)
                {
                    var seekAmount = Keyboard.Modifiers == ModifierKeys.Control ? -60 : -5;
                    _player.SeekRelative(seekAmount);
                }
                e.Handled = true;
                break;

            case Key.Right:
                if (_player.IsMediaLoaded)
                {
                    var seekAmount = Keyboard.Modifiers == ModifierKeys.Control ? 60 : 5;
                    _player.SeekRelative(seekAmount);
                }
                e.Handled = true;
                break;

            case Key.Up:
                _player.Volume = Math.Min(100, _player.Volume + 5);
                sliVolume.Value = _player.Volume / 100.0;
                e.Handled = true;
                break;

            case Key.Down:
                _player.Volume = Math.Max(0, _player.Volume - 5);
                sliVolume.Value = _player.Volume / 100.0;
                e.Handled = true;
                break;

            case Key.M:
                _player.Muted = !_player.Muted;
                MenuMute.IsChecked = _player.Muted;
                e.Handled = true;
                break;

            case Key.A:
                _player.CycleAudioTrack();
                e.Handled = true;
                break;

            case Key.V:
                _player.CycleSubtitleTrack();
                e.Handled = true;
                break;

            case Key.S:
                if (Keyboard.Modifiers == ModifierKeys.None)
                {
                    _player.TakeScreenshot();
                }
                e.Handled = true;
                break;

            case Key.F:
                ToggleFullscreen();
                e.Handled = true;
                break;

            case Key.Escape:
                if (_isFullscreen)
                {
                    ToggleFullscreen();
                }
                e.Handled = true;
                break;

            case Key.O:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    BtnOpen_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;

            case Key.U:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    MenuOpenUrl_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;

            case Key.I:
                if (Keyboard.Modifiers == ModifierKeys.Control)
                {
                    MenuMediaInfo_Click(sender, new RoutedEventArgs());
                    e.Handled = true;
                }
                break;

            case Key.OemComma:
                _player.FrameBackStep();
                e.Handled = true;
                break;

            case Key.OemPeriod:
                _player.FrameStep();
                e.Handled = true;
                break;
        }
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!_isPlayerInitialized || _player == null) return;

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            string[]? files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
            if (files != null && files.Length > 0)
            {
                try
                {
                    SaveCurrentPlaybackPosition();
                    _player.Load(files[0]);
                    _currentMediaPath = files[0];
                    _timer.Start();
                    UpdatePlayPauseButton(true);
                    ShowVideoView();
                    AddToRecentFiles(files[0]);
                    RestorePlaybackPosition(files[0]);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to load media:\n{ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }

    #endregion
}
