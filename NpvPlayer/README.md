# NpvPlayer - MPV-based Video Player

A modern WPF video player powered by the MPV media player engine.

## Features

- **MPV-powered playback**: Supports virtually all video and audio formats
- **Hardware acceleration**: Automatic hardware decoding support
- **Keyboard shortcuts**: 
  - `Space` - Play/Pause toggle
  - `←` / `→` - Seek backward/forward 5 seconds
  - `↑` / `↓` - Volume up/down
  - `M` - Mute toggle
  - `F` - Toggle fullscreen
  - `Esc` - Exit fullscreen
  - `Ctrl+O` - Open file
- **Drag & Drop**: Drop media files directly onto the player
- **Dark theme UI**: Modern dark interface

## Setup Instructions

### 1. Download libmpv

1. Go to [mpv-player-windows libmpv releases](https://sourceforge.net/projects/mpv-player-windows/files/libmpv/)
2. Download the latest `mpv-dev-x86_64-*.7z` (for 64-bit) or `mpv-dev-i686-*.7z` (for 32-bit)
3. Extract the archive

### 2. Copy libmpv to your application

Copy `libmpv-2.dll` from the extracted archive to one of these locations:
- The same folder as `NpvPlayer.exe`
- A `lib` subfolder next to `NpvPlayer.exe`

For development, copy to:
- `bin\Debug\net9.0-windows\libmpv-2.dll`
- or `bin\Debug\net9.0-windows\lib\libmpv-2.dll`

### 3. Run the application

```bash
dotnet run
```

Or build and run the executable:
```bash
dotnet build
.\bin\Debug\net9.0-windows\NpvPlayer.exe
```

## Supported Formats

MPV supports a wide variety of formats including:
- **Video**: MP4, MKV, AVI, WebM, MOV, WMV, FLV, and many more
- **Audio**: MP3, FLAC, WAV, OGG, AAC, and many more
- **Subtitles**: SRT, ASS, SSA, SUB, and embedded subtitles

## Troubleshooting

### "Failed to initialize MPV player" error
- Make sure `libmpv-2.dll` is in the correct location
- Ensure you downloaded the correct architecture (x64 vs x86)
- Try placing the DLL directly next to the executable

### Video doesn't play
- Check if the file format is supported
- Try updating libmpv to the latest version
- Check the Windows Event Viewer for detailed error messages

## License

This project uses MPV which is licensed under GPL v2+. See [MPV License](https://github.com/mpv-player/mpv/blob/master/LICENSE.GPL) for details.
