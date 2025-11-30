using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NpvPlayer;

/// <summary>
/// Information about a media track (audio, subtitle, video)
/// </summary>
public class TrackInfo
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string? Language { get; set; }
    public string? Codec { get; set; }
    public bool IsSelected { get; set; }

    public string DisplayName
    {
        get
        {
            var parts = new List<string>();
            parts.Add($"Track {Id}");
            
            if (!string.IsNullOrEmpty(Language))
                parts.Add($"[{Language.ToUpper()}]");
            
            if (!string.IsNullOrEmpty(Title))
                parts.Add(Title);
            else if (!string.IsNullOrEmpty(Codec))
                parts.Add($"({Codec})");

            return string.Join(" - ", parts);
        }
    }
}

/// <summary>
/// High-level wrapper for MPV player functionality
/// </summary>
public class MpvPlayerWrapper : IDisposable
{
    private IntPtr _mpvHandle = IntPtr.Zero;
    private bool _disposed = false;
    private CancellationTokenSource? _eventLoopCts;
    private Task? _eventLoopTask;
    private readonly IntPtr _windowHandle;

    // Events
    public event EventHandler? MediaLoaded;
    public event EventHandler? MediaFinished;
#pragma warning disable CS0067 // Event is never used - kept for future extensibility
    public event EventHandler? MediaError;
    public event EventHandler<double>? PositionChanged;
#pragma warning restore CS0067

    // Properties
    public bool IsInitialized => _mpvHandle != IntPtr.Zero;
    public bool IsMediaLoaded { get; private set; }
    public bool IsPaused { get; private set; }

    public TimeSpan Position
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return TimeSpan.Zero;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "time-pos", MpvInterop.MpvFormat.Double, out double pos);
                if (result == MpvInterop.MpvError.Success)
                    return TimeSpan.FromSeconds(pos);
            }
            catch { }
            return TimeSpan.Zero;
        }
        set
        {
            if (!IsInitialized || !IsMediaLoaded) return;
            var seconds = value.TotalSeconds;
            MpvInterop.mpv_set_property(_mpvHandle, "time-pos", MpvInterop.MpvFormat.Double, ref seconds);
        }
    }

    public TimeSpan Duration
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return TimeSpan.Zero;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "duration", MpvInterop.MpvFormat.Double, out double dur);
                if (result == MpvInterop.MpvError.Success)
                    return TimeSpan.FromSeconds(dur);
            }
            catch { }
            return TimeSpan.Zero;
        }
    }

    public int Volume
    {
        get
        {
            if (!IsInitialized) return 100;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "volume", MpvInterop.MpvFormat.Double, out double vol);
                if (result == MpvInterop.MpvError.Success)
                    return (int)vol;
            }
            catch { }
            return 100;
        }
        set
        {
            if (!IsInitialized) return;
            double vol = Math.Clamp(value, 0, 100);
            MpvInterop.mpv_set_property(_mpvHandle, "volume", MpvInterop.MpvFormat.Double, ref vol);
        }
    }

    public MpvPlayerWrapper(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public void Initialize()
    {
        if (IsInitialized) return;

        // Set DLL search path
        SetDllDirectory();

        _mpvHandle = MpvInterop.mpv_create();
        if (_mpvHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create MPV instance. Make sure libmpv-2.dll is available.");
        }

        // Set window ID for video output
        long wid = _windowHandle.ToInt64();
        MpvInterop.mpv_set_option(_mpvHandle, "wid", MpvInterop.MpvFormat.Int64, ref wid);

        // Set some default options
        MpvInterop.mpv_set_option_string(_mpvHandle, "vo", "gpu");
        MpvInterop.mpv_set_option_string(_mpvHandle, "hwdec", "auto");
        MpvInterop.mpv_set_option_string(_mpvHandle, "keep-open", "yes");
        MpvInterop.mpv_set_option_string(_mpvHandle, "idle", "yes");
        
        // Disable MPV's on-screen controller (we have our own controls)
        MpvInterop.mpv_set_option_string(_mpvHandle, "osc", "no");
        MpvInterop.mpv_set_option_string(_mpvHandle, "osd-level", "0");

        var error = MpvInterop.mpv_initialize(_mpvHandle);
        if (error != MpvInterop.MpvError.Success)
        {
            MpvInterop.mpv_terminate_destroy(_mpvHandle);
            _mpvHandle = IntPtr.Zero;
            throw new InvalidOperationException($"Failed to initialize MPV: {error}");
        }

        // Start event loop
        StartEventLoop();
    }

    private void SetDllDirectory()
    {
        // Try to set up DLL search path
        string[] searchPaths = new[]
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib"),
            Environment.CurrentDirectory,
            Path.Combine(Environment.CurrentDirectory, "lib")
        };

        foreach (var path in searchPaths)
        {
            var dllPath = Path.Combine(path, "libmpv-2.dll");
            if (File.Exists(dllPath))
            {
                SetDllDirectoryW(path);
                break;
            }
            // Also check for mpv-2.dll
            dllPath = Path.Combine(path, "mpv-2.dll");
            if (File.Exists(dllPath))
            {
                SetDllDirectoryW(path);
                break;
            }
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectoryW(string lpPathName);

    private void StartEventLoop()
    {
        _eventLoopCts = new CancellationTokenSource();
        _eventLoopTask = Task.Run(() => EventLoop(_eventLoopCts.Token));
    }

    private void EventLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && IsInitialized)
        {
            try
            {
                var eventPtr = MpvInterop.mpv_wait_event(_mpvHandle, 0.1);
                if (eventPtr == IntPtr.Zero) continue;

                var mpvEvent = Marshal.PtrToStructure<MpvInterop.MpvEvent>(eventPtr);

                switch (mpvEvent.EventId)
                {
                    case MpvInterop.MpvEventId.FileLoaded:
                        IsMediaLoaded = true;
                        IsPaused = false;
                        MediaLoaded?.Invoke(this, EventArgs.Empty);
                        break;

                    case MpvInterop.MpvEventId.EndFile:
                        IsMediaLoaded = false;
                        MediaFinished?.Invoke(this, EventArgs.Empty);
                        break;

                    case MpvInterop.MpvEventId.Shutdown:
                        return;

                    case MpvInterop.MpvEventId.Idle:
                        // Player is idle
                        break;
                }
            }
            catch (Exception)
            {
                // Ignore event loop errors
            }
        }
    }

    public void Load(string filePath)
    {
        if (!IsInitialized)
            throw new InvalidOperationException("MPV is not initialized.");

        // Use loadfile command
        var command = $"loadfile \"{filePath.Replace("\\", "/")}\"";
        var result = MpvInterop.mpv_command_string(_mpvHandle, command);
        
        if (result != MpvInterop.MpvError.Success)
        {
            throw new InvalidOperationException($"Failed to load file: {result}");
        }
    }

    public void Play()
    {
        if (!IsInitialized) return;
        long pause = 0;
        MpvInterop.mpv_set_property(_mpvHandle, "pause", MpvInterop.MpvFormat.Flag, ref pause);
        IsPaused = false;
    }

    public void Pause()
    {
        if (!IsInitialized) return;
        long pause = 1;
        MpvInterop.mpv_set_property(_mpvHandle, "pause", MpvInterop.MpvFormat.Flag, ref pause);
        IsPaused = true;
    }

    public void Resume()
    {
        Play();
    }

    public void Stop()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "stop");
        IsMediaLoaded = false;
        IsPaused = false;
    }

    public void Seek(double seconds)
    {
        if (!IsInitialized || !IsMediaLoaded) return;
        var command = $"seek {seconds} absolute";
        MpvInterop.mpv_command_string(_mpvHandle, command);
    }

    public void SeekRelative(double seconds)
    {
        if (!IsInitialized || !IsMediaLoaded) return;
        var command = $"seek {seconds} relative";
        MpvInterop.mpv_command_string(_mpvHandle, command);
    }

    public void FrameStep()
    {
        if (!IsInitialized || !IsMediaLoaded) return;
        MpvInterop.mpv_command_string(_mpvHandle, "frame-step");
    }

    public void FrameBackStep()
    {
        if (!IsInitialized || !IsMediaLoaded) return;
        MpvInterop.mpv_command_string(_mpvHandle, "frame-back-step");
    }

    public double Speed
    {
        get
        {
            if (!IsInitialized) return 1.0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "speed", MpvInterop.MpvFormat.Double, out double speed);
                if (result == MpvInterop.MpvError.Success)
                    return speed;
            }
            catch { }
            return 1.0;
        }
        set
        {
            if (!IsInitialized) return;
            double speed = Math.Clamp(value, 0.25, 4.0);
            MpvInterop.mpv_set_property(_mpvHandle, "speed", MpvInterop.MpvFormat.Double, ref speed);
        }
    }

    public bool Muted
    {
        get
        {
            if (!IsInitialized) return false;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "mute", MpvInterop.MpvFormat.Flag, out long muted);
                if (result == MpvInterop.MpvError.Success)
                    return muted != 0;
            }
            catch { }
            return false;
        }
        set
        {
            if (!IsInitialized) return;
            long muted = value ? 1 : 0;
            MpvInterop.mpv_set_property(_mpvHandle, "mute", MpvInterop.MpvFormat.Flag, ref muted);
        }
    }

    public int AudioTrack
    {
        get
        {
            if (!IsInitialized) return 1;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "aid", MpvInterop.MpvFormat.Int64, out long aid);
                if (result == MpvInterop.MpvError.Success)
                    return (int)aid;
            }
            catch { }
            return 1;
        }
        set
        {
            if (!IsInitialized) return;
            long aid = value;
            MpvInterop.mpv_set_property(_mpvHandle, "aid", MpvInterop.MpvFormat.Int64, ref aid);
        }
    }

    public int SubtitleTrack
    {
        get
        {
            if (!IsInitialized) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "sid", MpvInterop.MpvFormat.Int64, out long sid);
                if (result == MpvInterop.MpvError.Success)
                    return (int)sid;
            }
            catch { }
            return 0;
        }
        set
        {
            if (!IsInitialized) return;
            long sid = value;
            MpvInterop.mpv_set_property(_mpvHandle, "sid", MpvInterop.MpvFormat.Int64, ref sid);
        }
    }

    public void SetSubtitleTrack(int trackId)
    {
        if (!IsInitialized) return;
        if (trackId == 0)
        {
            MpvInterop.mpv_set_property_string(_mpvHandle, "sid", "no");
        }
        else
        {
            long sid = trackId;
            MpvInterop.mpv_set_property(_mpvHandle, "sid", MpvInterop.MpvFormat.Int64, ref sid);
        }
    }

    public void SetAudioTrack(int trackId)
    {
        if (!IsInitialized) return;
        if (trackId == 0)
        {
            MpvInterop.mpv_set_property_string(_mpvHandle, "aid", "no");
        }
        else
        {
            long aid = trackId;
            MpvInterop.mpv_set_property(_mpvHandle, "aid", MpvInterop.MpvFormat.Int64, ref aid);
        }
    }

    public List<TrackInfo> GetSubtitleTracks()
    {
        return GetTracks("sub");
    }

    public List<TrackInfo> GetAudioTracks()
    {
        return GetTracks("audio");
    }

    private List<TrackInfo> GetTracks(string type)
    {
        var tracks = new List<TrackInfo>();
        if (!IsInitialized || !IsMediaLoaded) return tracks;

        try
        {
            // Get track count
            var countResult = MpvInterop.mpv_get_property(_mpvHandle, "track-list/count", MpvInterop.MpvFormat.Int64, out long count);
            if (countResult != MpvInterop.MpvError.Success) return tracks;

            for (int i = 0; i < count; i++)
            {
                // Get track type
                var typePtr = MpvInterop.mpv_get_property_string(_mpvHandle, $"track-list/{i}/type");
                if (typePtr == IntPtr.Zero) continue;
                var trackType = Marshal.PtrToStringUTF8(typePtr);
                MpvInterop.mpv_free(typePtr);

                if (trackType != type) continue;

                // Get track ID
                MpvInterop.mpv_get_property(_mpvHandle, $"track-list/{i}/id", MpvInterop.MpvFormat.Int64, out long id);

                // Get track title (may be null)
                string? title = null;
                var titlePtr = MpvInterop.mpv_get_property_string(_mpvHandle, $"track-list/{i}/title");
                if (titlePtr != IntPtr.Zero)
                {
                    title = Marshal.PtrToStringUTF8(titlePtr);
                    MpvInterop.mpv_free(titlePtr);
                }

                // Get track language (may be null)
                string? lang = null;
                var langPtr = MpvInterop.mpv_get_property_string(_mpvHandle, $"track-list/{i}/lang");
                if (langPtr != IntPtr.Zero)
                {
                    lang = Marshal.PtrToStringUTF8(langPtr);
                    MpvInterop.mpv_free(langPtr);
                }

                // Get codec
                string? codec = null;
                var codecPtr = MpvInterop.mpv_get_property_string(_mpvHandle, $"track-list/{i}/codec");
                if (codecPtr != IntPtr.Zero)
                {
                    codec = Marshal.PtrToStringUTF8(codecPtr);
                    MpvInterop.mpv_free(codecPtr);
                }

                // Check if selected
                MpvInterop.mpv_get_property(_mpvHandle, $"track-list/{i}/selected", MpvInterop.MpvFormat.Flag, out long selected);

                tracks.Add(new TrackInfo
                {
                    Id = (int)id,
                    Title = title,
                    Language = lang,
                    Codec = codec,
                    IsSelected = selected != 0
                });
            }
        }
        catch { }

        return tracks;
    }

    public void CycleAudioTrack()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "cycle audio");
    }

    public void CycleSubtitleTrack()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "cycle sub");
    }

    public void ToggleSubtitles()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "cycle sub-visibility");
    }

    public void LoadSubtitleFile(string filePath)
    {
        if (!IsInitialized) return;
        var command = $"sub-add \"{filePath.Replace("\\", "/")}\"";
        MpvInterop.mpv_command_string(_mpvHandle, command);
    }

    public void LoadAudioFile(string filePath)
    {
        if (!IsInitialized) return;
        var command = $"audio-add \"{filePath.Replace("\\", "/")}\"";
        MpvInterop.mpv_command_string(_mpvHandle, command);
    }

    public int SubtitleDelay
    {
        get
        {
            if (!IsInitialized) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "sub-delay", MpvInterop.MpvFormat.Double, out double delay);
                if (result == MpvInterop.MpvError.Success)
                    return (int)(delay * 1000);
            }
            catch { }
            return 0;
        }
        set
        {
            if (!IsInitialized) return;
            double delay = value / 1000.0;
            MpvInterop.mpv_set_property(_mpvHandle, "sub-delay", MpvInterop.MpvFormat.Double, ref delay);
        }
    }

    public int AudioDelay
    {
        get
        {
            if (!IsInitialized) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "audio-delay", MpvInterop.MpvFormat.Double, out double delay);
                if (result == MpvInterop.MpvError.Success)
                    return (int)(delay * 1000);
            }
            catch { }
            return 0;
        }
        set
        {
            if (!IsInitialized) return;
            double delay = value / 1000.0;
            MpvInterop.mpv_set_property(_mpvHandle, "audio-delay", MpvInterop.MpvFormat.Double, ref delay);
        }
    }

    public void SetAspectRatio(string ratio)
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_set_property_string(_mpvHandle, "video-aspect-override", ratio);
    }

    public void ResetAspectRatio()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_set_property_string(_mpvHandle, "video-aspect-override", "-1");
    }

    public void SetVideoZoom(double zoom)
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_set_property(_mpvHandle, "video-zoom", MpvInterop.MpvFormat.Double, ref zoom);
    }

    public void RotateVideo(int degrees)
    {
        if (!IsInitialized) return;
        long rotation = degrees;
        MpvInterop.mpv_set_property(_mpvHandle, "video-rotate", MpvInterop.MpvFormat.Int64, ref rotation);
    }

    public void TakeScreenshot()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "screenshot");
    }

    public void TakeScreenshotToClipboard()
    {
        if (!IsInitialized) return;
        MpvInterop.mpv_command_string(_mpvHandle, "screenshot-to-file - png");
    }

    public bool Loop
    {
        get
        {
            if (!IsInitialized) return false;
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, "loop-file");
            if (ptr != IntPtr.Zero)
            {
                var value = Marshal.PtrToStringUTF8(ptr);
                MpvInterop.mpv_free(ptr);
                return value == "inf";
            }
            return false;
        }
        set
        {
            if (!IsInitialized) return;
            MpvInterop.mpv_set_property_string(_mpvHandle, "loop-file", value ? "inf" : "no");
        }
    }

    public string? CurrentFilePath
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return null;
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, "path");
            if (ptr != IntPtr.Zero)
            {
                var value = Marshal.PtrToStringUTF8(ptr);
                MpvInterop.mpv_free(ptr);
                return value;
            }
            return null;
        }
    }

    public string? MediaTitle
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return null;
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, "media-title");
            if (ptr != IntPtr.Zero)
            {
                var value = Marshal.PtrToStringUTF8(ptr);
                MpvInterop.mpv_free(ptr);
                return value;
            }
            return null;
        }
    }

    public string? VideoCodec
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return null;
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, "video-codec");
            if (ptr != IntPtr.Zero)
            {
                var value = Marshal.PtrToStringUTF8(ptr);
                MpvInterop.mpv_free(ptr);
                return value;
            }
            return null;
        }
    }

    public string? AudioCodec
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return null;
            var ptr = MpvInterop.mpv_get_property_string(_mpvHandle, "audio-codec-name");
            if (ptr != IntPtr.Zero)
            {
                var value = Marshal.PtrToStringUTF8(ptr);
                MpvInterop.mpv_free(ptr);
                return value;
            }
            return null;
        }
    }

    public int VideoWidth
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "video-params/w", MpvInterop.MpvFormat.Int64, out long width);
                if (result == MpvInterop.MpvError.Success)
                    return (int)width;
            }
            catch { }
            return 0;
        }
    }

    public int VideoHeight
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "video-params/h", MpvInterop.MpvFormat.Int64, out long height);
                if (result == MpvInterop.MpvError.Success)
                    return (int)height;
            }
            catch { }
            return 0;
        }
    }

    public double FPS
    {
        get
        {
            if (!IsInitialized || !IsMediaLoaded) return 0;
            try
            {
                var result = MpvInterop.mpv_get_property(_mpvHandle, "container-fps", MpvInterop.MpvFormat.Double, out double fps);
                if (result == MpvInterop.MpvError.Success)
                    return fps;
            }
            catch { }
            return 0;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Stop event loop
        _eventLoopCts?.Cancel();
        _eventLoopTask?.Wait(1000);
        _eventLoopCts?.Dispose();

        // Destroy MPV
        if (_mpvHandle != IntPtr.Zero)
        {
            MpvInterop.mpv_terminate_destroy(_mpvHandle);
            _mpvHandle = IntPtr.Zero;
        }
    }
}
