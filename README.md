# NpvPlayer 🎬

A modern WPF video player powered by **libmpv** - VLC-style interface with anime-friendly features.

![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)
![WPF](https://img.shields.io/badge/WPF-Windows-0078D4?logo=windows)
![License](https://img.shields.io/badge/License-GPL--2.0-blue)

## ✨ Features

### 🎥 Playback
- **MPV-powered**: Supports virtually all video/audio formats (MP4, MKV, AVI, WebM, FLAC, MP3...)
- **Hardware acceleration**: Automatic GPU decoding
- **Playback position memory**: Resume where you left off
- **Auto-download libmpv**: No manual setup required!

### 🎮 Controls
- VLC-style dark interface
- Custom seek bar with timeline markers
- **Skip OP/ED buttons** - Perfect for anime! (default: skip 85s)
- Volume slider with mute toggle
- Fullscreen support

### ⌨️ Keyboard Shortcuts
| Key | Action |
|-----|--------|
| `Space` | Play/Pause |
| `←` / `→` | Seek ±5 seconds |
| `↑` / `↓` | Volume ±5% |
| `M` | Mute toggle |
| `F` | Fullscreen |
| `Esc` | Exit fullscreen |
| `Ctrl+O` | Open file |

### 📋 Menus
- **Media**: Open files, folders, URLs, capture devices, recent files
- **Playback**: Speed control, chapter/title navigation
- **Audio**: Track selection, stereo mode, audio delay
- **Video**: Aspect ratio, crop, zoom, deinterlace
- **Subtitle**: Track selection, add subtitle file, sync delay
- **Tools**: Preferences, media info
- **View**: Playlist panel, fullscreen

## 🚀 Getting Started

### Requirements
- Windows 10/11
- .NET 9.0 Runtime

### Run from source
```powershell
git clone https://github.com/chiraitori/npv-player.git
cd npv-player/NpvPlayer
dotnet run
```

On first run, the app will automatically download libmpv (~30MB).

### Build
```powershell
dotnet build -c Release
```

## 📁 Project Structure

```
NpvPlayer/
├── MainWindow.xaml          # UI layout
├── MainWindow.xaml.cs       # Core initialization
├── MainWindow.Player.cs     # MPV initialization & download
├── MainWindow.Menus.cs      # Menu handlers
├── MainWindow.Controls.cs   # Playback controls
├── MainWindow.Window.cs     # Fullscreen, keyboard, drag-drop
├── MainWindow.Data.cs       # Recent files, position memory
├── MpvPlayerWrapper.cs      # High-level MPV wrapper
└── MpvInterop.cs            # P/Invoke bindings
```

## 🔧 Tech Stack

- **Framework**: .NET 9.0 WPF
- **Player Engine**: libmpv via P/Invoke
- **7z Extraction**: SevenZipSharp + 7z.Libs (built-in)

## 📜 License

GPL-2.0 - This project uses MPV which is licensed under GPL v2+.

## 🙏 Credits

- [mpv-player](https://mpv.io/) - The best media player
- [mpv-player-windows](https://sourceforge.net/projects/mpv-player-windows/) - Windows builds of libmpv
