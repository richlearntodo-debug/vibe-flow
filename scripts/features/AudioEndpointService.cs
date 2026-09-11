using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

// Windows reports virtual-cable capture endpoints as LineLevel (form factor 2).
// Input methods that only list microphones or headsets (form factor 4/5) therefore
// hide CABLE Output, which is why some voice tools never see the remote audio.
// The endpoint property store is writable through Core Audio by a normal
// medium-integrity process, so the host can present CABLE Output as a microphone.
// The change does not touch audio routing and is fully reversible.
internal static class AudioEndpointShapePolicy
{
    internal const int FormFactorLineLevel = 2;
    internal const int FormFactorMicrophone = 4;

    // CABLE Output is the endpoint voice tools must record from. Sister endpoints
    // such as "CABLE In 16ch" or "CABLE Input" must never match.
    internal static bool IsVirtualCableCaptureName(string friendlyName)
    {
        if (string.IsNullOrWhiteSpace(friendlyName)) return false;
        string value = friendlyName.Trim();
        bool cable = value.IndexOf("CABLE", StringComparison.OrdinalIgnoreCase) >= 0;
        bool output = value.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0;
        return cable && output;
    }

    internal static bool NeedsMicrophoneShape(bool endpointFound, int currentFormFactor)
    {
        return endpointFound && currentFormFactor == FormFactorLineLevel;
    }

    internal static bool NeedsLineLevelShape(bool endpointFound, int currentFormFactor)
    {
        return endpointFound && currentFormFactor == FormFactorMicrophone;
    }
}

internal sealed class AudioEndpointShape
{
    internal bool Found;
    internal string EndpointId = "";
    internal string FriendlyName = "";
    internal int FormFactor = -1;

    internal bool IsMicrophoneShape
    {
        get { return FormFactor == AudioEndpointShapePolicy.FormFactorMicrophone; }
    }

    internal bool NeedsMicrophoneShape
    {
        get { return AudioEndpointShapePolicy.NeedsMicrophoneShape(Found, FormFactor); }
    }

    internal bool NeedsLineLevelShape
    {
        get { return AudioEndpointShapePolicy.NeedsLineLevelShape(Found, FormFactor); }
    }
}

internal static class AudioEndpointService
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
        int RegisterEndpointNotificationCallback(IntPtr client);
        int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        int GetCount(out int count);
        int Item(int index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        int Activate(ref Guid iid, int clsCtx, IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        int OpenPropertyStore(int access, out IPropertyStore properties);
        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        int GetState(out int state);
    }

    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out int count);
        int GetAt(int index, out PropertyKey key);
        int GetValue(ref PropertyKey key, [In, Out] ref PropVariant value);
        int SetValue(ref PropertyKey key, ref PropVariant value);
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    // Endpoint records can outlive the device instance: a stale entry with the same
    // friendly name returns AUDCLNT "invalidated" errors (0xE000020B). Active
    // endpoints are therefore searched first, and a failing candidate is skipped
    // instead of aborting the whole lookup.
    private static bool TryFindEndpoint(string friendlyNameFragment, int dataFlow, out IMMDevice device)
    {
        device = null;
        int[] stateMasks = { DeviceStateActive, DeviceStateAll };
        foreach (int mask in stateMasks)
        {
            IMMDeviceCollection collection = null;
            try
            {
                var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
                if (enumerator.EnumAudioEndpoints(dataFlow, mask, out collection) != 0 || collection == null) continue;
                int count;
                if (collection.GetCount(out count) != 0) count = 0;
                for (int index = 0; index < count; index++)
                {
                    IMMDevice candidate;
                    if (collection.Item(index, out candidate) != 0 || candidate == null) continue;
                    IPropertyStore store;
                    if (candidate.OpenPropertyStore(StorageRead, out store) != 0 || store == null) continue;
                    string friendlyName = ReadString(store, FriendlyNameKey);
                    if (friendlyName == null ||
                        friendlyName.IndexOf(friendlyNameFragment, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    device = candidate;
                    return true;
                }
            }
            catch (Exception)
            {
                // Fall through to the next mask (or report "not found").
            }
            finally
            {
                if (collection != null) Marshal.ReleaseComObject(collection);
            }
        }
        return false;
    }

    // Reading the endpoint level catches the classic "the driver was reinstalled and
    // the cable came back at a lower level or muted" regression: the recording kernel
    // measures its own output before Windows applies this level.
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        int RegisterControlChangeNotify(IntPtr notify);
        int UnregisterControlChangeNotify(IntPtr notify);
        int GetChannelCount(out int count);
        int SetMasterVolumeLevel(float level, ref Guid context);
        int SetMasterVolumeLevelScalar(float level, ref Guid context);
        int GetMasterVolumeLevel(out float level);
        int GetMasterVolumeLevelScalar(out float level);
        int SetChannelVolumeLevel(int channel, float level, ref Guid context);
        int SetChannelVolumeLevelScalar(int channel, float level, ref Guid context);
        int GetChannelVolumeLevel(int channel, out float level);
        int GetChannelVolumeLevelScalar(int channel, out float level);
        int SetMute(bool mute, ref Guid context);
        int GetMute(out bool mute);
    }

    // x64 layout: VARTYPE(2) + reserved(6) + union at offset 8.
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public int Int32Value;
        [FieldOffset(8)] public IntPtr PointerValue;
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    private const int DataFlowCapture = 1;
    private const int DeviceStateActive = 1;
    private const int DeviceStateAll = 15;
    private const int ClassContextAll = 23;
    private static readonly Guid AudioEndpointVolumeIid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
    private static readonly Guid AudioClientIid = new Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");
    private static readonly Guid AudioSessionManager2Iid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
    private const int StorageRead = 0;
    private const int StorageReadWrite = 2;
    private const ushort VariantTypeUi4 = 19;
    private const ushort VariantTypeLpwStr = 31;

    private static readonly PropertyKey FormFactorKey = new PropertyKey
    {
        FormatId = new Guid("1DA5D803-D492-4EDD-8C23-E0C0FFEE7F0E"),
        PropertyId = 0
    };

    private static readonly PropertyKey FriendlyNameKey = new PropertyKey
    {
        FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        PropertyId = 14
    };

    internal static bool TryReadVirtualCableShape(out AudioEndpointShape shape, out string errorCode)
    {
        shape = new AudioEndpointShape();
        errorCode = "";
        IMMDeviceCollection collection = null;
        try
        {
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            int hr = enumerator.EnumAudioEndpoints(DataFlowCapture, DeviceStateActive, out collection);
            if (hr != 0)
            {
                errorCode = "ENDPOINT-ENUMERATE-FAILED";
                return false;
            }
            int count;
            if (collection.GetCount(out count) != 0) count = 0;
            for (int index = 0; index < count; index++)
            {
                IMMDevice device;
                if (collection.Item(index, out device) != 0 || device == null) continue;
                string id;
                if (device.GetId(out id) != 0) continue;
                IPropertyStore store;
                if (device.OpenPropertyStore(StorageRead, out store) != 0 || store == null) continue;
                string friendlyName = ReadString(store, FriendlyNameKey);
                if (!AudioEndpointShapePolicy.IsVirtualCableCaptureName(friendlyName)) continue;
                int formFactor = ReadInt32(store, FormFactorKey);
                shape.Found = true;
                shape.EndpointId = id ?? "";
                shape.FriendlyName = friendlyName ?? "";
                shape.FormFactor = formFactor;
                return true;
            }
            errorCode = "ENDPOINT-NOT-FOUND";
            return false;
        }
        catch (Exception)
        {
            errorCode = "ENDPOINT-READ-FAILED";
            return false;
        }
        finally
        {
            if (collection != null) Marshal.ReleaseComObject(collection);
        }
    }

    internal static bool TrySetFormFactor(string endpointId, int formFactor, out string errorCode)
    {
        errorCode = "";
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            errorCode = "ENDPOINT-ID-MISSING";
            return false;
        }
        IMMDevice device = null;
        try
        {
            var enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDevice(endpointId, out device) != 0 || device == null)
            {
                errorCode = "ENDPOINT-OPEN-FAILED";
                return false;
            }
            IPropertyStore store;
            int hr = device.OpenPropertyStore(StorageReadWrite, out store);
            if (hr != 0 || store == null)
            {
                errorCode = "ENDPOINT-STORE-READONLY";
                return false;
            }
            PropertyKey key = FormFactorKey;
            var value = new PropVariant { VariantType = VariantTypeUi4, Int32Value = formFactor };
            hr = store.SetValue(ref key, ref value);
            if (hr != 0)
            {
                errorCode = "ENDPOINT-SET-FAILED";
                return false;
            }
            hr = store.Commit();
            if (hr != 0)
            {
                errorCode = "ENDPOINT-COMMIT-FAILED";
                return false;
            }
            return true;
        }
        catch (Exception)
        {
            errorCode = "ENDPOINT-WRITE-FAILED";
            return false;
        }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    internal static bool TryReadEndpointLevel(string friendlyNameFragment, int dataFlow,
        out double levelScalar, out bool muted, out string errorCode)
    {
        levelScalar = -1;
        muted = false;
        errorCode = "";
        IMMDevice device = null;
        try
        {
            if (!TryFindEndpoint(friendlyNameFragment, dataFlow, out device))
            {
                errorCode = "LEVEL-ENDPOINT-NOT-FOUND";
                return false;
            }
            Guid iid = AudioEndpointVolumeIid;
            object raw;
            if (device.Activate(ref iid, ClassContextAll, IntPtr.Zero, out raw) != 0 || raw == null)
            {
                errorCode = "LEVEL-ACTIVATE-FAILED";
                return false;
            }
            var volume = (IAudioEndpointVolume)raw;
            float scalar;
            if (volume.GetMasterVolumeLevelScalar(out scalar) != 0)
            {
                errorCode = "LEVEL-READ-FAILED";
                return false;
            }
            bool isMuted;
            if (volume.GetMute(out isMuted) != 0) isMuted = false;
            levelScalar = scalar;
            muted = isMuted;
            return true;
        }
        catch (Exception ex)
        {
            errorCode = "LEVEL-READ-" + ex.GetType().Name +
                (ex.HResult != 0 ? ":0x" + ex.HResult.ToString("X8") : "");
            return false;
        }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    // The kernel renders 48 kHz stereo 16-bit into CABLE Input and the tool records
    // CABLE Output; if a driver reinstall resets an endpoint to another mix format,
    // Windows resamples the stream and recording quality drops.
    [Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        int Initialize(int shareMode, int streamFlags, long bufferDuration, long periodicity,
            IntPtr format, IntPtr sessionGuid);
        int GetBufferSize(out int frames);
        int GetStreamLatency(out long latency);
        int GetCurrentPadding(out int frames);
        int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closestMatch);
        int GetMixFormat(out IntPtr format);
        int GetDevicePeriod(out long defaultPeriod, out long minimumPeriod);
        int Start();
        int Stop();
        int Reset();
        int SetEventHandle(IntPtr handle);
        int GetService(ref Guid iid, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    internal static bool TryReadEndpointFormat(string friendlyNameFragment, int dataFlow,
        out int sampleRate, out int channels, out int bits, out string errorCode)
    {
        sampleRate = 0;
        channels = 0;
        bits = 0;
        errorCode = "";
        IMMDevice device = null;
        try
        {
            if (!TryFindEndpoint(friendlyNameFragment, dataFlow, out device))
            {
                errorCode = "FORMAT-ENDPOINT-NOT-FOUND";
                return false;
            }
            Guid iid = AudioClientIid;
            object raw;
            if (device.Activate(ref iid, ClassContextAll, IntPtr.Zero, out raw) != 0 || raw == null)
            {
                errorCode = "FORMAT-ACTIVATE-FAILED";
                return false;
            }
            var client = (IAudioClient)raw;
            IntPtr format;
            if (client.GetMixFormat(out format) != 0 || format == IntPtr.Zero)
            {
                errorCode = "FORMAT-READ-FAILED";
                return false;
            }
            try
            {
                sampleRate = Marshal.ReadInt32(format, 4);
                channels = Marshal.ReadInt16(format, 2);
                bits = Marshal.ReadInt16(format, 14);
                return true;
            }
            finally
            {
                Marshal.FreeCoTaskMem(format);
            }
        }
        catch (Exception ex)
        {
            errorCode = "FORMAT-READ-" + ex.GetType().Name +
                (ex.HResult != 0 ? ":0x" + ex.HResult.ToString("X8") : "");
            return false;
        }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    internal static bool TrySetEndpointLevel(string friendlyNameFragment, int dataFlow, double levelScalar,
        bool muted, out string errorCode)
    {
        errorCode = "";
        IMMDevice device = null;
        try
        {
            if (!TryFindEndpoint(friendlyNameFragment, dataFlow, out device))
            {
                errorCode = "LEVEL-ENDPOINT-NOT-FOUND";
                return false;
            }
            Guid iid = AudioEndpointVolumeIid;
            object raw;
            if (device.Activate(ref iid, ClassContextAll, IntPtr.Zero, out raw) != 0 || raw == null)
            {
                errorCode = "LEVEL-ACTIVATE-FAILED";
                return false;
            }
            var volume = (IAudioEndpointVolume)raw;
            Guid context = Guid.Empty;
            float clamped = (float)Math.Max(0.0, Math.Min(1.0, levelScalar));
            if (volume.SetMasterVolumeLevelScalar(clamped, ref context) != 0)
            {
                errorCode = "LEVEL-WRITE-FAILED";
                return false;
            }
            if (volume.SetMute(muted, ref context) != 0)
            {
                errorCode = "LEVEL-MUTE-WRITE-FAILED";
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            errorCode = "LEVEL-WRITE-" + ex.GetType().Name +
                (ex.HResult != 0 ? ":0x" + ex.HResult.ToString("X8") : "");
            return false;
        }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    // Which processes actually hold a capture session on an endpoint is the only way
    // to verify the assumption the whole product rests on: that the configured voice
    // tool really records from CABLE Output instead of its own microphone pick.
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        int GetAudioSessionControl(ref Guid sessionGuid, int streamFlags, out IntPtr sessionControl);
        int GetSimpleAudioVolume(ref Guid sessionGuid, int streamFlags, out IntPtr audioVolume);
        int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        int RegisterSessionNotification(IntPtr notification);
        int UnregisterSessionNotification(IntPtr notification);
        int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionId, IntPtr notification);
        int UnregisterDuckNotification(IntPtr notification);
    }

    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int count);
        int GetSession(int index, out IAudioSessionControl session);
    }

    [Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        int GetGroupingParam(out Guid grouping);
        int SetGroupingParam(ref Guid grouping, ref Guid context);
        int RegisterAudioSessionNotification(IntPtr notification);
        int UnregisterAudioSessionNotification(IntPtr notification);
    }

    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        int GetState(out int state);
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, ref Guid context);
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string path, ref Guid context);
        int GetGroupingParam(out Guid grouping);
        int SetGroupingParam(ref Guid grouping, ref Guid context);
        int RegisterAudioSessionNotification(IntPtr notification);
        int UnregisterAudioSessionNotification(IntPtr notification);
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string identifier);
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string instanceIdentifier);
        int GetProcessId(out int processId);
        int IsSystemSoundsSession();
        int SetDuckingPreference(bool optOut);
    }

    internal static bool TryListCaptureSessions(string friendlyNameFragment, int dataFlow,
        out List<string> sessionProcessNames, out string errorCode)
    {
        sessionProcessNames = new List<string>();
        errorCode = "";
        IMMDevice device = null;
        try
        {
            if (!TryFindEndpoint(friendlyNameFragment, dataFlow, out device))
            {
                errorCode = "SESSION-ENDPOINT-NOT-FOUND";
                return false;
            }
            Guid iid = AudioSessionManager2Iid;
            object rawManager;
            if (device.Activate(ref iid, ClassContextAll, IntPtr.Zero, out rawManager) != 0 || rawManager == null)
            {
                errorCode = "SESSION-MANAGER-UNAVAILABLE";
                return false;
            }
            var manager = (IAudioSessionManager2)rawManager;
            IAudioSessionEnumerator sessions;
            if (manager.GetSessionEnumerator(out sessions) != 0 || sessions == null)
            {
                errorCode = "SESSION-ENUMERATE-FAILED";
                return false;
            }
            int count;
            if (sessions.GetCount(out count) != 0) count = 0;
            for (int index = 0; index < count; index++)
            {
                IAudioSessionControl control;
                if (sessions.GetSession(index, out control) != 0 || control == null) continue;
                try
                {
                    var control2 = control as IAudioSessionControl2;
                    if (control2 == null) continue;
                    int processId;
                    if (control2.GetProcessId(out processId) != 0 || processId <= 0) continue;
                    string processName;
                    try { processName = System.Diagnostics.Process.GetProcessById(processId).ProcessName; }
                    catch { processName = "pid:" + processId; }
                    int state;
                    control2.GetState(out state);
                    string entry = processName + (state == 1 ? " (active)" : state == 2 ? " (inactive)" : "");
                    if (!sessionProcessNames.Contains(entry)) sessionProcessNames.Add(entry);
                }
                finally
                {
                    Marshal.ReleaseComObject(control);
                }
            }
            Marshal.ReleaseComObject(sessions);
            return true;
        }
        catch (Exception ex)
        {
            errorCode = "SESSION-READ-" + ex.GetType().Name +
                (ex.HResult != 0 ? ":0x" + ex.HResult.ToString("X8") : "");
            return false;
        }
        finally
        {
            if (device != null) Marshal.ReleaseComObject(device);
        }
    }

    private static int ReadInt32(IPropertyStore store, PropertyKey key)
    {
        var value = new PropVariant();
        try
        {
            PropertyKey local = key;
            if (store.GetValue(ref local, ref value) != 0) return -1;
            if (value.VariantType != VariantTypeUi4) return -1;
            return value.Int32Value;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    private static string ReadString(IPropertyStore store, PropertyKey key)
    {
        var value = new PropVariant();
        try
        {
            PropertyKey local = key;
            if (store.GetValue(ref local, ref value) != 0) return "";
            if (value.VariantType != VariantTypeLpwStr || value.PointerValue == IntPtr.Zero) return "";
            return Marshal.PtrToStringUni(value.PointerValue) ?? "";
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }
}
