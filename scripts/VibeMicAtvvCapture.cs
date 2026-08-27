using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

[assembly: System.Reflection.AssemblyTitle("Vibe Flow RC003 voice capture")]
[assembly: System.Reflection.AssemblyProduct("Vibe Flow Remote")]
[assembly: System.Reflection.AssemblyCompany("Vibe Flow Contributors")]
[assembly: System.Reflection.AssemblyVersion("1.2.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.2.1.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("1.2.1")]

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
    private static string captureHealthPath;
    private static string runtimeLogPath;
    private static string diagnosticDirectory;
    private static GattCharacteristic writeCharacteristic;
    private static int audioCount;
    private static int controlCount;
    private static int micOpen;
    private static ushort protocolVersion = 0x0100;
    private static byte selectedCodec = 0x02;
    private static byte sessionId;
    private static readonly object StreamLock = new object();
    private static ImaAdpcmDecoder decoder;
    private static SpeechLeveler leveler;
    private static ClockedVirtualMicSink audioSink;
    private static DefaultCaptureEndpointLease defaultCaptureEndpointLease;
    private static ITranscriptionSessionController voiceController;
    private static AdpcmFrameAccumulator frameAccumulator;
    private static EventWaitHandle stopEvent;
    private static EventWaitHandle voiceKeyEvent;
    private static EventWaitHandle voiceKeyHeldEvent;
    private static EventWaitHandle audioDiagnosticEvent;
    private static ManualResetEvent connectionLostEvent;
    private static ManualResetEventSlim capabilitiesReadyEvent;
    private static BlockingCollection<AudioNotification> audioPacketQueue;
    private static Thread audioPacketWorker;
    private static string audioEndpointName = "CABLE Input";
    private static double audioGain = 1.0;
    private static bool automaticLeveling = true;
    private static int drainMs = 180;
    private static string transcriptionProvider = "wechat";
    private static string transcriptionHotkey = "ctrl+win";
    private static string transcriptionTrigger = "toggle";
    private static int providerStartupDelayMs = 100;
    private static string audioProcessingMode = "speech";
    private static bool routeDefaultCaptureDuringDictation = true;
    private static long lastStreamTicks;
    private static long lastAudioTicks;
    private static int streamPacketCount;
    private static int streamActive;
    private static double streamSquareSum;
    private static long streamSampleCount;
    private static int streamPeak;
    private static double streamOutputSquareSum;
    private static int streamOutputPeak;
    private static double streamAppliedGainSum;
    private static int streamAppliedGainFrames;
    private static int streamGeneration;
    private static bool streamLiveStarted;
    private static readonly List<short> streamAudioBuffer = new List<short>();
    private static long lastVoiceKeyTicks;
    private static long lastPacketTicks;
    private static int streamMaxPacketGapMs;
    private static bool streamBufferTrimmed;
    private static bool pendingCodecSync;
    private static int pendingSyncPredictor;
    private static int pendingSyncIndex;
    private static int pendingStopGeneration;
    private static long lastStopSignalTicks;
    private static int ignoredAfterStopLogged;
    private static long audioPacketsEnqueued;
    private static long audioPacketsProcessed;
    private static int audioQueueMaxDepth;
    private static int audioQueueDrops;
    private static int audioDiagnosticArmed;
    private static readonly object CaptureHealthLock = new object();
    private static Timer captureHealthTimer;
    private static DateTime captureStartedUtc = DateTime.MinValue;
    private static string captureHealthState = "starting";
    private static int atvvReadyState;
    private static int bleConnectedState;
    private static readonly ConcurrentDictionary<int, AudioDiagnosticSession> AudioDiagnostics =
        new ConcurrentDictionary<int, AudioDiagnosticSession>();
    private const int PreRollLimitSamples = 16000 * 5;
    private const string RecordingKernelVersion = "v1.0.3";

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--self-test", StringComparison.OrdinalIgnoreCase)) return RunSelfTests();
        if (args.Length > 0 && args[0].Equals("--endpoint-route-test", StringComparison.OrdinalIgnoreCase))
            return RunEndpointRouteTest();
        if (args.Length > 0 && args[0].Equals("--sink-clock-test", StringComparison.OrdinalIgnoreCase))
            return RunSinkClockTest(args.Length > 1 ? args[1] : "CABLE Input");
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
            if (args.Length > 5) bool.TryParse(args[5], out automaticLeveling);
            if (args.Length > 6 && !string.IsNullOrWhiteSpace(args[6])) transcriptionProvider = args[6].Trim().ToLowerInvariant();
            if (args.Length > 7 && !string.IsNullOrWhiteSpace(args[7])) transcriptionHotkey = args[7].Trim();
            if (args.Length > 8 && !string.IsNullOrWhiteSpace(args[8])) transcriptionTrigger = args[8].Trim().ToLowerInvariant();
            if (args.Length > 9) int.TryParse(args[9], out providerStartupDelayMs);
            if (providerStartupDelayMs < 20 || providerStartupDelayMs > 2000) providerStartupDelayMs = 100;
            if (args.Length > 10 && !string.IsNullOrWhiteSpace(args[10])) audioProcessingMode = args[10].Trim().ToLowerInvariant();
            if (audioProcessingMode != "speech" && audioProcessingMode != "transparent")
                audioProcessingMode = automaticLeveling ? "speech" : "transparent";
            if (args.Length > 11) bool.TryParse(args[11], out routeDefaultCaptureDuringDictation);
            Directory.CreateDirectory(outDir);
            eventPath = Path.Combine(outDir, "remote-voice-events.jsonl");
            reportPath = Path.Combine(outDir, "remote-voice-report.json");
            captureHealthPath = Path.Combine(outDir, "capture-health.json");
            runtimeLogPath = Path.Combine(outDir, "vibe-mic-runtime.log");
            diagnosticDirectory = outDir;
            captureStartedUtc = DateTime.UtcNow;
            Volatile.Write(ref atvvReadyState, 0);
            Volatile.Write(ref bleConnectedState, 0);
            SetCaptureHealthState("starting");
            captureHealthTimer = new Timer(delegate { WriteCaptureHealth(null); }, null, 0, 2000);
            WriteReport("running", "");
            if (routeDefaultCaptureDuringDictation)
                defaultCaptureEndpointLease = new DefaultCaptureEndpointLease("CABLE Output",
                    Path.Combine(outDir, "default-capture-endpoint-lease.txt"), RuntimeLog);
            if (File.Exists(eventPath)) File.Delete(eventPath);
            decoder = new ImaAdpcmDecoder(1.0);
            leveler = new SpeechLeveler(audioProcessingMode, audioGain);
            frameAccumulator = new AdpcmFrameAccumulator(120);
            audioSink = new ClockedVirtualMicSink(audioEndpointName);
            capabilitiesReadyEvent = new ManualResetEventSlim(false);
            audioPacketQueue = new BlockingCollection<AudioNotification>(256);
            audioPacketWorker = new Thread(ProcessAudioPackets);
            audioPacketWorker.IsBackground = true;
            audioPacketWorker.Name = "Vibe Mic ordered ATVV audio decoder";
            audioPacketWorker.Start();
            Action<int> finalizeAudio = delegate(int generation)
            {
                try
                {
                    int pendingBefore = audioSink.PendingBlocks;
                    var drainTimer = System.Diagnostics.Stopwatch.StartNew();
                    audioSink.Flush();
                    bool drained = audioSink.Drain(5000, delegate
                    {
                        return Volatile.Read(ref streamGeneration) > generation;
                    });
                    if (!drained) audioSink.DiscardPending();
                    drainTimer.Stop();
                    RuntimeLog("VIRTUAL MIC DRAIN COMPLETE pending_before=" + pendingBefore +
                        " pending_after=" + audioSink.PendingBlocks + " waited_ms=" + drainTimer.ElapsedMilliseconds +
                        " queue_drops=" + audioSink.DroppedBlocks + " superseded=" + (!drained));
                    CompleteAudioDiagnostic(generation, drained ? "audio_drained" : "superseded_by_new_recording", false);
                }
                finally
                {
                    if (defaultCaptureEndpointLease != null) defaultCaptureEndpointLease.Release(generation, "audio_finalized");
                }
            };
            Func<int, bool> prepareTranscriptionInput = delegate(int generation)
            {
                return defaultCaptureEndpointLease == null || defaultCaptureEndpointLease.Acquire(generation);
            };
            Action<int> releaseTranscriptionInput = delegate(int generation)
            {
                if (defaultCaptureEndpointLease != null) defaultCaptureEndpointLease.Release(generation, "session_without_audio");
            };
            voiceController = TranscriptionSessionControllerFactory.Create(transcriptionProvider, transcriptionHotkey,
                transcriptionTrigger, providerStartupDelayMs, IsVoiceSessionActive, RuntimeLog, finalizeAudio,
                prepareTranscriptionInput, releaseTranscriptionInput);
            stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicStopCapture");
            voiceKeyEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicVoiceKeyPressed");
            voiceKeyHeldEvent = new EventWaitHandle(false, EventResetMode.ManualReset, "Local\\VibeMicVoiceKeyHeld");
            audioDiagnosticEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\VibeMicCaptureAudioDiagnostic");
            connectionLostEvent = new ManualResetEvent(false);
            RuntimeLog("START endpoint=" + audioSink.DeviceName + " source_format=16000_mono virtual_mic_format=48000_stereo_16bit sensitivity=" + audioGain.ToString("0.00") +
                " processing=" + audioProcessingMode + " drain_ms=" + drainMs + " provider=" + transcriptionProvider +
                " provider_hotkey=" + transcriptionHotkey.Replace(' ', '_') + " provider_trigger=" + transcriptionTrigger +
                " provider_startup_ms=" + providerStartupDelayMs +
                " recording_kernel=" + RecordingKernelVersion +
                " voice_state_machine=v11 ordered_audio_queue=true ordered_codec_sync=true sample_limiter=true" +
                " transcription_submit=true audio_clock=wasapi_event nonblocking_sink=true block_ms=20" +
                " audio_diagnostics=opt_in_next_session default_capture_route=" + routeDefaultCaptureDuringDictation);
            try { RunAsync(seconds).GetAwaiter().GetResult(); }
            finally
            {
                if (captureHealthTimer != null)
                {
                    try { captureHealthTimer.Change(Timeout.Infinite, Timeout.Infinite); } catch { }
                    try { captureHealthTimer.Dispose(); } catch { }
                    captureHealthTimer = null;
                }
                SetCaptureHealthState("stopped");
                if (audioPacketQueue != null)
                {
                    audioPacketQueue.CompleteAdding();
                    if (audioPacketWorker != null) audioPacketWorker.Join(2000);
                }
                if (voiceController != null) voiceController.Dispose();
                if (defaultCaptureEndpointLease != null) defaultCaptureEndpointLease.Dispose();
                CompleteAllAudioDiagnostics("capture_shutdown");
                if (audioSink != null) audioSink.Dispose();
                if (stopEvent != null) stopEvent.Dispose();
                if (voiceKeyEvent != null) voiceKeyEvent.Dispose();
                if (voiceKeyHeldEvent != null) voiceKeyHeldEvent.Dispose();
                if (audioDiagnosticEvent != null) audioDiagnosticEvent.Dispose();
                if (connectionLostEvent != null) connectionLostEvent.Dispose();
                if (capabilitiesReadyEvent != null) capabilitiesReadyEvent.Dispose();
                if (audioPacketQueue != null) audioPacketQueue.Dispose();
            }
            WriteReport("completed", "");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            RuntimeLog("ERROR " + ex.Message);
            SetCaptureHealthState("error");
            if (!string.IsNullOrEmpty(reportPath)) WriteReport("error", ex.Message);
            return 1;
        }
    }

    private static async Task RunAsync(int seconds)
    {
        SetCaptureHealthState("connecting");
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
            Volatile.Write(ref bleConnectedState, ble.ConnectionStatus == BluetoothConnectionStatus.Connected ? 1 : 0);
            connectionLostEvent.Reset();
            ble.ConnectionStatusChanged += OnConnectionStatusChanged;

            try
            {
                GattDeviceServicesResult services = await ble.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Cached).AsTask();
                bool serviceCacheHit = services.Status == GattCommunicationStatus.Success && services.Services.Count > 0;
                if (!serviceCacheHit)
                {
                    RuntimeLog("GATT SERVICE cache=miss status=" + services.Status + " fallback=uncached");
                    services = await ble.GetGattServicesForUuidAsync(ServiceUuid, BluetoothCacheMode.Uncached).AsTask();
                }
                else RuntimeLog("GATT SERVICE cache=hit");
                Console.WriteLine("ATVV service status: " + services.Status + " cache=" + (serviceCacheHit ? "hit" : "fallback"));
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
                        Volatile.Write(ref bleConnectedState, 1);
                        Volatile.Write(ref atvvReadyState, 1);
                        SetCaptureHealthState("ready");
                        RuntimeLog("ATVV READY route=RC003_16k_mono_to_" + audioSink.DeviceName + "_48k_stereo_clocked");
                        RecoverHeldVoiceRequestAtReady();
                        Console.WriteLine(seconds == 0 ? "Listening continuously. Hold the RC003 record button and speak." : "Listening for " + seconds + " seconds. Hold the RC003 record button and speak.");
                        Console.WriteLine("Audio route: RC003 16 kHz mono -> " + audioSink.DeviceName + " 48 kHz stereo clocked virtual mic");
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
        bool connected = sender.ConnectionStatus == BluetoothConnectionStatus.Connected;
        Volatile.Write(ref bleConnectedState, connected ? 1 : 0);
        if (!connected)
        {
            Volatile.Write(ref atvvReadyState, 0);
            SetCaptureHealthState("disconnected");
            if (connectionLostEvent != null) connectionLostEvent.Set();
        }
        else SetCaptureHealthState(Volatile.Read(ref atvvReadyState) == 1 ? "ready" : "connecting");
    }

    private static void RecoverHeldVoiceRequestAtReady()
    {
        if (!ShouldRecoverHeldVoiceRequest(voiceKeyHeldEvent)) return;
        RuntimeLog("STARTUP VOICE HOLD recovered_at_atvv_ready=true delayed_after_release=false");
        voiceKeyEvent.Set();
    }

    private static bool ShouldRecoverHeldVoiceRequest(WaitHandle heldEvent)
    {
        return heldEvent != null && heldEvent.WaitOne(0);
    }

    private static void MonitorConnection(int seconds)
    {
        int started = Environment.TickCount;
        WaitHandle[] handles = { stopEvent, connectionLostEvent, voiceKeyEvent, audioDiagnosticEvent };
        while (true)
        {
            int timeout = seconds == 0 ? Timeout.Infinite : Math.Max(0, seconds * 1000 - unchecked(Environment.TickCount - started));
            int signal = WaitHandle.WaitAny(handles, timeout);
            if (signal == WaitHandle.WaitTimeout || signal == 0) return;
            if (signal == 1) throw new IOException("RC003 Bluetooth voice connection was lost.");
            if (signal == 3)
            {
                Interlocked.Exchange(ref audioDiagnosticArmed, 1);
                RuntimeLog("AUDIO DIAGNOSTIC ARMED next_session_only=true max_seconds=30 privacy=explicit_user_action");
                continue;
            }
            if (!ShouldRecoverHeldVoiceRequest(voiceKeyHeldEvent))
            {
                RuntimeLog("VOICE KEY discarded reason=released_before_capture_ready delayed_after_release=false");
                continue;
            }

            long pressedAt = DateTime.UtcNow.Ticks;
            long previousVoiceKey = Interlocked.Exchange(ref lastVoiceKeyTicks, pressedAt);
            if (previousVoiceKey != 0 && pressedAt - previousVoiceKey < TimeSpan.FromMilliseconds(500).Ticks)
            {
                RuntimeLog("VOICE KEY coalesced duplicate_source");
                continue;
            }

            int generationAtPress = Volatile.Read(ref streamGeneration);
            RuntimeLog("VOICE KEY detected generation=" + generationAtPress + "; waiting_for_natural_stream_ms=120");
            if (WaitForNewStream(generationAtPress, 120)) continue;
            if (stopEvent.WaitOne(0)) return;
            if (connectionLostEvent.WaitOne(0)) throw new IOException("RC003 connection was lost while waiting for its voice stream.");

            if (Interlocked.CompareExchange(ref micOpen, 1, 0) == 0)
            {
                try
                {
                    WriteCommand(OpenCommand(), "mic_open_recovery").GetAwaiter().GetResult();
                    RuntimeLog("ATVV MIC_OPEN recovery requested generation=" + generationAtPress);
                }
                catch
                {
                    Interlocked.Exchange(ref micOpen, 0);
                    throw;
                }
            }

            if (!WaitForAudioAfter(pressedAt, 1500))
            {
                if (stopEvent.WaitOne(0)) return;
                throw new IOException("Voice key was detected but RC003 delivered no audio after MIC_OPEN recovery.");
            }
        }
    }

    private static bool WaitForNewStream(int generationAtPress, int timeoutMs)
    {
        int started = Environment.TickCount;
        while (unchecked(Environment.TickCount - started) < timeoutMs)
        {
            if (Volatile.Read(ref streamGeneration) != generationAtPress || Volatile.Read(ref streamActive) == 1) return true;
            if (stopEvent.WaitOne(0) || connectionLostEvent.WaitOne(0)) return false;
            Thread.Sleep(10);
        }
        return Volatile.Read(ref streamGeneration) != generationAtPress || Volatile.Read(ref streamActive) == 1;
    }

    private static bool WaitForAudioAfter(long pressedAt, int timeoutMs)
    {
        int started = Environment.TickCount;
        while (unchecked(Environment.TickCount - started) < timeoutMs)
        {
            if (Interlocked.Read(ref lastAudioTicks) >= pressedAt) return true;
            if (stopEvent.WaitOne(0) || connectionLostEvent.WaitOne(0)) return false;
            Thread.Sleep(10);
        }
        return Interlocked.Read(ref lastAudioTicks) >= pressedAt;
    }

    private static async Task<GattCharacteristic> GetCharacteristic(GattDeviceService service, Guid uuid)
    {
        GattCharacteristicsResult result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Cached).AsTask();
        bool cacheHit = result.Status == GattCommunicationStatus.Success && result.Characteristics.Count > 0;
        if (!cacheHit)
        {
            RuntimeLog("GATT CHARACTERISTIC cache=miss uuid=" + uuid + " status=" + result.Status + " fallback=uncached");
            result = await service.GetCharacteristicsForUuidAsync(uuid, BluetoothCacheMode.Uncached).AsTask();
        }
        else RuntimeLog("GATT CHARACTERISTIC cache=hit uuid=" + uuid);
        Console.WriteLine("Characteristic " + uuid + " status: " + result.Status + " cache=" + (cacheHit ? "hit" : "fallback"));
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
            if (control && first == 0x0A && bytes.Length >= 7)
            {
                EnqueueOrderedAudio(new AudioNotification
                {
                    IsCodecSync = true,
                    SyncPredictor = (short)((bytes[4] << 8) | bytes[5]),
                    SyncIndex = bytes[6],
                    ObservedGeneration = Volatile.Read(ref streamGeneration),
                    WasStreamActive = Volatile.Read(ref streamActive) == 1
                });
            }
            if (control)
            {
                Interlocked.Increment(ref controlCount);
                AppendEvent("remote_control", ControlName(first), sender.Uuid, bytes);
            }
            else
            {
                Interlocked.Increment(ref audioCount);
            }

            if (!control)
            {
                var notification = new AudioNotification
                {
                    Bytes = bytes,
                    ObservedGeneration = Volatile.Read(ref streamGeneration),
                    WasStreamActive = Volatile.Read(ref streamActive) == 1
                };
                EnqueueOrderedAudio(notification);
                return;
            }

            if (first == 0x0B)
            {
                ParseCapabilities(bytes);
                Console.WriteLine("ATVV CAPS version=" + protocolVersion.ToString("X4") + " codec=" + selectedCodec);
            }
            else if (first == 0x08 && Interlocked.CompareExchange(ref micOpen, 1, 0) == 0)
            {
                try { await WriteCommand(OpenCommand(), "mic_open"); }
                catch
                {
                    Interlocked.Exchange(ref micOpen, 0);
                    if (connectionLostEvent != null) connectionLostEvent.Set();
                    throw;
                }
            }
            else if (first == 0x04)
            {
                byte receivedSession = bytes.Length >= 4 ? bytes[3] : sessionId;
                bool started;
                int startedGeneration = BeginStream(receivedSession, out started);
                if (started)
                {
                    RuntimeLog("REMOTE STREAM START session=" + sessionId + " generation=" + startedGeneration + " mode=live source=control");
                    Console.WriteLine("ATVV STREAM START session=" + sessionId);
                }
                else RuntimeLog("REMOTE STREAM START duplicate session=" + sessionId + " generation=" + startedGeneration);
            }
            else if (first == 0x0A && bytes.Length >= 7)
            {
                // Codec sync was queued before diagnostic file I/O so it cannot overtake audio frames.
            }
            else if (first == 0x00)
            {
                int stoppingGeneration = Volatile.Read(ref streamGeneration);
                byte stoppingSession = sessionId;
                Interlocked.Exchange(ref micOpen, 0);
                Interlocked.Exchange(ref lastStopSignalTicks, DateTime.UtcNow.Ticks);
                Interlocked.Exchange(ref pendingStopGeneration, stoppingGeneration);
                RuntimeLog("REMOTE STREAM STOP SIGNAL session=" + stoppingSession + " generation=" + stoppingGeneration + " tail_wait_ms=80");
                ThreadPool.QueueUserWorkItem(delegate
                {
                    Thread.Sleep(80);
                    long queuedThroughTail = Interlocked.Read(ref audioPacketsEnqueued);
                    WaitForAudioPackets(queuedThroughTail, 1500);
                    FinalizeStreamStop(stoppingGeneration, stoppingSession);
                });
            }
        }
        catch (Exception ex) { Console.Error.WriteLine("Notify error: " + ex.Message); }
    }

    private static void EnqueueOrderedAudio(AudioNotification notification)
    {
        if (!audioPacketQueue.TryAdd(notification))
        {
            Interlocked.Increment(ref audioQueueDrops);
            RuntimeLog("ATVV ORDERED QUEUE OVERFLOW capacity=256; reconnecting");
            if (connectionLostEvent != null) connectionLostEvent.Set();
            return;
        }

        Interlocked.Increment(ref audioPacketsEnqueued);
        if (!notification.IsCodecSync && Volatile.Read(ref streamActive) == 1)
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            long previousTicks = Interlocked.Exchange(ref lastPacketTicks, nowTicks);
            if (previousTicks != 0)
            {
                int gapMs = (int)TimeSpan.FromTicks(nowTicks - previousTicks).TotalMilliseconds;
                int observedGap;
                while (gapMs > (observedGap = Volatile.Read(ref streamMaxPacketGapMs)) &&
                    Interlocked.CompareExchange(ref streamMaxPacketGapMs, gapMs, observedGap) != observedGap) { }
            }
        }

        int depth = audioPacketQueue.Count;
        int observed;
        while (depth > (observed = Volatile.Read(ref audioQueueMaxDepth)) &&
            Interlocked.CompareExchange(ref audioQueueMaxDepth, depth, observed) != observed) { }
    }

    private static void ProcessAudioPackets()
    {
        foreach (AudioNotification packet in audioPacketQueue.GetConsumingEnumerable())
        {
            try
            {
                if (!capabilitiesReadyEvent.Wait(1500))
                {
                    RuntimeLog("ATVV ORDERED notification dropped reason=capabilities_not_ready bytes=" +
                        (packet.Bytes == null ? 0 : packet.Bytes.Length));
                    continue;
                }
                if (packet.IsCodecSync) ApplyOrderedCodecSync(packet);
                else ProcessAudioPacket(packet);
            }
            catch (Exception ex)
            {
                RuntimeLog("ATVV AUDIO PROCESS ERROR " + ex.Message);
                if (connectionLostEvent != null) connectionLostEvent.Set();
            }
            finally { Interlocked.Increment(ref audioPacketsProcessed); }
        }
    }

    private static void ApplyOrderedCodecSync(AudioNotification packet)
    {
        lock (StreamLock)
        {
            if (!packet.WasStreamActive || packet.ObservedGeneration != Volatile.Read(ref streamGeneration) ||
                Volatile.Read(ref streamActive) != 1)
            {
                RuntimeLog("ATVV CODEC SYNC ignored_stale observed_generation=" + packet.ObservedGeneration +
                    " current_generation=" + Volatile.Read(ref streamGeneration));
                return;
            }

            frameAccumulator.Reset();
            pendingSyncPredictor = packet.SyncPredictor;
            pendingSyncIndex = packet.SyncIndex;
            pendingCodecSync = true;
        }
        RuntimeLog("ATVV CODEC SYNC generation=" + packet.ObservedGeneration + " predictor=" +
            packet.SyncPredictor + " index=" + packet.SyncIndex + " ordered=true");
    }

    private static void ProcessAudioPacket(AudioNotification packet)
    {
        if (packet.WasStreamActive && (packet.ObservedGeneration != Volatile.Read(ref streamGeneration) ||
            Volatile.Read(ref streamActive) == 0))
        {
            if (Interlocked.Exchange(ref ignoredAfterStopLogged, 1) == 0)
                RuntimeLog("ATVV AUDIO ignored_stale_generation observed_generation=" + packet.ObservedGeneration +
                    " current_generation=" + Volatile.Read(ref streamGeneration));
            return;
        }
        byte[] bytes = packet.Bytes;
        short[] liveAudio = null;
        bool liveStartedNow = false;
        bool bufferTrimmedNow = false;
        bool implicitStreamStarted;
        int generation = EnsureStreamForAudio(out implicitStreamStarted);
        if (generation == 0) return;

        lock (StreamLock)
        {
            if (Volatile.Read(ref streamActive) != 1 || Volatile.Read(ref streamGeneration) != generation) return;

            List<byte[]> frames = frameAccumulator.Append(bytes);
            var readyAudio = new List<short>();
            foreach (byte[] frame in frames)
            {
                if (pendingCodecSync)
                {
                    decoder.Reset(pendingSyncPredictor, pendingSyncIndex);
                    pendingCodecSync = false;
                }

                short[] decoded = decoder.Decode(frame);
                streamPacketCount++;
                foreach (short sample in decoded)
                {
                    int absolute = sample == short.MinValue ? 32768 : Math.Abs((int)sample);
                    if (absolute > streamPeak) streamPeak = absolute;
                    streamSquareSum += (double)sample * sample;
                    streamSampleCount++;
                }

                short[] leveled = leveler.Process(decoded);
                AudioDiagnosticSession diagnostic;
                if (AudioDiagnostics.TryGetValue(generation, out diagnostic)) diagnostic.Append(decoded, leveled);
                streamAppliedGainSum += leveler.LastAppliedGain;
                streamAppliedGainFrames++;
                foreach (short sample in leveled)
                {
                    int absolute = sample == short.MinValue ? 32768 : Math.Abs((int)sample);
                    if (absolute > streamOutputPeak) streamOutputPeak = absolute;
                    streamOutputSquareSum += (double)sample * sample;
                }
                readyAudio.AddRange(leveled);
            }

            if (voiceController.IsReady(generation))
            {
                if (streamAudioBuffer.Count > 0)
                {
                    streamAudioBuffer.AddRange(readyAudio);
                    liveAudio = streamAudioBuffer.ToArray();
                    streamAudioBuffer.Clear();
                }
                else if (readyAudio.Count > 0) liveAudio = readyAudio.ToArray();
                if (!streamLiveStarted && liveAudio != null)
                {
                    streamLiveStarted = true;
                    liveStartedNow = true;
                }
            }
            else
            {
                streamAudioBuffer.AddRange(readyAudio);
                if (streamAudioBuffer.Count > PreRollLimitSamples)
                {
                    streamAudioBuffer.RemoveRange(0, streamAudioBuffer.Count - PreRollLimitSamples);
                    if (!streamBufferTrimmed)
                    {
                        streamBufferTrimmed = true;
                        bufferTrimmedNow = true;
                    }
                }
            }
        }

        if (liveAudio != null) audioSink.Write(liveAudio);
        if (implicitStreamStarted) RuntimeLog("ATVV STREAM implicit_audio_race generation=" + generation);
        if (liveStartedNow)
        {
            RuntimeLog("AUDIO LIVE START session=" + sessionId + " generation=" + generation + " buffered_samples=" + liveAudio.Length);
            SignalRecordingCue(true, generation);
        }
        if (bufferTrimmedNow) RuntimeLog("AUDIO BUFFER TRIMMED generation=" + generation + " limit_ms=5000");
        Interlocked.Exchange(ref lastAudioTicks, DateTime.UtcNow.Ticks);
    }

    private sealed class AudioNotification
    {
        public byte[] Bytes;
        public int ObservedGeneration;
        public bool WasStreamActive;
        public bool IsCodecSync;
        public int SyncPredictor;
        public int SyncIndex;
    }

    private static int EnsureStreamForAudio(out bool started)
    {
        started = false;
        if (Volatile.Read(ref streamActive) == 1) return Volatile.Read(ref streamGeneration);

        long stoppedAt = Interlocked.Read(ref lastStopSignalTicks);
        if (stoppedAt != 0 && DateTime.UtcNow.Ticks - stoppedAt < TimeSpan.FromMilliseconds(300).Ticks)
        {
            if (Interlocked.Exchange(ref ignoredAfterStopLogged, 1) == 0)
                RuntimeLog("ATVV AUDIO ignored_after_stop delay_ms=" + (int)TimeSpan.FromTicks(DateTime.UtcNow.Ticks - stoppedAt).TotalMilliseconds);
            return 0;
        }

        return BeginStream(sessionId, out started);
    }

    private static void WaitForAudioPackets(long target, int timeoutMs)
    {
        int started = Environment.TickCount;
        while (Interlocked.Read(ref audioPacketsProcessed) < target && unchecked(Environment.TickCount - started) < timeoutMs)
            Thread.Sleep(2);
    }

    private static int BeginStream(byte receivedSession, out bool started)
    {
        int generation;
        lock (StreamLock)
        {
            int activeGeneration = Volatile.Read(ref streamGeneration);
            bool replacingStoppedStream = Volatile.Read(ref streamActive) == 1 &&
                Volatile.Read(ref pendingStopGeneration) == activeGeneration;
            if (Volatile.Read(ref streamActive) == 1 && !replacingStoppedStream)
            {
                sessionId = receivedSession;
                started = false;
                return activeGeneration;
            }

            sessionId = receivedSession;
            generation = Interlocked.Increment(ref streamGeneration);
            decoder.Reset();
            frameAccumulator.Reset();
            pendingCodecSync = false;
            streamPacketCount = 0;
            streamSquareSum = 0;
            streamSampleCount = 0;
            streamPeak = 0;
            streamOutputSquareSum = 0;
            streamOutputPeak = 0;
            streamAppliedGainSum = 0;
            streamAppliedGainFrames = 0;
            leveler.BeginSession();
            audioSink.ResetSessionMetrics();
            Interlocked.Exchange(ref audioQueueMaxDepth, audioPacketQueue == null ? 0 : audioPacketQueue.Count);
            Interlocked.Exchange(ref audioQueueDrops, 0);
            streamLiveStarted = false;
            streamAudioBuffer.Clear();
            Interlocked.Exchange(ref lastPacketTicks, 0);
            Interlocked.Exchange(ref streamMaxPacketGapMs, 0);
            streamBufferTrimmed = false;
            Interlocked.Exchange(ref pendingStopGeneration, 0);
            Interlocked.Exchange(ref ignoredAfterStopLogged, 0);
            Volatile.Write(ref streamActive, 1);
            if (Interlocked.Exchange(ref audioDiagnosticArmed, 0) == 1)
            {
                try
                {
                    var diagnostic = new AudioDiagnosticSession(diagnosticDirectory, generation, receivedSession, "CABLE Output");
                    AudioDiagnostics[generation] = diagnostic;
                    RuntimeLog("AUDIO DIAGNOSTIC START generation=" + generation + " directory=" +
                        diagnostic.DirectoryPath + " cable_capture=" + diagnostic.CableCaptureStatus);
                }
                catch (Exception ex)
                {
                    RuntimeLog("AUDIO DIAGNOSTIC START FAILED generation=" + generation + " error=" + ex.Message);
                }
            }
            started = true;
        }
        Interlocked.Exchange(ref lastStreamTicks, DateTime.UtcNow.Ticks);
        voiceController.Start(generation);
        return generation;
    }

    private static void FinalizeStreamStop(int stoppingGeneration, byte stoppingSession)
    {
        int packets;
        int peak;
        int maxGapMs;
        int partialFrameBytes;
        int outputPeak;
        long samples;
        double squareSum;
        double outputSquareSum;
        double averageGain;
        int discardedSamples;
        bool liveDelivered;
        lock (StreamLock)
        {
            if (stoppingGeneration != Volatile.Read(ref streamGeneration) || Volatile.Read(ref streamActive) == 0)
            {
                RuntimeLog("STALE STREAM STOP ignored session=" + stoppingSession + " generation=" + stoppingGeneration + " current_generation=" + Volatile.Read(ref streamGeneration));
                return;
            }
            Volatile.Write(ref streamActive, 0);
            Interlocked.CompareExchange(ref pendingStopGeneration, 0, stoppingGeneration);
            packets = streamPacketCount;
            peak = streamPeak;
            maxGapMs = streamMaxPacketGapMs;
            samples = streamSampleCount;
            squareSum = streamSquareSum;
            outputSquareSum = streamOutputSquareSum;
            outputPeak = streamOutputPeak;
            averageGain = streamAppliedGainFrames == 0 ? 0 : streamAppliedGainSum / streamAppliedGainFrames;
            partialFrameBytes = frameAccumulator.PendingCount;
            liveDelivered = streamLiveStarted;
            discardedSamples = liveDelivered ? 0 : streamAudioBuffer.Count;
            streamAudioBuffer.Clear();
            frameAccumulator.Reset();
        }

        double rms = samples == 0 ? 0 : Math.Sqrt(squareSum / samples);
        double outputRms = samples == 0 ? 0 : Math.Sqrt(outputSquareSum / samples);
        int audioMs = (int)(samples * 1000 / 16000);
        RuntimeLog("REMOTE STREAM STOP session=" + stoppingSession + " generation=" + stoppingGeneration +
            " frames=" + packets + " audio_ms=" + audioMs + " raw_peak_pct=" + (peak * 100.0 / 32768).ToString("0.0") +
            " raw_rms_pct=" + (rms * 100.0 / 32768).ToString("0.0") +
            " output_peak_pct=" + (outputPeak * 100.0 / 32768).ToString("0.0") +
            " output_rms_pct=" + (outputRms * 100.0 / 32768).ToString("0.0") +
            " avg_gain=" + averageGain.ToString("0.00") + " max_gap_ms=" + maxGapMs +
            " queue_max=" + Volatile.Read(ref audioQueueMaxDepth) + " queue_drops=" + Volatile.Read(ref audioQueueDrops) +
            " sink_queue_max=" + audioSink.MaximumQueueDepth + " sink_queue_drops=" + audioSink.DroppedBlocks +
            " sink_pending=" + audioSink.PendingBlocks + " partial_frame_bytes=" + partialFrameBytes);

        if (liveDelivered)
        {
            audioSink.WriteSilence(drainMs);
            audioSink.Flush();
            voiceController.Stop(stoppingGeneration, true);
            RuntimeLog("AUDIO LIVE STOP session=" + stoppingSession + " generation=" + stoppingGeneration + " trailing_silence_ms=" + drainMs);
        }
        else
        {
            voiceController.Stop(stoppingGeneration, false);
            CompleteAudioDiagnostic(stoppingGeneration, "voice_panel_unavailable", false);
            RuntimeLog("AUDIO LIVE FAILED session=" + stoppingSession + " generation=" + stoppingGeneration +
                " reason=voice_panel_unavailable discarded_samples=" + discardedSamples);
        }
        SignalRecordingCue(false, stoppingGeneration);
        Console.WriteLine("ATVV STREAM STOP session=" + stoppingSession);
    }

    private static void CompleteAudioDiagnostic(int generation, string reason, bool synchronous)
    {
        AudioDiagnosticSession diagnostic;
        if (!AudioDiagnostics.TryRemove(generation, out diagnostic)) return;
        diagnostic.RequestStop();
        Action complete = delegate
        {
            try
            {
                string result = diagnostic.Complete(reason);
                RuntimeLog("AUDIO DIAGNOSTIC COMPLETE generation=" + generation + " " + result);
            }
            catch (Exception ex)
            {
                RuntimeLog("AUDIO DIAGNOSTIC FAILED generation=" + generation + " error=" + ex.Message);
            }
        };
        if (synchronous) complete();
        else ThreadPool.QueueUserWorkItem(delegate { complete(); });
    }

    private static void CompleteAllAudioDiagnostics(string reason)
    {
        foreach (int generation in AudioDiagnostics.Keys.ToArray())
            CompleteAudioDiagnostic(generation, reason, true);
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

    private static void SignalRecordingCue(bool starting, int generation)
    {
        string eventName = starting ? "Local\\VibeMicRecordingStartCue" : "Local\\VibeMicRecordingStopCue";
        bool signaled = false;
        try
        {
            using (EventWaitHandle handle = EventWaitHandle.OpenExisting(eventName)) signaled = handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException) { }
        catch { }
        RuntimeLog("RECORDING CUE SIGNAL kind=" + (starting ? "start" : "stop") +
            " generation=" + generation + " signaled=" + signaled);
    }

    private static void SetCaptureHealthState(string state)
    {
        lock (CaptureHealthLock)
        {
            if (!string.IsNullOrWhiteSpace(state)) captureHealthState = state;
            WriteCaptureHealthLocked();
        }
    }

    private static void WriteCaptureHealth(string state)
    {
        lock (CaptureHealthLock)
        {
            if (!string.IsNullOrWhiteSpace(state)) captureHealthState = state;
            WriteCaptureHealthLocked();
        }
    }

    private static void WriteCaptureHealthLocked()
    {
        if (string.IsNullOrWhiteSpace(captureHealthPath)) return;
        try
        {
            long audioTicks = Interlocked.Read(ref lastAudioTicks);
            string lastAudio = audioTicks > 0
                ? new DateTime(audioTicks, DateTimeKind.Utc).ToString("o") : "";
            string json = "{\"updated_at\":\"" + DateTime.UtcNow.ToString("o") +
                "\",\"pid\":" + System.Diagnostics.Process.GetCurrentProcess().Id +
                ",\"process_started_utc\":\"" + captureStartedUtc.ToString("o") +
                "\",\"state\":\"" + (captureHealthState ?? "starting") +
                "\",\"recording_kernel\":\"" + RecordingKernelVersion +
                "\",\"atvv_ready\":" + (Volatile.Read(ref atvvReadyState) == 1 ? "true" : "false") +
                ",\"ble_connected\":" + (Volatile.Read(ref bleConnectedState) == 1 ? "true" : "false") +
                ",\"audio_packets\":" + Interlocked.CompareExchange(ref audioCount, 0, 0) +
                ",\"control_packets\":" + Interlocked.CompareExchange(ref controlCount, 0, 0) +
                ",\"last_audio_at\":\"" + lastAudio + "\"}";
            string tempPath = captureHealthPath + ".tmp";
            File.WriteAllText(tempPath, json, Utf8NoBom);
            if (File.Exists(captureHealthPath)) File.Replace(tempPath, captureHealthPath, null);
            else File.Move(tempPath, captureHealthPath);
        }
        catch { }
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
        if (capabilitiesReadyEvent != null) capabilitiesReadyEvent.Set();
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

    private static bool IsVoiceSessionActive(int generation)
    {
        return Volatile.Read(ref streamActive) == 1 && Volatile.Read(ref streamGeneration) == generation;
    }

    private static int RunSelfTests()
    {
        try
        {
            var testDecoder = new ImaAdpcmDecoder(1.0);
            short[] decoded = testDecoder.Decode(new byte[] { 0x11 });
            if (decoded.Length != 2 || decoded[0] != 1 || decoded[1] != 2) throw new InvalidOperationException("ADPCM nibble order failed");

            var accumulator = new AdpcmFrameAccumulator(120);
            if (accumulator.Append(new byte[60]).Count != 0 || accumulator.PendingCount != 60) throw new InvalidOperationException("Frame partial buffering failed");
            if (accumulator.Append(new byte[60]).Count != 1 || accumulator.PendingCount != 0) throw new InvalidOperationException("Frame completion failed");
            if (accumulator.Append(new byte[240]).Count != 2 || accumulator.PendingCount != 0) throw new InvalidOperationException("Frame coalescing failed");

            var testLeveler = new SpeechLeveler("speech", 1.0);
            short[] quiet = Enumerable.Repeat((short)1000, 240).ToArray();
            short[] raised = testLeveler.Process(quiet);
            if (raised.Max(v => Math.Abs((int)v)) <= 1500 || raised.Max(v => Math.Abs((int)v)) > 30000)
                throw new InvalidOperationException("Robust quiet-speech leveling failed");
            short[] protectedOutput = testLeveler.Process(Enumerable.Repeat((short)30000, 240).ToArray());
            if (protectedOutput.Any(v => Math.Abs((int)v) > 30000)) throw new InvalidOperationException("Peak protection failed");

            var outlierLeveler = new SpeechLeveler("speech", 1.0);
            short[] withOutlier = Enumerable.Repeat((short)500, 240).ToArray();
            withOutlier[120] = 32767;
            short[] outlierOutput = outlierLeveler.Process(withOutlier);
            if (Math.Abs((int)outlierOutput[40]) < 1500 || outlierOutput.Any(v => Math.Abs((int)v) > 30000))
                throw new InvalidOperationException("Isolated spike resilience failed");

            var veryQuietLeveler = new SpeechLeveler("speech", 1.0);
            short[] veryQuietOutput = null;
            for (int i = 0; i < 20; i++) veryQuietOutput = veryQuietLeveler.Process(Enumerable.Repeat((short)200, 240).ToArray());
            if (veryQuietOutput.Max(v => Math.Abs((int)v)) < 1000)
                throw new InvalidOperationException("Very quiet speech recovery failed");

            var transparentLeveler = new SpeechLeveler("transparent", 1.0);
            short[] transparent = transparentLeveler.Process(new short[] { -1234, 2345 });
            if (transparent[0] != -1234 || transparent[1] != 2345)
                throw new InvalidOperationException("Transparent audio mode failed");

            List<int> parsedHotkey;
            if (!KeyboardShortcutSender.TryParse("ctrl+win", out parsedHotkey) || parsedHotkey.Count != 2 ||
                !KeyboardShortcutSender.TryParse("rightalt", out parsedHotkey) || parsedHotkey.Count != 1 ||
                KeyboardShortcutSender.TryParse("not-a-key", out parsedHotkey))
                throw new InvalidOperationException("Transcription hotkey parsing failed");

            using (var startupHold = new ManualResetEvent(false))
            {
                if (ShouldRecoverHeldVoiceRequest(startupHold))
                    throw new InvalidOperationException("Released startup voice key was recovered");
                startupHold.Set();
                if (!ShouldRecoverHeldVoiceRequest(startupHold))
                    throw new InvalidOperationException("Held startup voice key was not recovered");
                startupHold.Reset();
                if (ShouldRecoverHeldVoiceRequest(startupHold))
                    throw new InvalidOperationException("Startup voice key recovery survived key release");
            }

            var testResampler = new LinearPcmUpsampler();
            byte[] resampled = testResampler.Convert(new short[] { 0, 300 });
            if (resampled.Length != 24 || BitConverter.ToInt16(resampled, 12) != 100 ||
                BitConverter.ToInt16(resampled, 14) != 100 || BitConverter.ToInt16(resampled, 20) != 300)
                throw new InvalidOperationException("16-to-48 kHz stereo conversion failed");

            int waitCycles = 0;
            int preemptWaitMs;
            SessionPanelWaitResult preemptResult = SessionPanelWaitPolicy.Wait(
                delegate { return true; },
                delegate { return waitCycles >= 1; },
                delegate(int ignored) { waitCycles++; },
                5000, 50, out preemptWaitMs);
            if (preemptResult != SessionPanelWaitResult.Superseded || waitCycles != 1 || preemptWaitMs != 50)
                throw new InvalidOperationException("New transcription generation did not preempt stale panel wait");

            string leaseMarker = Path.Combine(Path.GetTempPath(), "vibe-mic-endpoint-lease-test-" + Guid.NewGuid().ToString("N") + ".txt");
            var fakeEndpointPolicy = new FakeCaptureEndpointPolicy();
            try
            {
                using (var lease = new DefaultCaptureEndpointLease("CABLE Output", leaseMarker,
                    delegate(string ignored) { }, fakeEndpointPolicy))
                {
                    if (!lease.Acquire(41) || !File.Exists(leaseMarker) || !fakeEndpointPolicy.AllRolesUse("cable-output"))
                        throw new InvalidOperationException("Default capture endpoint lease acquisition failed");
                    if (!lease.Acquire(42)) throw new InvalidOperationException("Default capture endpoint lease transfer failed");
                    lease.Release(41, "superseded_test");
                    if (!fakeEndpointPolicy.AllRolesUse("cable-output"))
                        throw new InvalidOperationException("Superseded endpoint lease restored too early");
                    lease.Release(42, "completed_test");
                    if (!fakeEndpointPolicy.UsesOriginalRoles() || File.Exists(leaseMarker))
                        throw new InvalidOperationException("Default capture endpoint lease restoration failed");
                }
            }
            finally
            {
                if (File.Exists(leaseMarker)) File.Delete(leaseMarker);
                if (File.Exists(leaseMarker + ".tmp")) File.Delete(leaseMarker + ".tmp");
            }
            Console.WriteLine("Vibe Mic voice pipeline self-test passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Voice pipeline self-test failed: " + ex.Message);
            return 1;
        }
    }

    private static int RunSinkClockTest(string endpoint)
    {
        try
        {
            using (var sink = new ClockedVirtualMicSink(endpoint))
            {
                Thread.Sleep(200);
                sink.ResetSessionMetrics();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                sink.Write(new short[16000 * 5]);
                sink.Drain(12000);
                timer.Stop();
                string result = "Virtual microphone clock test audio_ms=5000 elapsed_ms=" + timer.ElapsedMilliseconds +
                    " queue_max=" + sink.MaximumQueueDepth + " drops=" + sink.DroppedBlocks +
                    " pending=" + sink.PendingBlocks;
                Console.WriteLine(result);
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sink-clock-test.txt"), result, Utf8NoBom);
                return sink.PendingBlocks == 0 && timer.ElapsedMilliseconds >= 4500 && timer.ElapsedMilliseconds <= 6000 ? 0 : 3;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Virtual microphone clock test failed: " + ex.Message);
            return 1;
        }
    }

    private static int RunEndpointRouteTest()
    {
        string marker = Path.Combine(Path.GetTempPath(), "vibe-mic-endpoint-route-test-" + Guid.NewGuid().ToString("N") + ".txt");
        DefaultCaptureEndpointLease lease = null;
        try
        {
            lease = new DefaultCaptureEndpointLease("CABLE Output", marker, Console.WriteLine);
            if (!lease.Acquire(1))
            {
                Console.Error.WriteLine("Default capture endpoint route test could not acquire CABLE Output.");
                return 4;
            }
            Thread.Sleep(80);
            lease.Release(1, "reversible_endpoint_test");
            if (File.Exists(marker))
            {
                Console.Error.WriteLine("Default capture endpoint route test left a pending recovery marker.");
                return 5;
            }
            Console.WriteLine("Default capture endpoint route test passed and restored all roles.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Default capture endpoint route test failed: " + ex.Message);
            return 1;
        }
        finally
        {
            if (lease != null) lease.Dispose();
        }
    }
}

internal sealed class AdpcmFrameAccumulator
{
    private readonly int frameSize;
    private readonly List<byte> pending = new List<byte>();

    public AdpcmFrameAccumulator(int size)
    {
        if (size <= 0) throw new ArgumentOutOfRangeException("size");
        frameSize = size;
    }

    public int PendingCount { get { return pending.Count; } }

    public List<byte[]> Append(byte[] data)
    {
        var frames = new List<byte[]>();
        if (data == null || data.Length == 0) return frames;
        pending.AddRange(data);
        while (pending.Count >= frameSize)
        {
            byte[] frame = pending.GetRange(0, frameSize).ToArray();
            pending.RemoveRange(0, frameSize);
            frames.Add(frame);
        }
        return frames;
    }

    public void Reset()
    {
        pending.Clear();
    }
}

internal enum CaptureEndpointRole
{
    Console = 0,
    Multimedia = 1,
    Communications = 2
}

internal interface ICaptureEndpointPolicy : IDisposable
{
    string FindActiveCaptureEndpointId(string requestedName);
    string GetDefaultCaptureEndpointId(CaptureEndpointRole role);
    string GetFriendlyName(string endpointId);
    int SetDefaultCaptureEndpoint(string endpointId, CaptureEndpointRole role);
}

internal sealed class WindowsCaptureEndpointPolicy : ICaptureEndpointPolicy
{
    private readonly MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

    public string FindActiveCaptureEndpointId(string requestedName)
    {
        string expected = string.IsNullOrWhiteSpace(requestedName) ? "CABLE Output" : requestedName.Trim();
        string bestId = null;
        int bestScore = 0;
        foreach (MMDevice candidate in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            try
            {
                string name = candidate.FriendlyName ?? "";
                int score = name.Equals(expected, StringComparison.OrdinalIgnoreCase) ? 3 :
                    name.StartsWith(expected + " ", StringComparison.OrdinalIgnoreCase) ? 2 :
                    name.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestId = candidate.ID;
                }
            }
            finally { candidate.Dispose(); }
        }
        return bestId;
    }

    public string GetDefaultCaptureEndpointId(CaptureEndpointRole role)
    {
        using (MMDevice endpoint = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, ToNaudioRole(role)))
            return endpoint.ID;
    }

    public string GetFriendlyName(string endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId)) return "none";
        try
        {
            using (MMDevice endpoint = enumerator.GetDevice(endpointId))
                return endpoint.FriendlyName ?? endpointId;
        }
        catch { return "unavailable"; }
    }

    public int SetDefaultCaptureEndpoint(string endpointId, CaptureEndpointRole role)
    {
        object client = null;
        try
        {
            client = new PolicyConfigClient();
            return ((IPolicyConfig)client).SetDefaultEndpoint(endpointId, role);
        }
        finally
        {
            if (client != null && Marshal.IsComObject(client)) Marshal.FinalReleaseComObject(client);
        }
    }

    public void Dispose()
    {
        enumerator.Dispose();
    }

    private static Role ToNaudioRole(CaptureEndpointRole role)
    {
        if (role == CaptureEndpointRole.Communications) return Role.Communications;
        if (role == CaptureEndpointRole.Multimedia) return Role.Multimedia;
        return Role.Console;
    }

    [ComImport]
    [Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")]
    private class PolicyConfigClient { }

    [ComImport]
    [Guid("F8679F50-850A-41CF-9C72-430F290290C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int defaultFormat, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr endpointFormat, IntPtr mixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int defaultPeriod, IntPtr period, IntPtr minimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr period);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr propertyKey, IntPtr propertyValue);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string deviceId, IntPtr propertyKey, IntPtr propertyValue);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, CaptureEndpointRole role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int visible);
    }
}

internal sealed class DefaultCaptureEndpointLease : IDisposable
{
    private static readonly CaptureEndpointRole[] Roles =
    {
        CaptureEndpointRole.Console,
        CaptureEndpointRole.Multimedia,
        CaptureEndpointRole.Communications
    };

    private readonly object sync = new object();
    private readonly string targetName;
    private readonly string markerPath;
    private readonly Action<string> log;
    private readonly ICaptureEndpointPolicy policy;
    private Dictionary<CaptureEndpointRole, string> originalEndpoints;
    private int ownerGeneration;
    private bool routed;
    private bool markerInvalid;
    private bool disposed;

    public DefaultCaptureEndpointLease(string requestedTargetName, string recoveryMarkerPath, Action<string> logger)
        : this(requestedTargetName, recoveryMarkerPath, logger, new WindowsCaptureEndpointPolicy()) { }

    internal DefaultCaptureEndpointLease(string requestedTargetName, string recoveryMarkerPath, Action<string> logger,
        ICaptureEndpointPolicy endpointPolicy)
    {
        targetName = string.IsNullOrWhiteSpace(requestedTargetName) ? "CABLE Output" : requestedTargetName;
        markerPath = recoveryMarkerPath;
        log = logger ?? delegate { };
        policy = endpointPolicy;
        RecoverStaleLease();
    }

    public bool Acquire(int generation)
    {
        lock (sync)
        {
            if (disposed || markerInvalid) return false;
            if (routed)
            {
                int previousOwner = ownerGeneration;
                ownerGeneration = generation;
                log("DEFAULT CAPTURE ROUTE TRANSFERRED previous_generation=" + previousOwner +
                    " generation=" + generation + " target=" + targetName.Replace(' ', '_'));
                return true;
            }

            string targetId;
            try { targetId = policy.FindActiveCaptureEndpointId(targetName); }
            catch (Exception ex)
            {
                log("DEFAULT CAPTURE ROUTE FAILED generation=" + generation + " phase=find_target error=" + ex.Message);
                return false;
            }
            if (string.IsNullOrWhiteSpace(targetId))
            {
                log("DEFAULT CAPTURE ROUTE FAILED generation=" + generation + " phase=find_target target=" +
                    targetName.Replace(' ', '_'));
                return false;
            }

            if (originalEndpoints == null)
            {
                originalEndpoints = SnapshotDefaults();
                if (originalEndpoints.Count == 0)
                {
                    originalEndpoints = null;
                    log("DEFAULT CAPTURE ROUTE FAILED generation=" + generation + " phase=snapshot_defaults");
                    return false;
                }
                if (!WriteMarker(originalEndpoints))
                {
                    originalEndpoints = null;
                    log("DEFAULT CAPTURE ROUTE FAILED generation=" + generation + " phase=write_recovery_marker");
                    return false;
                }
            }

            var timer = System.Diagnostics.Stopwatch.StartNew();
            bool applied = SetAllRoles(targetId, "acquire");
            if (applied) applied = VerifyAllRoles(targetId);
            timer.Stop();
            if (!applied)
            {
                RestoreOriginals("acquire_rollback");
                log("DEFAULT CAPTURE ROUTE FAILED generation=" + generation + " phase=apply_or_verify");
                return false;
            }

            routed = true;
            ownerGeneration = generation;
            log("DEFAULT CAPTURE ROUTE ACQUIRED generation=" + generation + " target=" +
                SafeFriendlyName(targetId).Replace(' ', '_') + " elapsed_ms=" + timer.ElapsedMilliseconds);
            return true;
        }
    }

    public void Release(int generation, string reason)
    {
        lock (sync)
        {
            if (disposed || originalEndpoints == null) return;
            if (routed && ownerGeneration != generation)
            {
                log("DEFAULT CAPTURE ROUTE RELEASE SKIPPED generation=" + generation +
                    " owner_generation=" + ownerGeneration + " reason=superseded");
                return;
            }
            RestoreOriginals(reason);
        }
    }

    private void RecoverStaleLease()
    {
        lock (sync)
        {
            if (string.IsNullOrWhiteSpace(markerPath) || !File.Exists(markerPath)) return;
            originalEndpoints = ReadMarker();
            if (originalEndpoints == null || originalEndpoints.Count == 0)
            {
                markerInvalid = true;
                log("DEFAULT CAPTURE ROUTE RECOVERY BLOCKED reason=invalid_marker path=" + markerPath);
                return;
            }
            log("DEFAULT CAPTURE ROUTE RECOVERY START roles=" + originalEndpoints.Count);
            RestoreOriginals("startup_recovery");
        }
    }

    private Dictionary<CaptureEndpointRole, string> SnapshotDefaults()
    {
        var snapshot = new Dictionary<CaptureEndpointRole, string>();
        foreach (CaptureEndpointRole role in Roles)
        {
            try
            {
                string endpointId = policy.GetDefaultCaptureEndpointId(role);
                if (!string.IsNullOrWhiteSpace(endpointId)) snapshot[role] = endpointId;
            }
            catch (Exception ex)
            {
                log("DEFAULT CAPTURE SNAPSHOT FAILED role=" + role.ToString().ToLowerInvariant() + " error=" + ex.Message);
            }
        }
        return snapshot;
    }

    private bool SetAllRoles(string endpointId, string phase)
    {
        bool success = true;
        foreach (CaptureEndpointRole role in Roles)
        {
            try
            {
                int result = policy.SetDefaultCaptureEndpoint(endpointId, role);
                if (result < 0)
                {
                    success = false;
                    log("DEFAULT CAPTURE SET FAILED phase=" + phase + " role=" + role.ToString().ToLowerInvariant() +
                        " hresult=0x" + result.ToString("X8"));
                }
            }
            catch (Exception ex)
            {
                success = false;
                log("DEFAULT CAPTURE SET FAILED phase=" + phase + " role=" + role.ToString().ToLowerInvariant() +
                    " error=" + ex.Message);
            }
        }
        return success;
    }

    private bool VerifyAllRoles(string expectedId)
    {
        foreach (CaptureEndpointRole role in Roles)
        {
            try
            {
                string current = policy.GetDefaultCaptureEndpointId(role);
                if (!string.Equals(current, expectedId, StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch { return false; }
        }
        return true;
    }

    private bool VerifyOriginals()
    {
        foreach (KeyValuePair<CaptureEndpointRole, string> original in originalEndpoints)
        {
            try
            {
                string current = policy.GetDefaultCaptureEndpointId(original.Key);
                if (!string.Equals(current, original.Value, StringComparison.OrdinalIgnoreCase)) return false;
            }
            catch { return false; }
        }
        return true;
    }

    private bool RestoreOriginals(string reason)
    {
        if (originalEndpoints == null || originalEndpoints.Count == 0) return true;
        bool restored = true;
        foreach (KeyValuePair<CaptureEndpointRole, string> original in originalEndpoints)
        {
            try
            {
                int result = policy.SetDefaultCaptureEndpoint(original.Value, original.Key);
                if (result < 0)
                {
                    restored = false;
                    log("DEFAULT CAPTURE RESTORE FAILED role=" + original.Key.ToString().ToLowerInvariant() +
                        " hresult=0x" + result.ToString("X8") + " reason=" + reason);
                }
            }
            catch (Exception ex)
            {
                restored = false;
                log("DEFAULT CAPTURE RESTORE FAILED role=" + original.Key.ToString().ToLowerInvariant() +
                    " error=" + ex.Message + " reason=" + reason);
            }
        }
        if (restored) restored = VerifyOriginals();
        routed = false;
        ownerGeneration = 0;
        if (restored)
        {
            DeleteMarker();
            originalEndpoints = null;
            log("DEFAULT CAPTURE ROUTE RESTORED reason=" + reason);
        }
        else log("DEFAULT CAPTURE ROUTE RESTORE PENDING reason=" + reason + " marker_preserved=true");
        return restored;
    }

    private bool WriteMarker(Dictionary<CaptureEndpointRole, string> endpoints)
    {
        try
        {
            string directory = Path.GetDirectoryName(markerPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            string temporaryPath = markerPath + ".tmp";
            var lines = new List<string>();
            lines.Add("VERSION=1");
            lines.Add("CREATED_UTC=" + DateTime.UtcNow.ToString("o"));
            lines.Add("PROCESS_ID=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            foreach (CaptureEndpointRole role in Roles)
            {
                string endpointId;
                if (endpoints.TryGetValue(role, out endpointId))
                    lines.Add("ROLE_" + role.ToString().ToUpperInvariant() + "=" + Encode(endpointId));
            }
            File.WriteAllLines(temporaryPath, lines.ToArray(), new UTF8Encoding(false));
            if (File.Exists(markerPath)) File.Delete(markerPath);
            File.Move(temporaryPath, markerPath);
            return true;
        }
        catch (Exception ex)
        {
            log("DEFAULT CAPTURE MARKER WRITE FAILED error=" + ex.Message);
            return false;
        }
    }

    private Dictionary<CaptureEndpointRole, string> ReadMarker()
    {
        try
        {
            var endpoints = new Dictionary<CaptureEndpointRole, string>();
            foreach (string line in File.ReadAllLines(markerPath, Encoding.UTF8))
            {
                int separator = line.IndexOf('=');
                if (separator <= 0 || !line.StartsWith("ROLE_", StringComparison.OrdinalIgnoreCase)) continue;
                CaptureEndpointRole role;
                if (!Enum.TryParse(line.Substring(5, separator - 5), true, out role)) continue;
                string endpointId = Decode(line.Substring(separator + 1));
                if (!string.IsNullOrWhiteSpace(endpointId)) endpoints[role] = endpointId;
            }
            return endpoints;
        }
        catch (Exception ex)
        {
            log("DEFAULT CAPTURE MARKER READ FAILED error=" + ex.Message);
            return null;
        }
    }

    private string SafeFriendlyName(string endpointId)
    {
        try { return policy.GetFriendlyName(endpointId); }
        catch { return targetName; }
    }

    private void DeleteMarker()
    {
        try
        {
            if (File.Exists(markerPath)) File.Delete(markerPath);
            if (File.Exists(markerPath + ".tmp")) File.Delete(markerPath + ".tmp");
        }
        catch (Exception ex) { log("DEFAULT CAPTURE MARKER DELETE FAILED error=" + ex.Message); }
    }

    private static string Encode(string value)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? ""));
    }

    private static string Decode(string value)
    {
        try { return Encoding.UTF8.GetString(Convert.FromBase64String(value ?? "")); }
        catch { return ""; }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            if (originalEndpoints != null) RestoreOriginals("capture_shutdown");
            disposed = true;
            policy.Dispose();
        }
    }
}

internal sealed class FakeCaptureEndpointPolicy : ICaptureEndpointPolicy
{
    private readonly Dictionary<CaptureEndpointRole, string> defaults = new Dictionary<CaptureEndpointRole, string>();

    public FakeCaptureEndpointPolicy()
    {
        defaults[CaptureEndpointRole.Console] = "original-console";
        defaults[CaptureEndpointRole.Multimedia] = "original-multimedia";
        defaults[CaptureEndpointRole.Communications] = "original-communications";
    }

    public string FindActiveCaptureEndpointId(string requestedName) { return "cable-output"; }
    public string GetDefaultCaptureEndpointId(CaptureEndpointRole role) { return defaults[role]; }
    public string GetFriendlyName(string endpointId) { return endpointId; }

    public int SetDefaultCaptureEndpoint(string endpointId, CaptureEndpointRole role)
    {
        defaults[role] = endpointId;
        return 0;
    }

    public bool AllRolesUse(string endpointId)
    {
        return defaults.Values.All(value => value == endpointId);
    }

    public bool UsesOriginalRoles()
    {
        return defaults[CaptureEndpointRole.Console] == "original-console" &&
            defaults[CaptureEndpointRole.Multimedia] == "original-multimedia" &&
            defaults[CaptureEndpointRole.Communications] == "original-communications";
    }

    public void Dispose() { }
}

internal interface ITranscriptionSessionController : IDisposable
{
    void Start(int generation);
    void Stop(int generation, bool audioDelivered);
    bool IsReady(int generation);
}

internal static class TranscriptionSessionControllerFactory
{
    public static ITranscriptionSessionController Create(string provider, string hotkey, string triggerMode,
        int startupDelayMs, Func<int, bool> activeCheck, Action<string> logger, Action<int> audioFinalizer,
        Func<int, bool> prepareInput, Action<int> releaseInputWithoutAudio)
    {
        string normalized = string.IsNullOrWhiteSpace(provider) ? "wechat" : provider.Trim().ToLowerInvariant();
        if (normalized == "wechat")
            return new WeTypeVoiceSessionController(hotkey, activeCheck, logger, audioFinalizer,
                prepareInput, releaseInputWithoutAudio);
        return new HotkeyTranscriptionSessionController(normalized, hotkey, triggerMode, startupDelayMs,
            activeCheck, logger, audioFinalizer, prepareInput, releaseInputWithoutAudio);
    }
}

internal enum SessionPanelWaitResult
{
    Closed,
    Superseded,
    TimedOut
}

internal static class SessionPanelWaitPolicy
{
    public static SessionPanelWaitResult Wait(Func<bool> panelOpen, Func<bool> superseded,
        Action<int> sleep, int timeoutMs, int pollMs, out int waitedMs)
    {
        waitedMs = 0;
        int boundedTimeout = Math.Max(0, timeoutMs);
        int boundedPoll = Math.Max(1, pollMs);
        while (panelOpen())
        {
            if (superseded()) return SessionPanelWaitResult.Superseded;
            if (waitedMs >= boundedTimeout) return SessionPanelWaitResult.TimedOut;
            int delay = Math.Min(boundedPoll, boundedTimeout - waitedMs);
            sleep(delay);
            waitedMs += delay;
        }
        return SessionPanelWaitResult.Closed;
    }
}

internal sealed class WeTypeVoiceSessionController : ITranscriptionSessionController
{
    private const int WM_CLOSE = 0x0010;
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const uint KeyEventKeyUp = 0x0002;
    private readonly BlockingCollection<VoiceSessionCommand> commands = new BlockingCollection<VoiceSessionCommand>();
    private readonly Func<int, bool> isSessionActive;
    private readonly Action<string> log;
    private readonly Action<int> finalizeAudio;
    private readonly Func<int, bool> prepareInput;
    private readonly Action<int> releaseInputWithoutAudio;
    private readonly string hotkey;
    private readonly Thread worker;
    private int readyGeneration;
    private int latestStartGeneration;
    private int hotkeyGeneration;
    private int toolbarGeneration;
    private volatile bool disposed;

    public WeTypeVoiceSessionController(string configuredHotkey, Func<int, bool> activeCheck, Action<string> logger,
        Action<int> audioFinalizer, Func<int, bool> inputPreparer, Action<int> inputReleaseWithoutAudio)
    {
        hotkey = string.IsNullOrWhiteSpace(configuredHotkey) ? "ctrl+win" : configuredHotkey;
        isSessionActive = activeCheck;
        log = logger;
        finalizeAudio = audioFinalizer;
        prepareInput = inputPreparer;
        releaseInputWithoutAudio = inputReleaseWithoutAudio;
        worker = new Thread(ProcessCommands);
        worker.IsBackground = true;
        worker.Name = "Vibe Mic WeType session coordinator";
        worker.Start();
    }

    public void Start(int generation)
    {
        if (disposed) return;
        Interlocked.Exchange(ref latestStartGeneration, generation);
        Interlocked.Exchange(ref readyGeneration, 0);
        commands.Add(new VoiceSessionCommand { Generation = generation, Start = true });
    }

    public void Stop(int generation, bool audioDelivered)
    {
        if (disposed) return;
        commands.Add(new VoiceSessionCommand { Generation = generation, Start = false, AudioDelivered = audioDelivered });
    }

    public bool IsReady(int generation)
    {
        return Volatile.Read(ref readyGeneration) == generation;
    }

    private void ProcessCommands()
    {
        foreach (VoiceSessionCommand command in commands.GetConsumingEnumerable())
        {
            try
            {
                if (command.Start) BeginSession(command.Generation);
                else EndSession(command.Generation, command.AudioDelivered);
            }
            catch (Exception ex)
            {
                log("WETYPE SESSION ERROR generation=" + command.Generation + " error=" + ex.Message);
            }
        }
    }

    private void BeginSession(int generation)
    {
        if (disposed || Volatile.Read(ref latestStartGeneration) != generation || !isSessionActive(generation))
        {
            log("WETYPE SESSION START superseded generation=" + generation);
            return;
        }

        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool routed = prepareInput == null || prepareInput(generation);
        log("TRANSCRIPTION INPUT ROUTE provider=wechat generation=" + generation + " ready=" + routed);
        CloseStalePanel(generation);

        if (TryToolbarClick(generation, 1, true, "start"))
        {
            Interlocked.Exchange(ref toolbarGeneration, generation);
            if (WaitForPanel(generation, 300))
            {
                MarkReady(generation, "toolbar_primary", 1, timer.ElapsedMilliseconds);
                return;
            }
        }

        if (TapVoiceHotkey(generation, "start_fallback"))
        {
            Interlocked.Exchange(ref hotkeyGeneration, generation);
            if (WaitForPanel(generation, 500))
            {
                MarkReady(generation, "hotkey_fallback", 1, timer.ElapsedMilliseconds);
                return;
            }
        }

        if (TryToolbarClick(generation, 2, true, "start_retry"))
        {
            Interlocked.Exchange(ref toolbarGeneration, generation);
            if (WaitForPanel(generation, 400))
            {
                MarkReady(generation, "toolbar_retry", 2, timer.ElapsedMilliseconds);
                return;
            }
        }
        log("WETYPE PANEL UNAVAILABLE generation=" + generation + " trigger_elapsed_ms=" + timer.ElapsedMilliseconds);
    }

    private bool TryToolbarClick(int generation, int attempt, bool requireActiveSession, string phase)
    {
        if (disposed || (requireActiveSession && (!isSessionActive(generation) ||
            Volatile.Read(ref latestStartGeneration) != generation))) return false;
        IntPtr toolbar = FindWindowByClass("wetype.statusbar.window", false);
        if (toolbar == IntPtr.Zero)
        {
            log("WETYPE TOOLBAR unavailable generation=" + generation + " phase=" + phase + " attempt=" + attempt);
            return false;
        }

        RECT client;
        if (!GetClientRect(toolbar, out client) || client.Right <= 0 || client.Bottom <= 0)
        {
            log("WETYPE TOOLBAR invalid_rect generation=" + generation + " phase=" + phase + " attempt=" + attempt);
            return false;
        }

        int x = Math.Max(1, client.Right * 45 / 142);
        int y = Math.Max(1, client.Bottom / 2);
        IntPtr point = new IntPtr((y << 16) | (x & 0xFFFF));
        bool down = PostMessage(toolbar, WM_LBUTTONDOWN, new IntPtr(1), point);
        bool up = PostMessage(toolbar, WM_LBUTTONUP, IntPtr.Zero, point);
        log("WETYPE TOOLBAR CLICK generation=" + generation + " phase=" + phase + " attempt=" + attempt + " sent=" + (down && up));
        return down && up;
    }

    private void MarkReady(int generation, string source, int attempt, long elapsedMs)
    {
        if (!isSessionActive(generation)) return;
        Interlocked.Exchange(ref readyGeneration, generation);
        log("WETYPE PANEL READY generation=" + generation + " source=" + source + " attempt=" + attempt +
            " trigger_to_ready_ms=" + elapsedMs);
        log("TRANSCRIPTION READY provider=wechat generation=" + generation + " trigger_to_ready_ms=" + elapsedMs);
    }

    private bool TapVoiceHotkey(int generation, string phase)
    {
        bool sent = KeyboardShortcutSender.Tap(hotkey, 80);
        log("WETYPE HOTKEY TAP generation=" + generation + " phase=" + phase + " shortcut=" +
            hotkey.Replace(' ', '_') + " sent=" + sent);
        return sent;
    }

    private void EndSession(int generation, bool audioDelivered)
    {
        if (audioDelivered) finalizeAudio(generation);
        else if (releaseInputWithoutAudio != null) releaseInputWithoutAudio(generation);
        if (Volatile.Read(ref readyGeneration) == generation) Interlocked.Exchange(ref readyGeneration, 0);

        if (PreemptIfSuperseded(generation, "after_audio_drain", 0)) return;

        IntPtr panel = FindVoicePanelWindow();
        bool submitted = false;
        if (audioDelivered && panel != IntPtr.Zero)
        {
            if (Volatile.Read(ref hotkeyGeneration) == generation)
            {
                submitted = TapVoiceHotkey(generation, "submit_after_audio_drained");
                int hotkeyWaitMs;
                SessionPanelWaitResult hotkeyWait = SessionPanelWaitPolicy.Wait(
                    delegate { return FindVoicePanelWindow() != IntPtr.Zero && !disposed; },
                    delegate { return HasNewerStart(generation); },
                    delegate(int delay) { Thread.Sleep(delay); },
                    400, 50, out hotkeyWaitMs);
                if (hotkeyWait == SessionPanelWaitResult.Superseded)
                {
                    PreemptIfSuperseded(generation, "hotkey_submit_wait", hotkeyWaitMs);
                    return;
                }
                if (FindVoicePanelWindow() != IntPtr.Zero)
                    submitted = TryToolbarClick(generation, 1, false, "submit_fallback");
            }
            else if (Volatile.Read(ref toolbarGeneration) == generation)
                submitted = TryToolbarClick(generation, 1, false, "submit");
        }
        else if (audioDelivered) submitted = true;
        else if (panel != IntPtr.Zero)
        {
            PostMessage(panel, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            log("WETYPE PANEL CLOSED generation=" + generation + " reason=no_audio_delivered");
        }
        Interlocked.CompareExchange(ref hotkeyGeneration, 0, generation);
        Interlocked.CompareExchange(ref toolbarGeneration, 0, generation);
        log("WETYPE TRANSCRIPTION SUBMIT generation=" + generation + " sent=" + submitted +
            " audio_delivered=" + audioDelivered);

        int waitedMs;
        SessionPanelWaitResult panelWait = SessionPanelWaitPolicy.Wait(
            delegate { return FindVoicePanelWindow() != IntPtr.Zero && !disposed; },
            delegate { return HasNewerStart(generation); },
            delegate(int delay) { Thread.Sleep(delay); },
            5000, 50, out waitedMs);
        if (panelWait == SessionPanelWaitResult.Superseded)
        {
            PreemptIfSuperseded(generation, "panel_completion_wait", waitedMs);
            return;
        }
        panel = FindVoicePanelWindow();
        if (panel != IntPtr.Zero)
        {
            PostMessage(panel, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            log("WETYPE PANEL CLOSED generation=" + generation + " reason=submit_timeout waited_ms=" + waitedMs);
        }
        log("WETYPE SESSION END generation=" + generation + " audio_delivered=" + audioDelivered +
            " submitted=" + submitted + " panel_wait_ms=" + waitedMs);
    }

    private bool HasNewerStart(int generation)
    {
        return Volatile.Read(ref latestStartGeneration) > generation;
    }

    private bool PreemptIfSuperseded(int generation, string phase, int waitedMs)
    {
        int newerGeneration = Volatile.Read(ref latestStartGeneration);
        if (newerGeneration <= generation) return false;
        if (Volatile.Read(ref readyGeneration) == generation) Interlocked.Exchange(ref readyGeneration, 0);
        Interlocked.CompareExchange(ref hotkeyGeneration, 0, generation);
        Interlocked.CompareExchange(ref toolbarGeneration, 0, generation);
        IntPtr panel = FindVoicePanelWindow();
        if (panel != IntPtr.Zero) PostMessage(panel, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        log("WETYPE SESSION PREEMPTED generation=" + generation + " newer_generation=" + newerGeneration +
            " phase=" + phase + " waited_ms=" + waitedMs + " stale_panel_closed=" + (panel != IntPtr.Zero));
        return true;
    }

    private void CloseStalePanel(int generation)
    {
        IntPtr panel = FindVoicePanelWindow();
        if (panel == IntPtr.Zero) return;
        PostMessage(panel, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        int waitedMs = 0;
        while (panel != IntPtr.Zero && waitedMs < 150 && !disposed)
        {
            Thread.Sleep(25);
            waitedMs += 25;
            panel = FindVoicePanelWindow();
        }
        log("WETYPE STALE PANEL CLOSED generation=" + generation + " waited_ms=" + waitedMs);
    }

    private bool WaitForPanel(int generation, int timeoutMs)
    {
        int started = Environment.TickCount;
        while (unchecked(Environment.TickCount - started) < timeoutMs)
        {
            if (disposed || !isSessionActive(generation)) return false;
            if (FindVoicePanelWindow() != IntPtr.Zero) return true;
            Thread.Sleep(25);
        }
        return false;
    }

    private static IntPtr FindVoicePanelWindow()
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            if (!IsWindowVisible(window)) return true;
            var title = new StringBuilder(256);
            GetWindowText(window, title, title.Capacity);
            if (title.ToString().IndexOf("语音输入", StringComparison.OrdinalIgnoreCase) < 0) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    private static IntPtr FindWindowByClass(string expectedClass, bool visibleOnly)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate(IntPtr window, IntPtr parameter)
        {
            if (visibleOnly && !IsWindowVisible(window)) return true;
            var className = new StringBuilder(128);
            GetClassName(window, className, className.Capacity);
            if (!className.ToString().Equals(expectedClass, StringComparison.OrdinalIgnoreCase)) return true;
            found = window;
            return false;
        }, IntPtr.Zero);
        return found;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        commands.CompleteAdding();
        bool workerStopped = worker == null || worker.Join(3000);
        IntPtr panel = FindVoicePanelWindow();
        if (panel != IntPtr.Zero)
        {
            PostMessage(panel, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            log("WETYPE PANEL CLOSED reason=controller_dispose");
        }
        if (workerStopped) commands.Dispose();
    }

    private sealed class VoiceSessionCommand
    {
        public int Generation;
        public bool Start;
        public bool AudioDelivered;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out RECT rectangle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

}

internal sealed class HotkeyTranscriptionSessionController : ITranscriptionSessionController
{
    private readonly BlockingCollection<Command> commands = new BlockingCollection<Command>();
    private readonly string provider;
    private readonly string hotkey;
    private readonly bool holdMode;
    private readonly int startupDelayMs;
    private readonly Func<int, bool> isSessionActive;
    private readonly Action<string> log;
    private readonly Action<int> finalizeAudio;
    private readonly Func<int, bool> prepareInput;
    private readonly Action<int> releaseInputWithoutAudio;
    private readonly Thread worker;
    private int latestStartGeneration;
    private int startedGeneration;
    private int readyGeneration;
    private volatile bool disposed;

    public HotkeyTranscriptionSessionController(string providerName, string configuredHotkey, string triggerMode,
        int configuredStartupDelayMs, Func<int, bool> activeCheck, Action<string> logger, Action<int> audioFinalizer,
        Func<int, bool> inputPreparer, Action<int> inputReleaseWithoutAudio)
    {
        provider = string.IsNullOrWhiteSpace(providerName) ? "custom" : providerName;
        hotkey = string.IsNullOrWhiteSpace(configuredHotkey) ? "ctrl+win" : configuredHotkey;
        holdMode = string.Equals(triggerMode, "hold", StringComparison.OrdinalIgnoreCase);
        startupDelayMs = Math.Max(20, Math.Min(2000, configuredStartupDelayMs));
        isSessionActive = activeCheck;
        log = logger;
        finalizeAudio = audioFinalizer;
        prepareInput = inputPreparer;
        releaseInputWithoutAudio = inputReleaseWithoutAudio;
        worker = new Thread(ProcessCommands);
        worker.IsBackground = true;
        worker.Name = "Vibe Mic transcription session coordinator";
        worker.Start();
    }

    public void Start(int generation)
    {
        if (disposed) return;
        Interlocked.Exchange(ref latestStartGeneration, generation);
        Interlocked.Exchange(ref readyGeneration, 0);
        commands.Add(new Command { Generation = generation, Start = true });
    }

    public void Stop(int generation, bool audioDelivered)
    {
        if (disposed) return;
        commands.Add(new Command { Generation = generation, Start = false, AudioDelivered = audioDelivered });
    }

    public bool IsReady(int generation)
    {
        return Volatile.Read(ref readyGeneration) == generation;
    }

    private void ProcessCommands()
    {
        foreach (Command command in commands.GetConsumingEnumerable())
        {
            try
            {
                if (command.Start) BeginSession(command.Generation);
                else EndSession(command.Generation, command.AudioDelivered);
            }
            catch (Exception ex)
            {
                log("TRANSCRIPTION SESSION ERROR provider=" + provider + " generation=" + command.Generation +
                    " error=" + ex.Message);
            }
        }
    }

    private void BeginSession(int generation)
    {
        if (disposed || Volatile.Read(ref latestStartGeneration) != generation || !isSessionActive(generation)) return;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool routed = prepareInput == null || prepareInput(generation);
        log("TRANSCRIPTION INPUT ROUTE provider=" + provider + " generation=" + generation + " ready=" + routed);
        bool sent = holdMode ? KeyboardShortcutSender.KeyDown(hotkey) : KeyboardShortcutSender.Tap(hotkey, 70);
        log("TRANSCRIPTION TRIGGER provider=" + provider + " generation=" + generation + " phase=start mode=" +
            (holdMode ? "hold" : "toggle") + " shortcut=" + hotkey.Replace(' ', '_') + " sent=" + sent);
        if (!sent) return;
        Interlocked.Exchange(ref startedGeneration, generation);

        int waitedMs = 0;
        while (waitedMs < startupDelayMs && !disposed && isSessionActive(generation))
        {
            int wait = Math.Min(10, startupDelayMs - waitedMs);
            Thread.Sleep(wait);
            waitedMs += wait;
        }
        if (disposed || !isSessionActive(generation)) return;
        Interlocked.Exchange(ref readyGeneration, generation);
        log("TRANSCRIPTION READY provider=" + provider + " generation=" + generation +
            " trigger_to_ready_ms=" + timer.ElapsedMilliseconds);
    }

    private void EndSession(int generation, bool audioDelivered)
    {
        if (Volatile.Read(ref readyGeneration) == generation) Interlocked.Exchange(ref readyGeneration, 0);
        if (audioDelivered) finalizeAudio(generation);
        else if (releaseInputWithoutAudio != null) releaseInputWithoutAudio(generation);

        bool wasStarted = Volatile.Read(ref startedGeneration) == generation;
        bool sent = false;
        if (wasStarted)
            sent = holdMode ? KeyboardShortcutSender.KeyUp(hotkey) : KeyboardShortcutSender.Tap(hotkey, 70);
        Interlocked.CompareExchange(ref startedGeneration, 0, generation);
        log("TRANSCRIPTION SESSION END provider=" + provider + " generation=" + generation +
            " audio_delivered=" + audioDelivered + " mode=" + (holdMode ? "hold" : "toggle") + " sent=" + sent);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        int activeGeneration = Volatile.Read(ref startedGeneration);
        if (holdMode && activeGeneration != 0) KeyboardShortcutSender.KeyUp(hotkey);
        commands.CompleteAdding();
        if (worker != null) worker.Join(2000);
        commands.Dispose();
    }

    private sealed class Command
    {
        public int Generation;
        public bool Start;
        public bool AudioDelivered;
    }
}

internal static class KeyboardShortcutSender
{
    private const uint KeyEventExtendedKey = 0x0001;
    private const uint KeyEventKeyUp = 0x0002;

    public static bool Tap(string shortcut, int holdMs)
    {
        if (!KeyDown(shortcut)) return false;
        Thread.Sleep(Math.Max(30, Math.Min(300, holdMs)));
        return KeyUp(shortcut);
    }

    public static bool KeyDown(string shortcut)
    {
        List<int> keys;
        return TryParse(shortcut, out keys) && Send(keys, false);
    }

    public static bool KeyUp(string shortcut)
    {
        List<int> keys;
        if (!TryParse(shortcut, out keys)) return false;
        keys.Reverse();
        return Send(keys, true);
    }

    internal static bool TryParse(string shortcut, out List<int> keys)
    {
        keys = new List<int>();
        if (string.IsNullOrWhiteSpace(shortcut)) return false;
        string[] parts = shortcut.Split(new char[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (string raw in parts)
        {
            int key = VirtualKeyFromName(raw);
            if (key <= 0 || keys.Contains(key)) return false;
            keys.Add(key);
        }
        return keys.Count > 0 && keys.Count <= 4;
    }

    private static int VirtualKeyFromName(string raw)
    {
        string value = (raw ?? "").Trim().ToLowerInvariant();
        if (value.Length == 1)
        {
            char character = char.ToUpperInvariant(value[0]);
            if ((character >= 'A' && character <= 'Z') || (character >= '0' && character <= '9')) return character;
        }
        if (value.Length >= 2 && value[0] == 'f')
        {
            int number;
            if (int.TryParse(value.Substring(1), out number) && number >= 1 && number <= 24) return 0x70 + number - 1;
        }
        var names = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "ctrl", 0xA2 }, { "control", 0xA2 }, { "leftctrl", 0xA2 }, { "lctrl", 0xA2 },
            { "rightctrl", 0xA3 }, { "rctrl", 0xA3 }, { "win", 0x5B }, { "meta", 0x5B },
            { "leftwin", 0x5B }, { "lwin", 0x5B }, { "rightwin", 0x5C }, { "rwin", 0x5C },
            { "alt", 0xA4 }, { "leftalt", 0xA4 }, { "lalt", 0xA4 }, { "rightalt", 0xA5 }, { "ralt", 0xA5 },
            { "shift", 0xA0 }, { "leftshift", 0xA0 }, { "rightshift", 0xA1 },
            { "space", 0x20 }, { "enter", 0x0D }, { "tab", 0x09 }, { "escape", 0x1B }, { "esc", 0x1B }
        };
        int result;
        return names.TryGetValue(value, out result) ? result : -1;
    }

    private static bool Send(List<int> keys, bool keyUp)
    {
        INPUT[] inputs = new INPUT[keys.Count];
        for (int i = 0; i < keys.Count; i++)
        {
            inputs[i].type = 1;
            inputs[i].u.ki.wVk = (ushort)keys[i];
            inputs[i].u.ki.dwFlags = (keyUp ? KeyEventKeyUp : 0) | (IsExtendedKey(keys[i]) ? KeyEventExtendedKey : 0);
        }
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
        return sent == inputs.Length;
    }

    private static bool IsExtendedKey(int key)
    {
        return key == 0x5B || key == 0x5C || key == 0xA3 || key == 0xA5;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint message;
        public ushort parameterLow;
        public ushort parameterHigh;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);
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

internal sealed class SpeechLeveler
{
    private const int OutputLimit = 30000;
    private const double TargetSpeechRms = 2100.0;
    private readonly bool speechEnhancement;
    private readonly double sensitivity;
    private double currentGain;
    private int previousSample;

    public double LastAppliedGain { get; private set; }

    public SpeechLeveler(string processingMode, double sensitivityValue)
    {
        speechEnhancement = !string.Equals(processingMode, "transparent", StringComparison.OrdinalIgnoreCase);
        sensitivity = Math.Max(0.5, Math.Min(4.0, sensitivityValue));
        BeginSession();
    }

    public void BeginSession()
    {
        double maximum = Math.Min(10.0, 8.0 * sensitivity);
        currentGain = speechEnhancement ? Math.Min(maximum, 4.0 * sensitivity) : sensitivity;
        LastAppliedGain = currentGain;
        previousSample = 0;
    }

    public short[] Process(short[] input)
    {
        if (input == null || input.Length == 0) return new short[0];

        if (!speechEnhancement)
        {
            LastAppliedGain = sensitivity;
            short[] transparent = new short[input.Length];
            for (int i = 0; i < input.Length; i++)
            {
                int scaled = (int)Math.Round(input[i] * sensitivity);
                transparent[i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, scaled));
            }
            return transparent;
        }

        int[] filtered = Smooth(input);
        previousSample = input[input.Length - 1];

        int[] absoluteSamples = new int[filtered.Length];
        for (int i = 0; i < filtered.Length; i++)
            absoluteSamples[i] = filtered[i] == short.MinValue ? 32768 : Math.Abs(filtered[i]);
        Array.Sort(absoluteSamples);
        int percentileIndex = Math.Max(0, Math.Min(absoluteSamples.Length - 1,
            (int)Math.Floor((absoluteSamples.Length - 1) * 0.95)));
        int robustCeiling = Math.Max(1, absoluteSamples[percentileIndex]);
        double squareSum = 0;
        foreach (int sample in filtered)
        {
            int absolute = Math.Min(robustCeiling, sample == short.MinValue ? 32768 : Math.Abs(sample));
            squareSum += (double)absolute * absolute;
        }
        double robustRms = Math.Sqrt(squareSum / filtered.Length);
        if (robustRms >= 80)
        {
            double maximum = Math.Min(10.0, 8.0 * sensitivity);
            double target = Math.Max(0.75, Math.Min(maximum, TargetSpeechRms * sensitivity / robustRms));
            double adjustment = target < currentGain ? 0.45 : 0.10;
            currentGain += (target - currentGain) * adjustment;
        }

        LastAppliedGain = currentGain;
        short[] output = new short[input.Length];
        for (int i = 0; i < filtered.Length; i++)
        {
            int scaled = (int)Math.Round(filtered[i] * currentGain);
            output[i] = (short)Math.Max(-OutputLimit, Math.Min(OutputLimit, scaled));
        }
        return output;
    }

    private int[] Smooth(short[] input)
    {
        int[] filtered = new int[input.Length];
        if (input.Length == 1)
        {
            filtered[0] = (previousSample + 3 * input[0]) >> 2;
            return filtered;
        }
        filtered[0] = (previousSample + 2 * input[0] + input[1]) >> 2;
        for (int i = 1; i < input.Length - 1; i++)
            filtered[i] = (input[i - 1] + 2 * input[i] + input[i + 1]) >> 2;
        filtered[input.Length - 1] = (input[input.Length - 2] + 3 * input[input.Length - 1]) >> 2;
        return filtered;
    }
}

internal sealed class AudioDiagnosticSession
{
    private const int SampleRate = 16000;
    private const int MaximumSeconds = 30;
    private readonly object sampleLock = new object();
    private readonly List<short> rawSamples = new List<short>();
    private readonly List<short> processedSamples = new List<short>();
    private readonly int generation;
    private readonly byte remoteSession;
    private CableOutputDiagnosticCapture cableCapture;
    private string cableCaptureError = "";
    private int stopRequested;
    private int completed;
    private bool samplesTruncated;

    public string DirectoryPath { get; private set; }
    public string CableCaptureStatus
    {
        get
        {
            if (cableCapture != null) return "active_" + Sanitize(cableCapture.FormatDescription);
            return "unavailable_" + Sanitize(cableCaptureError);
        }
    }

    public AudioDiagnosticSession(string parentDirectory, int streamGeneration, byte sessionId, string cableOutputName)
    {
        generation = streamGeneration;
        remoteSession = sessionId;
        DirectoryPath = Path.Combine(parentDirectory, "audio-diagnostic-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-g" + generation);
        Directory.CreateDirectory(DirectoryPath);
        try { cableCapture = new CableOutputDiagnosticCapture(cableOutputName, MaximumSeconds); }
        catch (Exception ex) { cableCaptureError = ex.Message; }
    }

    public void Append(short[] raw, short[] processed)
    {
        if (Volatile.Read(ref completed) != 0 || raw == null || processed == null) return;
        lock (sampleLock)
        {
            int remaining = SampleRate * MaximumSeconds - rawSamples.Count;
            int count = Math.Min(remaining, Math.Min(raw.Length, processed.Length));
            for (int i = 0; i < count; i++)
            {
                rawSamples.Add(raw[i]);
                processedSamples.Add(processed[i]);
            }
            if (count < raw.Length || count < processed.Length) samplesTruncated = true;
        }
    }

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) != 0) return;
        if (cableCapture != null) cableCapture.RequestStop();
    }

    public string Complete(string reason)
    {
        if (Interlocked.Exchange(ref completed, 1) != 0) return "already_completed=true";
        RequestStop();

        short[] raw;
        short[] processed;
        lock (sampleLock)
        {
            raw = rawSamples.ToArray();
            processed = processedSamples.ToArray();
        }

        string rawPath = Path.Combine(DirectoryPath, "01-raw-decoded-16k-mono.wav");
        string processedPath = Path.Combine(DirectoryPath, "02-processed-16k-mono.wav");
        string cablePath = Path.Combine(DirectoryPath, "03-cable-output.wav");
        WritePcm16Wave(rawPath, raw);
        WritePcm16Wave(processedPath, processed);

        int cableBytes = 0;
        string cableResult;
        if (cableCapture != null)
        {
            try
            {
                cableBytes = cableCapture.Finish(cablePath);
                cableResult = "saved";
            }
            catch (Exception ex)
            {
                cableResult = "failed: " + ex.Message;
            }
            finally
            {
                cableCapture.Dispose();
                cableCapture = null;
            }
        }
        else cableResult = "unavailable: " + cableCaptureError;

        var manifest = new StringBuilder();
        manifest.AppendLine("Vibe Flow one-shot audio diagnostic");
        manifest.AppendLine("Created: " + DateTime.Now.ToString("o"));
        manifest.AppendLine("Generation: " + generation);
        manifest.AppendLine("Remote session: " + remoteSession);
        manifest.AppendLine("Completion reason: " + reason);
        manifest.AppendLine("Raw samples: " + raw.Length);
        manifest.AppendLine("Processed samples: " + processed.Length);
        manifest.AppendLine("Input truncated at 30 seconds: " + samplesTruncated);
        manifest.AppendLine("CABLE capture: " + cableResult);
        manifest.AppendLine("These files were created only after explicit user activation and can be deleted safely.");
        File.WriteAllText(Path.Combine(DirectoryPath, "diagnostic-info.txt"), manifest.ToString(), new UTF8Encoding(false));

        return "directory=" + DirectoryPath + " raw_samples=" + raw.Length +
            " processed_samples=" + processed.Length + " cable_bytes=" + cableBytes +
            " truncated=" + samplesTruncated;
    }

    private static void WritePcm16Wave(string path, short[] samples)
    {
        byte[] bytes = new byte[samples.Length * sizeof(short)];
        if (bytes.Length > 0) System.Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        using (var writer = new WaveFileWriter(path, new WaveFormat(SampleRate, 16, 1)))
            writer.Write(bytes, 0, bytes.Length);
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Replace(' ', '_').Replace('\r', '_').Replace('\n', '_');
    }
}

internal sealed class CableOutputDiagnosticCapture : IDisposable
{
    private readonly object dataLock = new object();
    private readonly MemoryStream captured = new MemoryStream();
    private readonly ManualResetEventSlim recordingStopped = new ManualResetEventSlim(false);
    private readonly MMDeviceEnumerator enumerator;
    private readonly MMDevice endpoint;
    private readonly WasapiCapture capture;
    private readonly WaveFormat format;
    private readonly int maximumBytes;
    private int stopRequested;
    private bool disposed;

    public string FormatDescription { get { return format.ToString(); } }

    public CableOutputDiagnosticCapture(string requestedName, int maximumSeconds)
    {
        enumerator = new MMDeviceEnumerator();
        endpoint = FindCaptureDevice(enumerator, requestedName);
        if (endpoint == null)
        {
            enumerator.Dispose();
            throw new InvalidOperationException("Capture endpoint not found: " + requestedName);
        }

        try
        {
            capture = new WasapiCapture(endpoint, false, 20);
            format = capture.WaveFormat;
            maximumBytes = Math.Max(format.BlockAlign, format.AverageBytesPerSecond * Math.Max(1, maximumSeconds));
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            capture.StartRecording();
        }
        catch
        {
            if (capture != null) capture.Dispose();
            endpoint.Dispose();
            enumerator.Dispose();
            recordingStopped.Dispose();
            captured.Dispose();
            throw;
        }
    }

    private void OnDataAvailable(object sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref stopRequested) != 0 || args == null || args.BytesRecorded <= 0) return;
        lock (dataLock)
        {
            int remaining = maximumBytes - (int)captured.Length;
            int count = Math.Min(remaining, args.BytesRecorded);
            count -= count % Math.Max(1, format.BlockAlign);
            if (count > 0) captured.Write(args.Buffer, 0, count);
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs args)
    {
        recordingStopped.Set();
    }

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) != 0) return;
        try { capture.StopRecording(); }
        catch { recordingStopped.Set(); }
    }

    public int Finish(string path)
    {
        RequestStop();
        recordingStopped.Wait(1500);
        byte[] bytes;
        lock (dataLock) bytes = captured.ToArray();
        using (var writer = new WaveFileWriter(path, format)) writer.Write(bytes, 0, bytes.Length);
        return bytes.Length;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RequestStop();
        capture.DataAvailable -= OnDataAvailable;
        capture.RecordingStopped -= OnRecordingStopped;
        capture.Dispose();
        endpoint.Dispose();
        enumerator.Dispose();
        recordingStopped.Dispose();
        captured.Dispose();
    }

    private static MMDevice FindCaptureDevice(MMDeviceEnumerator deviceEnumerator, string requestedName)
    {
        string expected = string.IsNullOrWhiteSpace(requestedName) ? "CABLE Output" : requestedName;
        foreach (MMDevice candidate in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if ((candidate.FriendlyName ?? "").IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
            candidate.Dispose();
        }
        return null;
    }
}

internal sealed class LinearPcmUpsampler
{
    private int previousInputSample;
    private bool hasPreviousInputSample;

    public void Reset()
    {
        previousInputSample = 0;
        hasPreviousInputSample = false;
    }

    public byte[] Convert(short[] mono16k)
    {
        if (mono16k == null || mono16k.Length == 0) return new byte[0];
        byte[] output = new byte[mono16k.Length * 12];
        int position = 0;
        foreach (short sample in mono16k)
        {
            int previous = hasPreviousInputSample ? previousInputSample : sample;
            for (int phase = 1; phase <= 3; phase++)
            {
                int interpolated = (previous * (3 - phase) + sample * phase) / 3;
                short value = (short)interpolated;
                for (int channel = 0; channel < 2; channel++)
                {
                    output[position++] = (byte)(value & 0xFF);
                    output[position++] = (byte)((value >> 8) & 0xFF);
                }
            }
            previousInputSample = sample;
            hasPreviousInputSample = true;
        }
        return output;
    }
}

internal sealed class ClockedVirtualMicSink : IDisposable
{
    private const int BlockSamples = 320;
    private const int OutputSampleRate = 48000;
    private const int OutputChannels = 2;
    private const int OutputBitsPerSample = 16;
    private const int OutputBlockBytes = OutputSampleRate / 50 * OutputChannels * (OutputBitsPerSample / 8);
    private const int DrainGuardMs = 40;
    private readonly object stagingLock = new object();
    private readonly List<short> staging = new List<short>();
    private readonly LinearPcmUpsampler resampler = new LinearPcmUpsampler();
    private readonly MMDeviceEnumerator deviceEnumerator;
    private readonly MMDevice endpoint;
    private readonly BufferedWaveProvider provider;
    private readonly WasapiOut output;
    private int maximumQueueDepth;
    private int droppedBlocks;
    private volatile Exception playbackFailure;
    private volatile bool disposed;
    public string DeviceName { get; private set; }
    public int PendingBlocks
    {
        get
        {
            int bytes = provider == null ? 0 : provider.BufferedBytes;
            return (bytes + OutputBlockBytes - 1) / OutputBlockBytes;
        }
    }
    public int MaximumQueueDepth { get { return Volatile.Read(ref maximumQueueDepth); } }
    public int DroppedBlocks { get { return Volatile.Read(ref droppedBlocks); } }

    public ClockedVirtualMicSink(string requestedName)
    {
        deviceEnumerator = new MMDeviceEnumerator();
        endpoint = FindDevice(deviceEnumerator, requestedName);
        if (endpoint == null)
        {
            deviceEnumerator.Dispose();
            throw new InvalidOperationException("Audio endpoint not found: " + requestedName + ". Install VB-CABLE first.");
        }

        DeviceName = endpoint.FriendlyName;
        provider = new BufferedWaveProvider(new NAudio.Wave.WaveFormat(OutputSampleRate, OutputBitsPerSample, OutputChannels));
        provider.BufferDuration = TimeSpan.FromSeconds(30);
        provider.ReadFully = true;
        provider.DiscardOnBufferOverflow = true;
        output = new WasapiOut(endpoint, AudioClientShareMode.Shared, true, 20);
        try
        {
            output.PlaybackStopped += OnPlaybackStopped;
            output.Init(provider);
            output.Play();
        }
        catch
        {
            output.PlaybackStopped -= OnPlaybackStopped;
            output.Dispose();
            endpoint.Dispose();
            deviceEnumerator.Dispose();
            throw;
        }
    }

    public void WriteSilence(int milliseconds)
    {
        if (milliseconds <= 0) return;
        Write(new short[16000 * milliseconds / 1000]);
    }

    public void Write(short[] mono16k)
    {
        if (disposed || mono16k == null || mono16k.Length == 0) return;
        lock (stagingLock)
        {
            if (disposed) return;
            staging.AddRange(mono16k);
            while (staging.Count >= BlockSamples)
            {
                short[] block = staging.GetRange(0, BlockSamples).ToArray();
                staging.RemoveRange(0, BlockSamples);
                Enqueue(block);
            }
        }
    }

    public void ResetSessionMetrics()
    {
        lock (stagingLock) resampler.Reset();
        Interlocked.Exchange(ref maximumQueueDepth, PendingBlocks);
        Interlocked.Exchange(ref droppedBlocks, 0);
    }

    public void Flush()
    {
        if (disposed) return;
        lock (stagingLock)
        {
            if (staging.Count == 0) return;
            short[] block = staging.ToArray();
            staging.Clear();
            Enqueue(block);
        }
    }

    private void Enqueue(short[] samples)
    {
        byte[] output = resampler.Convert(samples);
        ThrowIfPlaybackFailed();
        int available = provider.BufferLength - provider.BufferedBytes;
        if (output.Length > available)
        {
            int dropped = (output.Length - Math.Max(0, available) + OutputBlockBytes - 1) / OutputBlockBytes;
            Interlocked.Add(ref droppedBlocks, dropped);
        }
        provider.AddSamples(output, 0, output.Length);
        int depth = PendingBlocks;
        int observed;
        while (depth > (observed = Volatile.Read(ref maximumQueueDepth)) &&
            Interlocked.CompareExchange(ref maximumQueueDepth, depth, observed) != observed) { }
    }

    public void Drain(int timeoutMs)
    {
        Drain(timeoutMs, null);
    }

    public bool Drain(int timeoutMs, Func<bool> shouldAbort)
    {
        if (shouldAbort != null && shouldAbort()) return false;
        Flush();
        int started = Environment.TickCount;
        while (PendingBlocks > 0 && unchecked(Environment.TickCount - started) < timeoutMs)
        {
            if (shouldAbort != null && shouldAbort()) return false;
            ThrowIfPlaybackFailed();
            Thread.Sleep(5);
        }
        if (PendingBlocks > 0)
        {
            throw new TimeoutException("VB-CABLE audio did not drain within " + timeoutMs + " ms; pending_blocks=" + PendingBlocks);
        }
        Thread.Sleep(DrainGuardMs);
        ThrowIfPlaybackFailed();
        return true;
    }

    public void DiscardPending()
    {
        lock (stagingLock)
        {
            staging.Clear();
            provider.ClearBuffer();
            resampler.Reset();
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        lock (stagingLock)
        {
            if (disposed) return;
            staging.Clear();
            disposed = true;
        }
        output.PlaybackStopped -= OnPlaybackStopped;
        output.Stop();
        output.Dispose();
        endpoint.Dispose();
        deviceEnumerator.Dispose();
    }

    private void OnPlaybackStopped(object sender, StoppedEventArgs args)
    {
        if (!disposed) playbackFailure = args.Exception ?? new InvalidOperationException("WASAPI output stopped unexpectedly");
    }

    private void ThrowIfPlaybackFailed()
    {
        Exception failure = playbackFailure;
        if (failure != null) throw new InvalidOperationException("WASAPI virtual microphone stopped: " + failure.Message, failure);
    }

    private static MMDevice FindDevice(MMDeviceEnumerator enumerator, string requested)
    {
        string expected = string.IsNullOrWhiteSpace(requested) ? "CABLE Input" : requested;
        foreach (MMDevice candidate in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            if ((candidate.FriendlyName ?? "").IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0) return candidate;
            candidate.Dispose();
        }
        return null;
    }
}
