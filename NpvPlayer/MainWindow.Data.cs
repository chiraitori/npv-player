using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NpvPlayer;

/// <summary>
/// MainWindow partial - Data persistence (recent files, playback positions)
/// </summary>
public partial class MainWindow
{
    #region Recent Files

    private void LoadRecentFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(_recentFilesPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(_recentFilesPath))
            {
                _recentFiles = File.ReadAllLines(_recentFilesPath)
                    .Where(f => !string.IsNullOrWhiteSpace(f))
                    .Take(MaxRecentFiles)
                    .ToList();
            }
        }
        catch { }
    }

    private void SaveRecentFiles()
    {
        try
        {
            var dir = Path.GetDirectoryName(_recentFilesPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllLines(_recentFilesPath, _recentFiles);
        }
        catch { }
    }

    private void AddToRecentFiles(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        _recentFiles.Remove(filePath);
        _recentFiles.Insert(0, filePath);
        
        while (_recentFiles.Count > MaxRecentFiles)
            _recentFiles.RemoveAt(_recentFiles.Count - 1);

        SaveRecentFiles();
    }

    #endregion

    #region Playback Position Memory

    private void LoadPlaybackPositions()
    {
        try
        {
            var dir = Path.GetDirectoryName(_positionsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            if (File.Exists(_positionsFilePath))
            {
                var lines = File.ReadAllLines(_positionsFilePath);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length == 2 && double.TryParse(parts[1], out double position))
                    {
                        _playbackPositions[parts[0]] = position;
                    }
                }
            }
        }
        catch { }
    }

    private void SavePlaybackPositions()
    {
        try
        {
            var dir = Path.GetDirectoryName(_positionsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var recentPositions = _playbackPositions
                .OrderByDescending(kvp => kvp.Value)
                .Take(MaxPositions)
                .Select(kvp => $"{kvp.Key}|{kvp.Value:F2}");

            File.WriteAllLines(_positionsFilePath, recentPositions);
        }
        catch { }
    }

    private void SaveCurrentPlaybackPosition()
    {
        if (_player == null || !_player.IsMediaLoaded || string.IsNullOrEmpty(_currentMediaPath))
            return;

        try
        {
            var position = _player.Position.TotalSeconds;
            var duration = _player.Duration.TotalSeconds;
            
            if (duration > 0 && position > 5 && position < duration * 0.95)
            {
                _playbackPositions[_currentMediaPath] = position;
            }
            else if (position >= duration * 0.95)
            {
                _playbackPositions.Remove(_currentMediaPath);
            }
        }
        catch { }
    }

    private async void RestorePlaybackPosition(string filePath)
    {
        if (_player == null || string.IsNullOrEmpty(filePath))
            return;

        if (_playbackPositions.TryGetValue(filePath, out double savedPosition) && savedPosition > 5)
        {
            await Task.Delay(500);
            
            if (_player.IsMediaLoaded && _player.Duration.TotalSeconds > 0)
            {
                if (savedPosition < _player.Duration.TotalSeconds * 0.95)
                {
                    _player.Seek(savedPosition);
                    
                    Title = $"NPV Player - Resumed at {FormatTime(savedPosition)}";
                    await Task.Delay(2000);
                    if (_player.IsMediaLoaded)
                        Title = $"NPV Player - {Path.GetFileName(filePath)}";
                }
            }
        }
    }

    private string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0 ? ts.ToString(@"hh\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    #endregion
}
