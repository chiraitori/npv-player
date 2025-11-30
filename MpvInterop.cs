using System;
using System.Runtime.InteropServices;

namespace NpvPlayer;

/// <summary>
/// P/Invoke bindings for libmpv (mpv-2.dll or mpv-1.dll)
/// </summary>
public static class MpvInterop
{
    private const string LibMpv = "libmpv-2";

    public enum MpvError
    {
        Success = 0,
        EventQueueFull = -1,
        NoMem = -2,
        Uninitialized = -3,
        InvalidParameter = -4,
        OptionNotFound = -5,
        OptionFormat = -6,
        OptionError = -7,
        PropertyNotFound = -8,
        PropertyFormat = -9,
        PropertyUnavailable = -10,
        PropertyError = -11,
        Command = -12,
        LoadingFailed = -13,
        AoInitFailed = -14,
        VoInitFailed = -15,
        NothingToPlay = -16,
        UnknownFormat = -17,
        Unsupported = -18,
        NotImplemented = -19,
        Generic = -20
    }

    public enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
        NodeArray = 7,
        NodeMap = 8,
        ByteArray = 9
    }

    public enum MpvEventId
    {
        None = 0,
        Shutdown = 1,
        LogMessage = 2,
        GetPropertyReply = 3,
        SetPropertyReply = 4,
        CommandReply = 5,
        StartFile = 6,
        EndFile = 7,
        FileLoaded = 8,
        Idle = 11,
        Tick = 14,
        ClientMessage = 16,
        VideoReconfig = 17,
        AudioReconfig = 18,
        Seek = 20,
        PlaybackRestart = 21,
        PropertyChange = 22,
        QueueOverflow = 24,
        Hook = 25
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvEvent
    {
        public MpvEventId EventId;
        public int Error;
        public ulong ReplyUserdata;
        public IntPtr Data;
    }

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_initialize(IntPtr ctx);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_command(IntPtr ctx, IntPtr[] args);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_command_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string args);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_option(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, ref long data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_option_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, ref long data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, ref double data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_set_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, out double data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_get_property(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format, out long data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, [MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern MpvError mpv_observe_property(IntPtr ctx, ulong replyUserdata, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, MpvFormat format);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_error_string(MpvError error);

    [DllImport(LibMpv, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_client_api_version();
}
