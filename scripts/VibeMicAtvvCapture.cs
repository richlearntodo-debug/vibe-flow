using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

internal sealed class VibeMicAtvvCapture
{
    private static readonly Guid ServiceUuid = new Guid("ab5e0001-5a21-4f05-bc7d-af01f617b664");
    private static readonly Guid WriteUuid = new Guid("ab5e0002-5a21-4f05-bc7d-af01f617b664");
    private static readonly Guid AudioUuid = new Guid("ab5e0003-5a21-4f05-bc7d-af01f617b664");
    private static readonly Guid ControlUuid = new Guid("ab5e0004-5a21-4f05-bc7d-af01f617b664");
    private static readonly object FileLock = new object();
    private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);
    private static string eventPath;
    private static string reportPath;
    private static string runtimeLogPath;
    private static GattCharacteristic writeCharacteristic;
    private static int audioCount;
    private static int controlCount;
    private static int micOpen;
    private static ushort protocolVersion = 0x0100;
    private static byte selectedCodec = 0x02;
    private static byte sessionId;
    private static readonly object StreamLock = new object();
    private static ImaAdpcmDecoder decoder;
    private static WaveOutSink audioSink;
    private static EventWaitHandle stopEvent;
    private static EventWaitHandle voiceKeyEvent;
    private static ManualResetEvent connectionLostEvent;
    private static string audioEndpointName = "CABLE Input";
    private static double audioGain = 1.0;
    private static int drainMs = 180;
    private static long lastStreamTicks;
    private static long lastAudioTicks;
    private static int streamPacketCount;
    private static int streamActive;
    private static double streamSquareSum;
    private static long streamSampleCount;
    private static int streamPeak;

    private static int Main(string[] args)
    {
        using (Mutex instanceMutex = new Mutex(false, "Local\\VibeMicAtvvCapture"))
        {
            bool ownsMutex = false;
            try
            {
                ownsMutex = instanceMutex.WaitOne(0, false);
            }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
            {
                Console.Error.WriteLine("Another Vibe Mic capture session is already running.");
                return 2;
            }
            try { return RunMain(args); }
            finally { try { instanceMutex.ReleaseMutex(); } catch { } }
        }
    }

    private static int RunMain(string[] args)
    {
        try
        {
            int seconds = args.Length > 0 ? int.Parse(args[0]) : 0;
            if (seconds < 0) seconds = 0;
            string outDir = args.Length > 1 ? args[1] : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "remote-voice-session");
            audioEndpointName = args.Length > 2 ? args[2] : "CABLE Input";
            if (args.Length > 3) double.TryParse(args[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out audioGain);
            if (audioGain <= 0 || audioGain > 4) audioGain = 1.0;
            if (args.Length > 4) int.TryParse(args[4], out drainMs);
            if (drainMs < 0 || drainMs > 2000) drainMs = 180;
            Directory.CreateDirectory(outDir);
            eventPath = Path.Combine(outDir, "remote-voice-events.jsonl");
            reportPath = Path.Combine(outDir, "remote-voice-report.json");
            runtimeLogPath = Path.Combine(outDir, "vibe-mic-runtime.log");
            if (File.Exists(eventPath)) File.Delete(eventPath);
            decoder = new ImaAdpcmDecoder(audioGain);
            audioSink = new WaveOutSink(audioEndpointName);
            stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicStopCapture");
            voiceKeyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceKeyPressed");
            connectionLostEvent = new ManualResetEvent(false);
            RuntimeLog("START endpoint=" + audioSink.DeviceName + " gain=" + audioGain.ToString("0.00") + " drain_ms=" + drainMs);
            try { RunAsync(seconds).GetAwaiter().GetResult(); }
            finally
            {
                WeChatHotkey.Release();
                if (audioSink != null) audioSink.Dispose();
                if (stopEvent != null) stopEvent.Dispose();
                if (voiceKeyEvent != null) voiceKeyEvent.Dispose();
                if (connectionLostEvent != null) connectionLostEvent.Dispose();
            }
            WriteReport("completed", "");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            RuntimeLog("ERROR " + ex.Message);
            if (!string.IsNullOrEmpty(reportPath)) WriteReport("error", ex.Message);
            return 1;
        }
    }

    private static async Task RunAsync(int seconds)
    {
        DeviceInformationCollection infos = await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelector()).AsTask();
        DeviceInformation candidate = infos
            .Where(i => Score(i.Name) > 0)
            .OrderByDescending(i => Score(i.Name))
            .FirstOrDefault();
        if (candidate == null) throw new InvalidOperationException("No RC003 BLE candidate found. Wake the remote and retry.");

        using (BluetoothLEDevice ble = await BluetoothLEDevice.FromIdAsync(candidate.Id).AsTask())
        {
            if (ble == null) throw new InvalidOperationException("BluetoothLEDevice.FromIdAsync returned null");
            Console.WriteLine("Device: " + candidate.Name);
            Console.WriteLine("Connection: " + ble.ConnectionStatus);
            RuntimeLog("BLE device=" + candidate.Name + " status=" + ble.ConnectionStatus);
            connectionLostEvent.Reset();
            ble.ConnectionStatusChanged += OnConnectionStatusChanged;

            try
            {
                GattDeviceServicesResult services = await ble.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached).AsTask();
                Console.WriteLine("ATVV service status: " + services.Status);
                if (services.Status != GattCommunicationStatus.Success || services.Services.Count == 0)
                    throw new InvalidOperationException("ATVV service not available: " + services.Status);

                using (GattDeviceService service = services.Services[0])
                {
                    GattCharacteristicsResult writes = await service.GetCharacteristicsForUuidAsync(WriteUuid, BluetoothCacheMode.Uncached).AsTask();
                    if (writes.Status != GattCommunicationStatus.Success || writes.Characteristics.Count == 0)
                        throw new InvalidOperationException("ATVV write characteristic not available: " + writes.Status);
                    writeCharacteristic = writes.Characteristics[0];

                    GattCharacteristic audio = await GetCharacteristic(service, AudioUuid);
                    GattCharacteristic control = await GetCharacteristic(service, ControlUuid);
                    audio.ValueChanged += OnValueChanged;
                    control.ValueChanged += OnValueChanged;
                    try
                    {
                        await EnableNotify(audio);
                        await EnableNotify(control);
                        await WriteCommand(new byte[] { 0x0A, 0x01, 0x00, 0x00, 0x03, 0x03 }, "get_caps_v10");
                        RuntimeLog("ATVV READY route=RC003_16k_to_" + audioSink.DeviceName + "_48k_stereo");
                        Console.WriteLine(seconds == 0 ? "Listening continuously. Hold the RC003 record button and speak." : "Listening for " + seconds + " seconds. Hold the RC003 record button and speak.");
                        Console.WriteLine("Audio route: RC003 16 kHz -> " + audioSink.DeviceName + " 48 kHz stereo");
                        await Task.Run(delegate { MonitorConnection(seconds); });
                    }
                    finally
                    {
                        if (Interlocked.CompareExchange(ref micOpen, 0, 1) == 1)
                        {
                            try { WriteCommand(CloseCommand(), "mic_close").GetAwaiter().GetResult(); } catch { }
                        }
                        try { audio.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None).AsTask().GetAwaiter().GetResult(); } catch { }
                        try { control.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None).AsTask().GetAwaiter().GetResult(); } catch { }
                        audio.ValueChanged -= OnValueChanged;
                        control.ValueChanged -= OnValueChanged;
                    }
                }
            }
            finally { ble.ConnectionStatusChanged -= OnConnectionStatusChanged; }
        }
    }

    private static void OnConnectionStatusChanged(BluetoothLEDevice sender, object args)
    {
        RuntimeLog("BLE status=" + sender.ConnectionStatus);
        if (sender.ConnectionStatus != BluetoothConnectionStatus.Connected && connectionLostEvent != null) connectionLostEvent.Set();
    }

    private static void MonitorConnection(int seconds)
    {
        int started = Environment.TickCount;
        WaitHandle[] handles = { stopEvent, connectionLostEvent, voiceKeyEvent };
        while (true)
        {
            int timeout = seconds == 0 ? Timeout.Infinite : Math.Max(0, seconds * 1000 - unchecked(Environment.TickCount - started));
            int signal = WaitHandle.WaitAny(handles, timeout);
            if (signal == WaitHandle.WaitTimeout || signal == 0) return;
            if (signal == 1) throw new IOException("RC003 Bluetooth voice connection was lost.");

            long pressedAt = DateTime.UtcNow.Ticks;
            RuntimeLog("VOICE KEY detected; waiting for ATVV stream");
            Thread.Sleep(1200);
            if (stopEvent.WaitOne(0)) return;
            long latest = Math.Max(Interlocked.Read(ref lastStreamTicks), Interlocked.Read(ref lastAudioTicks));
            if (latest < pressedAt - TimeSpan.FromMilliseconds(500).Ticks)
                throw new IOException("Voice key was pressed but no ATVV stream arrived; reconnect required.");
        }
    }

    private static async Task<GattCharacteristic> GetCharacteristic(GattDeviceService service, Guid uuid)
    {
        GattCharacteristicsResult result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached).AsTask();
        Console.WriteLine("Characteristic " + uuid + " status: " + result.Status);
        if (result.Status != GattCommunicationStatus.Success || result.Characteristics.Count == 0)
            throw new InvalidOperationException("ATVV characteristic not available: " + uuid);
        return result.Characteristics[0];
    }

    private static async Task EnableNotify(GattCharacteristic characteristic)
    {
        GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
            GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask();
        Console.WriteLine("Notify enable " + characteristic.Uuid + " => " + status);
        if (status != GattCommunicationStatus.Success)
            throw new InvalidOperationException("Could not enable notify for " + characteristic.Uuid + ": " + status);
    }

    private static async void OnValueChanged(GattCharacteristic sender, GattValueChangedEventArgs args)
    {
        try
        {
            byte[] bytes = new byte[args.CharacteristicValue.Length];
            using (DataReader reader = DataReader.FromBuffer(args.CharacteristicValue)) reader.ReadBytes(bytes);
            bool control = sender.Uuid == ControlUuid;
            int first = bytes.Length > 0 ? bytes[0] : -1;
            string type = control ? "remote_control" : "audio";
            string name = control ? ControlName(first) : "audio_packet";
            if (control) Interlocked.Increment(ref controlCount); else Interlocked.Increment(ref audioCount);
            AppendEvent(type, name, sender.Uuid, bytes);

            if (!control)
            {
                short[] decoded;
                lock (StreamLock)
                {
                    decoded = decoder.Decode(bytes);
                    if (Volatile.Read(ref streamActive) == 1)
                    {
                        streamPacketCount++;
                        foreach (short sample in decoded)
                        {
                            int absolute = sample == short.MinValue ? 32768 : Math.Abs((int)sample);
                            if (absolute > streamPeak) streamPeak = absolute;
                            streamSquareSum += (double)sample * sample;
                            streamSampleCount++;
                        }
                    }
                    audioSink.Write(decoded);
                }
                Interlocked.Exchange(ref lastAudioTicks, DateTime.UtcNow.Ticks);
                return;
            }

            if (first == 0x0B)
            {
                ParseCapabilities(bytes);
                Console.WriteLine("ATVV CAPS version=" + protocolVersion.ToString("X4") + " codec=" + selectedCodec);
            }
            else if (first == 0x08 && Interlocked.CompareExchange(ref micOpen, 1, 0) == 0)
            {
                WeChatHotkey.Hold();
                await WriteCommand(OpenCommand(), "mic_open");
            }
            else if (first == 0x04)
            {
                if (bytes.Length >= 4) sessionId = bytes[3];
                lock (StreamLock)
                {
                    decoder.Reset();
                    streamPacketCount = 0;
                    streamSquareSum = 0;
                    streamSampleCount = 0;
                    streamPeak = 0;
                    Volatile.Write(ref streamActive, 1);
                    audioSink.BeginStream();
                }
                Interlocked.Exchange(ref lastStreamTicks, DateTime.UtcNow.Ticks);
                WeChatHotkey.Hold();
                RuntimeLog("STREAM START session=" + sessionId);
                Console.WriteLine("ATVV STREAM START session=" + sessionId);
            }
            else if (first == 0x0A && bytes.Length >= 7)
            {
                int predictor = (short)((bytes[4] << 8) | bytes[5]);
                lock (StreamLock) decoder.Reset(predictor, bytes[6]);
            }
            else if (first == 0x00)
            {
                Interlocked.Exchange(ref micOpen, 0);
                Thread.Sleep(140);
                audioSink.Drain(2000);
                Thread.Sleep(drainMs);
                int packets;
                int peak;
                long samples;
                double squareSum;
                lock (StreamLock)
                {
                    Volatile.Write(ref streamActive, 0);
                    packets = streamPacketCount;
                    peak = streamPeak;
                    samples = streamSampleCount;
                    squareSum = streamSquareSum;
                }
                WeChatHotkey.Release();
                double rms = samples == 0 ? 0 : Math.Sqrt(squareSum / samples);
                RuntimeLog("STREAM STOP session=" + sessionId + " packets=" + packets + " audio_ms=" + (samples * 1000 / 16000) + " peak_pct=" + (peak * 100.0 / 32768).ToString("0.0") + " rms_pct=" + (rms * 100.0 / 32768).ToString("0.0"));
                Console.WriteLine("ATVV STREAM STOP session=" + sessionId);
            }
        }
        catch (Exception ex) { Console.Error.WriteLine("Notify error: " + ex.Message); }
    }

    private static async Task WriteCommand(byte[] bytes, string name)
    {
        using (DataWriter writer = new DataWriter())
        {
            writer.WriteBytes(bytes);
            IBuffer buffer = writer.DetachBuffer();
            GattCommunicationStatus status = await writeCharacteristic.WriteValueAsync(buffer, GattWriteOption.WriteWithResponse).AsTask();
            AppendHostCommand(name, bytes, status);
            Console.WriteLine("HOST " + name + " " + Hex(bytes) + " => " + status);
            if (status != GattCommunicationStatus.Success)
                throw new InvalidOperationException("ATVV command failed: " + name + " => " + status);
        }
    }

    private static void AppendEvent(string type, string name, Guid uuid, byte[] bytes)
    {
        string json = "{\"time\":\"" + DateTime.Now.ToString("HH:mm:ss.fff") + "\",\"type\":\"" + type +
            "\",\"name\":\"" + name + "\",\"characteristic\":\"" + uuid + "\",\"length\":" + bytes.Length +
            ",\"hex\":\"" + Hex(bytes) + "\"}";
        AppendLine(json);
        Console.WriteLine(json);
    }

    private static void AppendHostCommand(string name, byte[] bytes, GattCommunicationStatus status)
    {
        string json = "{\"time\":\"" + DateTime.Now.ToString("HH:mm:ss.fff") + "\",\"type\":\"host_command\",\"name\":\"" + name +
            "\",\"hex\":\"" + Hex(bytes) + "\",\"status\":\"" + status + "\"}";
        AppendLine(json);
    }

    private static void AppendLine(string line)
    {
        lock (FileLock) File.AppendAllText(eventPath, line + Environment.NewLine, Utf8NoBom);
    }

    private static void RuntimeLog(string message)
    {
        try
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + message;
            if (!string.IsNullOrEmpty(runtimeLogPath))
            {
                lock (FileLock) File.AppendAllText(runtimeLogPath, line + Environment.NewLine, Utf8NoBom);
            }
            Console.WriteLine(line);
        }
        catch { }
    }

    private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", " "); }

    private static string ControlName(int opcode)
    {
        if (opcode == 0x0B) return "capabilities";
        if (opcode == 0x08) return "mic_open_request";
        if (opcode == 0x04) return "stream_start";
        if (opcode == 0x00) return "stream_stop";
        if (opcode == 0x0A) return "codec_sync";
        return "control_" + opcode;
    }

    private static void ParseCapabilities(byte[] bytes)
    {
        if (bytes.Length < 7 || bytes[0] != 0x0B) return;
        protocolVersion = (ushort)((bytes[1] << 8) | bytes[2]);
        byte codecs = protocolVersion >= 0x0100 ? bytes[3] : (bytes.Length >= 9 ? bytes[4] : (byte)0x02);
        if (protocolVersion >= 0x0100 && codecs == 0 && bytes.Length >= 9 && (bytes[4] & 0x03) != 0) codecs = bytes[4];
        selectedCodec = (byte)((codecs & 0x02) != 0 ? 0x02 : 0x01);
    }

    private static byte[] OpenCommand()
    {
        return protocolVersion >= 0x0100
            ? new byte[] { 0x0C, 0x00 }
            : new byte[] { 0x0C, 0x00, selectedCodec };
    }

    private static byte[] CloseCommand()
    {
        return protocolVersion >= 0x0100
            ? new byte[] { 0x0D, sessionId }
            : new byte[] { 0x0D };
    }

    private static int Score(string name)
    {
        string n = (name ?? "").ToLowerInvariant();
        int score = 0;
        string[] needles = { "mi rc", "rc003", "xiaomi", "小米", "语音遥控器", "bluetooth remote" };
        foreach (string needle in needles) if (n.Contains(needle)) score += 50;
        return score;
    }

    private static void WriteReport(string status, string error)
    {
        try
        {
            string safeError = (error ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
            string json = "{\"generated_at\":\"" + DateTime.Now.ToString("o") + "\",\"status\":\"" + status +
                "\",\"audio_packets\":" + audioCount + ",\"control_packets\":" + controlCount + ",\"error\":\"" + safeError + "\"}";
            File.WriteAllText(reportPath, json, Utf8NoBom);
        }
        catch { }
    }
}

internal sealed class ImaAdpcmDecoder
{
    private static readonly int[] StepTable = {
        7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31,
        34, 37, 41, 45, 50, 55, 60, 66, 73, 80, 88, 97, 107, 118, 130, 143,
        157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544,
        598, 658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707,
        1878, 2066, 2272, 2499, 2749, 3024, 3327, 3660, 4026, 4428, 4871,
        5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635,
        13899, 15289, 16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
    };
    private static readonly int[] IndexTable = { -1, -1, -1, -1, 2, 4, 6, 8 };
    private readonly double gain;
    private int predictor;
    private int stepIndex;

    public ImaAdpcmDecoder(double gainValue) { gain = gainValue; Reset(); }
    public void Reset() { Reset(0, 0); }
    public void Reset(int predictorValue, int indexValue)
    {
        predictor = Math.Max(-32768, Math.Min(32767, predictorValue));
        stepIndex = Math.Max(0, Math.Min(88, indexValue));
    }
    public short[] Decode(byte[] bytes)
    {
        short[] samples = new short[bytes.Length * 2];
        int output = 0;
        foreach (byte value in bytes)
        {
            samples[output++] = DecodeNibble((value >> 4) & 0x0F);
            samples[output++] = DecodeNibble(value & 0x0F);
        }
        return samples;
    }
    private short DecodeNibble(int nibble)
    {
        int step = StepTable[stepIndex];
        int difference = step >> 3;
        if ((nibble & 1) != 0) difference += step >> 2;
        if ((nibble & 2) != 0) difference += step >> 1;
        if ((nibble & 4) != 0) difference += step;
        predictor += (nibble & 8) != 0 ? -difference : difference;
        predictor = Math.Max(-32768, Math.Min(32767, predictor));
        stepIndex = Math.Max(0, Math.Min(88, stepIndex + IndexTable[nibble & 7]));
        int scaled = (int)Math.Round(predictor * gain);
        return (short)Math.Max(-32768, Math.Min(32767, scaled));
    }
}

internal static class WeChatHotkey
{
    private const byte VkControl = 0x11;
    private const byte VkLWin = 0x5B;
    private const uint KeyUp = 0x0002;
    private static int held;
    public static void Hold()
    {
        if (Interlocked.Exchange(ref held, 1) == 1) return;
        keybd_event(VkControl, 0x1D, 0, UIntPtr.Zero);
        keybd_event(VkLWin, 0x5B, 0, UIntPtr.Zero);
        Console.WriteLine("WECHAT HOTKEY DOWN ctrl+win");
    }
    public static void Release()
    {
        if (Interlocked.Exchange(ref held, 0) == 0) return;
        keybd_event(VkLWin, 0x5B, KeyUp, UIntPtr.Zero);
        keybd_event(VkControl, 0x1D, KeyUp, UIntPtr.Zero);
        Console.WriteLine("WECHAT HOTKEY UP ctrl+win");
    }
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
}

internal sealed class WaveOutSink : IDisposable
{
    private const uint WaveMapper = 0xFFFFFFFF;
    private const uint WhdrDone = 0x00000001;
    private readonly BlockingCollection<byte[]> queue = new BlockingCollection<byte[]>();
    private readonly Thread worker;
    private IntPtr waveOut;
    private int pending;
    private bool disposed;
    public string DeviceName { get; private set; }

    public WaveOutSink(string requestedName)
    {
        string actualName;
        uint deviceId = FindDevice(requestedName, out actualName);
        if (deviceId == WaveMapper) throw new InvalidOperationException("Audio endpoint not found: " + requestedName + ". Install VB-CABLE first.");
        DeviceName = actualName;
        var format = new WaveFormat
        {
            formatTag = 1,
            channels = 2,
            samplesPerSec = 48000,
            bitsPerSample = 16,
            blockAlign = 4,
            avgBytesPerSec = 192000,
            extraSize = 0
        };
        uint result = waveOutOpen(out waveOut, deviceId, ref format, IntPtr.Zero, IntPtr.Zero, 0);
        if (result != 0) throw new InvalidOperationException("Could not open " + actualName + " (waveOut error " + result + ")");
        worker = new Thread(WriteLoop);
        worker.IsBackground = true;
        worker.Name = "Vibe Mic VB-CABLE writer";
        worker.Start();
    }

    public void BeginStream()
    {
        short[] silence = new short[3200];
        Write(silence);
    }

    public void Write(short[] mono16k)
    {
        if (disposed || mono16k == null || mono16k.Length == 0) return;
        byte[] output = new byte[mono16k.Length * 12];
        int position = 0;
        foreach (short sample in mono16k)
        {
            for (int repeat = 0; repeat < 3; repeat++)
            {
                output[position++] = (byte)(sample & 0xFF);
                output[position++] = (byte)((sample >> 8) & 0xFF);
                output[position++] = (byte)(sample & 0xFF);
                output[position++] = (byte)((sample >> 8) & 0xFF);
            }
        }
        Interlocked.Increment(ref pending);
        queue.Add(output);
    }

    public void Drain(int timeoutMs)
    {
        int started = Environment.TickCount;
        while (Volatile.Read(ref pending) > 0 && unchecked(Environment.TickCount - started) < timeoutMs) Thread.Sleep(5);
    }

    private void WriteLoop()
    {
        var inFlight = new List<NativeWaveBuffer>();
        while (!queue.IsCompleted || queue.Count > 0 || inFlight.Count > 0)
        {
            if (inFlight.Count < 8)
            {
                byte[] data;
                if (queue.TryTake(out data, 4))
                {
                    try { inFlight.Add(SubmitBuffer(data)); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Audio submit error: " + ex.Message);
                        Interlocked.Decrement(ref pending);
                    }
                }
            }

            for (int i = inFlight.Count - 1; i >= 0; i--)
            {
                if (!IsBufferDone(inFlight[i])) continue;
                ReleaseBuffer(inFlight[i]);
                inFlight.RemoveAt(i);
                Interlocked.Decrement(ref pending);
            }

            if (inFlight.Count >= 8 && !IsBufferDone(inFlight[0])) Thread.Sleep(1);
        }
    }

    private NativeWaveBuffer SubmitBuffer(byte[] data)
    {
        var native = new NativeWaveBuffer();
        native.data = Marshal.AllocHGlobal(data.Length);
        Marshal.Copy(data, 0, native.data, data.Length);
        var header = new WaveHeader { data = native.data, bufferLength = (uint)data.Length };
        native.header = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WaveHeader)));
        try
        {
            Marshal.StructureToPtr(header, native.header, false);
            uint result = waveOutPrepareHeader(waveOut, native.header, (uint)Marshal.SizeOf(typeof(WaveHeader)));
            if (result != 0) throw new InvalidOperationException("prepare failed " + result);
            native.prepared = true;
            result = waveOutWrite(waveOut, native.header, (uint)Marshal.SizeOf(typeof(WaveHeader)));
            if (result != 0) throw new InvalidOperationException("write failed " + result);
            return native;
        }
        catch
        {
            try { ReleaseBuffer(native); } catch { }
            throw;
        }
    }

    private static bool IsBufferDone(NativeWaveBuffer native)
    {
        WaveHeader header = (WaveHeader)Marshal.PtrToStructure(native.header, typeof(WaveHeader));
        return (header.flags & WhdrDone) != 0;
    }

    private void ReleaseBuffer(NativeWaveBuffer native)
    {
        if (native == null) return;
        if (native.prepared && native.header != IntPtr.Zero)
        {
            waveOutUnprepareHeader(waveOut, native.header, (uint)Marshal.SizeOf(typeof(WaveHeader)));
            native.prepared = false;
        }
        if (native.header != IntPtr.Zero) { Marshal.FreeHGlobal(native.header); native.header = IntPtr.Zero; }
        if (native.data != IntPtr.Zero) { Marshal.FreeHGlobal(native.data); native.data = IntPtr.Zero; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Drain(1500);
        queue.CompleteAdding();
        if (worker != null) worker.Join(2000);
        if (waveOut != IntPtr.Zero)
        {
            waveOutReset(waveOut);
            waveOutClose(waveOut);
            waveOut = IntPtr.Zero;
        }
    }

    private static uint FindDevice(string requested, out string actual)
    {
        for (uint i = 0; i < waveOutGetNumDevs(); i++)
        {
            WaveOutCaps caps;
            if (waveOutGetDevCaps((UIntPtr)i, out caps, (uint)Marshal.SizeOf(typeof(WaveOutCaps))) == 0 &&
                (caps.name ?? "").IndexOf(requested ?? "CABLE Input", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                actual = caps.name;
                return i;
            }
        }
        actual = requested;
        return WaveMapper;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WaveOutCaps
    {
        public ushort manufacturerId, productId;
        public uint driverVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string name;
        public uint formats;
        public ushort channels, reserved;
        public uint support;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort formatTag, channels;
        public uint samplesPerSec, avgBytesPerSec;
        public ushort blockAlign, bitsPerSample, extraSize;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr data;
        public uint bufferLength, bytesRecorded;
        public IntPtr user;
        public uint flags, loops;
        public IntPtr next, reserved;
    }
    private sealed class NativeWaveBuffer
    {
        public IntPtr data;
        public IntPtr header;
        public bool prepared;
    }
    [DllImport("winmm.dll")] private static extern uint waveOutGetNumDevs();
    [DllImport("winmm.dll", CharSet = CharSet.Auto)] private static extern uint waveOutGetDevCaps(UIntPtr deviceId, out WaveOutCaps caps, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutOpen(out IntPtr handle, uint deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
    [DllImport("winmm.dll")] private static extern uint waveOutPrepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutWrite(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutUnprepareHeader(IntPtr handle, IntPtr header, uint size);
    [DllImport("winmm.dll")] private static extern uint waveOutReset(IntPtr handle);
    [DllImport("winmm.dll")] private static extern uint waveOutClose(IntPtr handle);
}
