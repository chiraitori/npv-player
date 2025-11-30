using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NpvPlayer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// Main entry point - fields and initialization
/// </summary>
public partial class MainWindow : Window
{
    // Core state
    private bool _isDragging = false;
    private bool _isSeekBarPressed = false;
    private DispatcherTimer _timer;
    private MpvPlayerWrapper? _player;
    private bool _isPlayerInitialized = false;
    
    // Recent files
    private List<string> _recentFiles = new List<string>();
    private const int MaxRecentFiles = 10;
    private string _recentFilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "NpvPlayer", "recent.txt");
    
    // Playback position memory
    private string? _currentMediaPath;
    private Dictionary<string, double> _playbackPositions = new Dictionary<string, double>();
    private string _positionsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
        "NpvPlayer", "positions.txt");
    private const int MaxPositions = 100;

    public MainWindow()
    {
        InitializeComponent();
        
        _timer = new DispatcherTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += Timer_Tick;

        // Ensure markers are hidden at startup
        OpMarker.Visibility = Visibility.Hidden;
        EdMarker.Visibility = Visibility.Hidden;

        // Load persisted data
        LoadRecentFiles();
        LoadPlaybackPositions();

        // Initialize MPV player after the window is loaded
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Check if libmpv exists, if not offer to download
        if (!CheckLibmpvExists())
        {
            var result = System.Windows.MessageBox.Show(
                "libmpv-2.dll was not found.\n\nWould you like to download it automatically?\n\nThis will download approximately 30MB.",
                "MPV Library Missing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                await DownloadLibmpvAsync();
            }
        }

        InitializeMpvPlayer();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (_player == null || !_isPlayerInitialized) return;

        if (!_isDragging && !_isSeekBarPressed && _player.IsMediaLoaded)
        {
            try
            {
                var position = _player.Position;
                var duration = _player.Duration;

                sliProgress.Value = position.TotalSeconds;
                lblCurrentTime.Text = position.ToString(@"mm\:ss");
                lblTotalTime.Text = duration.ToString(@"mm\:ss");
                
                UpdateSeekBarVisuals();
            }
            catch
            {
                // Ignore position read errors during playback transitions
            }
        }
    }
}
