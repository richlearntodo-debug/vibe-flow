// 云端 ASR（实验）语音槽：在 Host 进程内从 CABLE Output 取音、松开录音键后上传识别，
// 再把文字用 Unicode 注入写进当前焦点输入框。
//
// 设计要点：
// - 默认关闭：cloud-asr.json 的 enabled / consentedAt 都为假时，这里没有任何网络调用，
//   也没有任何常驻音频资源。只有用户显式选择「云端 ASR（实验）」并确认隐私提示后才会启用。
// - 没按住录音键就没有录音、也没有网络请求：录音器在按键按下时创建，松开后立刻停止并释放。
// - 不落盘：音频只存在内存里的 WAV 字节，上传完即丢弃；不写临时文件、不写识别文字。
// - 不改冻结的 Capture：音频取自 CABLE Output（冻结 Capture 在会话期间把它设为默认录音设备）。

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using NAudio.CoreAudioApi;
using NAudio.Wave;

internal sealed class CloudAsrSettings
{
    public const string DefaultEndpoint = "https://api.siliconflow.cn/v1/audio/transcriptions";
    public const string DefaultModel = "FunAudioLLM/SenseVoiceSmall";
    public const int DefaultTimeoutSeconds = 30;

    public bool enabled { get; set; }
    public string endpoint { get; set; }
    public string model { get; set; }
    public string keyPath { get; set; }
    public int timeoutSeconds { get; set; }
    // 0 表示用户还没有看到并同意「录音会上传」的说明；非 0 是同意发生的时间（Unix 秒）。
    public long consentedAt { get; set; }

    public static CloudAsrSettings Default()
    {
        var value = new CloudAsrSettings();
        value.enabled = false;
        value.endpoint = DefaultEndpoint;
        value.model = DefaultModel;
        value.keyPath = "";
        value.timeoutSeconds = DefaultTimeoutSeconds;
        value.consentedAt = 0;
        return value;
    }

    public static string FilePath(string userStateRoot)
    {
        return Path.Combine(userStateRoot, "cloud-asr.json");
    }

    public static CloudAsrSettings Load(string userStateRoot, Action<string> log)
    {
        CloudAsrSettings fallback = Default();
        if (string.IsNullOrWhiteSpace(userStateRoot)) return fallback;
        string path = FilePath(userStateRoot);
        if (!File.Exists(path)) return fallback;
        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return fallback;
            var serializer = new JavaScriptSerializer();
            CloudAsrSettings parsed = serializer.Deserialize<CloudAsrSettings>(json);
            if (parsed == null) return fallback;
            return parsed.Normalized();
        }
        catch (Exception ex)
        {
            if (log != null) log("CLOUD ASR settings_load_failed error=" + SafeName(ex.GetType().Name));
            return fallback;
        }
    }

    public CloudAsrSettings Normalized()
    {
        CloudAsrSettings value = Default();
        value.enabled = enabled;
        value.consentedAt = consentedAt;
        value.endpoint = string.IsNullOrWhiteSpace(endpoint) ? DefaultEndpoint : endpoint.Trim();
        value.model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model.Trim();
        value.keyPath = (keyPath ?? "").Trim();
        value.timeoutSeconds = timeoutSeconds <= 0 || timeoutSeconds > 120 ? DefaultTimeoutSeconds : timeoutSeconds;
        return value;
    }

    public bool Save(string userStateRoot, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(userStateRoot)) return false;
        string path = FilePath(userStateRoot);
        try
        {
            Directory.CreateDirectory(userStateRoot);
            string json = new JavaScriptSerializer().Serialize(Normalized());
            string temp = path + ".tmp";
            File.WriteAllText(temp, json, Encoding.UTF8);
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
            return true;
        }
        catch (Exception ex)
        {
            if (log != null) log("CLOUD ASR settings_save_failed error=" + SafeName(ex.GetType().Name));
            return false;
        }
    }

    // API Key 只从本机文件读，绝不写进仓库、配置或日志。
    // 顺序：环境变量 → 配置里的 keyPath → %USERPROFILE%\.codex\secrets\siliconflow_api_key.txt
    public string ResolveKeyPath()
    {
        string fromEnvironment = Environment.GetEnvironmentVariable("VIBE_CLOUD_ASR_KEY_FILE");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return Environment.ExpandEnvironmentVariables(fromEnvironment.Trim());
        if (!string.IsNullOrWhiteSpace(keyPath)) return Environment.ExpandEnvironmentVariables(keyPath);
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(profile)) return "";
        return Path.Combine(profile, ".codex", "secrets", "siliconflow_api_key.txt");
    }

    public bool TryReadApiKey(out string apiKey)
    {
        apiKey = "";
        string path = ResolveKeyPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        try
        {
            string text = File.ReadAllText(path, Encoding.UTF8).Trim();
            if (text.Length == 0) return false;
            // 只取第一行，防止用户把说明文字一起粘贴进来。
            int newline = text.IndexOfAny(new char[] { '\r', '\n' });
            if (newline >= 0) text = text.Substring(0, newline).Trim();
            if (text.Length < 8) return false;
            apiKey = text;
            return true;
        }
        catch { return false; }
    }

    public bool IsUsable()
    {
        string apiKey;
        return enabled && consentedAt > 0 && TryReadApiKey(out apiKey);
    }

    internal static string SafeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value;
    }
}

// 纯策略：不碰网络、不碰设备，方便自检固定。
internal static class CloudAsrPolicy
{
    public const string AuthorisationNotice =
        "云端 ASR 是实验功能，默认关闭。\n\n" +
        "开启后，按住录音键期间的音频会在你松开按键后上传到所填写的云端服务商做识别；" +
        "音频因此离开这台电脑，服务商的隐私政策适用于这些音频。\n\n" +
        "Vibe Link 仍然不保存录音文件、也不保存识别出的文字；松开按键前不会产生任何网络请求。\n\n" +
        "是否启用云端 ASR？";

    public const int MinimumAudioMs = 300;
    public const int MaximumAudioMs = 60000;
    public const int MaximumUploadBytes = 50 * 1024 * 1024;
    public const int CaptureTailMs = 240;
    // 冷启动那一次 Capture 进程还没起来，录音键的保持信号会晚到；先等它出现一次。
    public const int HoldSignalGraceMs = 3000;

    public static bool ShouldUpload(bool enabled, bool consented, bool keyPresent,
        int audioBytes, int audioMs)
    {
        if (!enabled || !consented || !keyPresent) return false;
        if (audioBytes <= 0 || audioMs < MinimumAudioMs) return false;
        if (audioBytes > MaximumUploadBytes) return false;
        return true;
    }

    // 失败分类只用于给用户一句能行动的话，不带服务商原始报文。
    public static string ClassifyHttpFailure(int statusCode)
    {
        if (statusCode == 401 || statusCode == 403) return "AUTH";
        if (statusCode == 429) return "RATE_LIMIT";
        if (statusCode == 503 || statusCode == 504 || statusCode == 502) return "OVERLOADED";
        if (statusCode >= 500) return "SERVER";
        if (statusCode >= 400) return "REQUEST";
        return "UNKNOWN";
    }

    public static string FailureMessage(string code)
    {
        switch (code)
        {
            case "NOT_CONFIGURED": return "云端 ASR 还没配置好：需要先开启并填入 API Key";
            case "NO_KEY": return "没读到 API Key：请把 Key 写进 .codex\\secrets\\siliconflow_api_key.txt";
            case "AUDIO_DEVICE": return "没找到 CABLE Output，云端 ASR 无法取到遥控器音频";
            case "TOO_SHORT": return "这次没录到足够的声音，云端 ASR 没有上传";
            case "TOO_LONG": return "这段录音超过 60 秒，云端 ASR 没有上传";
            case "OFFLINE": return "网络不可用，云端 ASR 这次没有出字（不会自动重试）";
            case "AUTH": return "API Key 被服务商拒绝（401/403），请检查 Key 是否有效";
            case "RATE_LIMIT": return "服务商限流（429），稍后再按一次录音键";
            case "OVERLOADED": return "服务商暂时过载（503/504），稍后再试";
            case "SERVER": return "服务商返回错误，这次没有出字";
            case "REQUEST": return "服务商拒绝了这次请求，请检查模型名与账号状态";
            case "EMPTY": return "服务商返回了空结果，这次没有出字";
            case "FOCUS": return "识别已返回，但焦点输入框已改变，文字没有输入";
            case "INJECT": return "识别已返回，但文字没有成功输入到这个窗口";
            default: return "云端 ASR 这次没有出字";
        }
    }
}

internal sealed class CloudAsrResult
{
    public bool Ok;
    public string Text;
    public string FailureCode;
    public string Detail;
    public int AudioBytes;
    public int AudioMs;
    public long ReleaseToTextMs = -1;

    public static CloudAsrResult Failure(string code, string detail)
    {
        var result = new CloudAsrResult();
        result.Ok = false;
        result.FailureCode = code;
        result.Detail = detail;
        return result;
    }
}

// 只读 CABLE Output 的 WASAPI 共享模式录音器，音频留在内存里。
internal sealed class CloudAsrAudioRecorder : IDisposable
{
    private readonly object dataLock = new object();
    private readonly MemoryStream captured = new MemoryStream();
    private readonly ManualResetEventSlim stopped = new ManualResetEventSlim(false);
    private readonly MMDeviceEnumerator enumerator;
    private readonly MMDevice endpoint;
    private readonly WasapiCapture capture;
    private readonly WaveFormat format;
    private readonly int maximumBytes;
    private int stopRequested;
    private bool disposed;

    public CloudAsrAudioRecorder(string requestedName)
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
            maximumBytes = Math.Max(format.BlockAlign,
                format.AverageBytesPerSecond * (CloudAsrPolicy.MaximumAudioMs / 1000));
            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;
            capture.StartRecording();
        }
        catch
        {
            if (capture != null) capture.Dispose();
            endpoint.Dispose();
            enumerator.Dispose();
            stopped.Dispose();
            captured.Dispose();
            throw;
        }
    }

    public string EndpointName { get { return endpoint == null ? "" : endpoint.FriendlyName; } }

    public string FormatName { get { return CloudAsrWave.Describe(format); } }

    private void OnDataAvailable(object sender, WaveInEventArgs args)
    {
        if (Volatile.Read(ref stopRequested) != 0 || args == null || args.BytesRecorded <= 0) return;
        lock (dataLock)
        {
            int remaining = maximumBytes - (int)captured.Length;
            int count = Math.Min(remaining, args.BytesRecorded);
            int align = Math.Max(1, format.BlockAlign);
            count -= count % align;
            if (count > 0) captured.Write(args.Buffer, 0, count);
        }
    }

    private void OnRecordingStopped(object sender, StoppedEventArgs args)
    {
        stopped.Set();
    }

    public void RequestStop()
    {
        if (Interlocked.Exchange(ref stopRequested, 1) != 0) return;
        try { capture.StopRecording(); }
        catch { stopped.Set(); }
    }

    // 返回 16 kHz 单声道 16 bit 的 WAV 字节；没有可用音频时返回 null。
    public byte[] FinishAsWav(out int audioMs)
    {
        audioMs = 0;
        RequestStop();
        stopped.Wait(1500);
        byte[] raw;
        lock (dataLock) raw = captured.ToArray();
        return CloudAsrWave.To16kMonoWav(format, raw, out audioMs);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        RequestStop();
        try { capture.DataAvailable -= OnDataAvailable; } catch { }
        try { capture.RecordingStopped -= OnRecordingStopped; } catch { }
        try { capture.Dispose(); } catch { }
        try { endpoint.Dispose(); } catch { }
        try { enumerator.Dispose(); } catch { }
        try { stopped.Dispose(); } catch { }
        try { captured.Dispose(); } catch { }
    }

    private static MMDevice FindCaptureDevice(MMDeviceEnumerator deviceEnumerator, string requestedName)
    {
        string expected = string.IsNullOrWhiteSpace(requestedName) ? "CABLE Output" : requestedName;
        foreach (MMDevice candidate in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if ((candidate.FriendlyName ?? "").IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
            candidate.Dispose();
        }
        return null;
    }
}

internal static class CloudAsrWave
{
    private const int TargetRate = 16000;

    // 把设备原生格式（多为 48 kHz 立体声 float）压成 ASR 要的 16 kHz 单声道 16 bit WAV。
    public static byte[] To16kMonoWav(WaveFormat source, byte[] data, out int audioMs)
    {
        audioMs = 0;
        if (source == null || data == null || data.Length <= 0) return null;
        int channels = Math.Max(1, source.Channels);
        int bytesPerSample = Math.Max(1, source.BitsPerSample / 8);
        int blockAlign = source.BlockAlign > 0 ? source.BlockAlign : channels * bytesPerSample;
        if (blockAlign <= 0) return null;
        int frames = data.Length / blockAlign;
        if (frames <= 0) return null;
        int sampleRate = Math.Max(1, source.SampleRate);
        audioMs = (int)Math.Round(frames * 1000.0 / sampleRate);

        var mono = new float[frames];
        for (int frame = 0; frame < frames; frame++)
        {
            double sum = 0;
            int offset = frame * blockAlign;
            for (int channel = 0; channel < channels; channel++)
                sum += ReadSample(source, data, offset + channel * bytesPerSample);
            mono[frame] = (float)(sum / channels);
        }

        // 降采样前做一次滑动平均，压掉混叠。
        double ratio = (double)sampleRate / TargetRate;
        if (ratio >= 1.5)
        {
            int window = (int)Math.Round(ratio);
            if (window > 1 && frames > window)
            {
                var filtered = new float[frames];
                double running = 0;
                for (int index = 0; index < frames; index++)
                {
                    running += mono[index];
                    if (index >= window) running -= mono[index - window];
                    filtered[index] = (float)(running / Math.Min(index + 1, window));
                }
                mono = filtered;
            }
        }

        int targetFrames = (int)Math.Floor(frames / ratio);
        if (targetFrames <= 0) return null;
        var pcm = new short[targetFrames];
        for (int index = 0; index < targetFrames; index++)
        {
            double position = index * ratio;
            int baseIndex = (int)position;
            double fraction = position - baseIndex;
            float first = mono[Math.Min(baseIndex, frames - 1)];
            float second = mono[Math.Min(baseIndex + 1, frames - 1)];
            pcm[index] = ToInt16(first + (second - first) * fraction);
        }
        return WriteWav(pcm, TargetRate);
    }

    private static double ReadSample(WaveFormat format, byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length) return 0;
        switch (format.Encoding)
        {
            case WaveFormatEncoding.IeeeFloat:
                if (format.BitsPerSample == 64 && offset + 8 <= data.Length)
                    return BitConverter.ToDouble(data, offset);
                if (offset + 4 <= data.Length) return BitConverter.ToSingle(data, offset);
                return 0;
            case WaveFormatEncoding.Pcm:
                if (format.BitsPerSample == 8) return (data[offset] - 128) / 128.0;
                if (format.BitsPerSample == 24 && offset + 3 <= data.Length)
                    return ((data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16)) << 8) / 2147483648.0;
                if (format.BitsPerSample == 32 && offset + 4 <= data.Length)
                    return BitConverter.ToInt32(data, offset) / 2147483648.0;
                if (offset + 2 <= data.Length) return BitConverter.ToInt16(data, offset) / 32768.0;
                return 0;
            default:
                if (offset + 2 <= data.Length) return BitConverter.ToInt16(data, offset) / 32768.0;
                return 0;
        }
    }

    private static short ToInt16(double value)
    {
        double clamped = Math.Max(-1.0, Math.Min(1.0, value));
        return (short)Math.Round(clamped * 32767.0);
    }

    private static byte[] WriteWav(short[] samples, int sampleRate)
    {
        int dataBytes = samples.Length * 2;
        var stream = new MemoryStream(44 + dataBytes);
        var writer = new BinaryWriter(stream, Encoding.ASCII);
        writer.Write(new char[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + dataBytes);
        writer.Write(new char[] { 'W', 'A', 'V', 'E' });
        writer.Write(new char[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(new char[] { 'd', 'a', 't', 'a' });
        writer.Write(dataBytes);
        for (int index = 0; index < samples.Length; index++) writer.Write(samples[index]);
        writer.Flush();
        return stream.ToArray();
    }

    // 只用于日志：设备格式压成一行，不含音频数据。
    public static string Describe(WaveFormat format)
    {
        if (format == null) return "unknown";
        return format.SampleRate + "Hz/" + format.Channels + "ch/" + format.BitsPerSample + "bit/" +
            format.Encoding;
    }
}

internal static class CloudAsrClient
{
    public static CloudAsrResult Transcribe(CloudAsrSettings settings, string apiKey, byte[] wav,
        Action<string> log)
    {
        if (settings == null || wav == null || wav.Length <= 0)
            return CloudAsrResult.Failure("REQUEST", "empty_request");
        string boundary = "----VibeLinkCloudAsr" + Guid.NewGuid().ToString("N");
        byte[] body = BuildMultipart(boundary, settings.model, wav);
        HttpWebRequest request = null;
        try
        {
            // .NET Framework 默认可能用 TLS 1.0；只在真正上传时把进程内的默认协议抬到 TLS 1.2。
            try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; } catch { }
            request = (HttpWebRequest)WebRequest.Create(settings.endpoint);
            request.Method = "POST";
            request.ContentType = "multipart/form-data; boundary=" + boundary;
            request.Accept = "application/json";
            request.Headers["Authorization"] = "Bearer " + apiKey;
            request.Timeout = settings.timeoutSeconds * 1000;
            request.ReadWriteTimeout = settings.timeoutSeconds * 1000;
            request.AllowAutoRedirect = false;
            request.ContentLength = body.Length;
            request.Proxy = null;
            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }
            using (var response = (HttpWebResponse)request.GetResponse())
            {
                string text = ReadBody(response);
                return ParseResponse(text);
            }
        }
        catch (WebException ex)
        {
            var http = ex.Response as HttpWebResponse;
            if (http != null)
            {
                int status = (int)http.StatusCode;
                try { http.Dispose(); } catch { }
                string code = CloudAsrPolicy.ClassifyHttpFailure(status);
                if (log != null) log("CLOUD ASR upload_failed code=" + code + " status=" + status);
                return CloudAsrResult.Failure(code, "http_" + status);
            }
            if (log != null)
                log("CLOUD ASR upload_failed code=OFFLINE error=" +
                    CloudAsrSettings.SafeName(ex.Status.ToString()));
            return CloudAsrResult.Failure("OFFLINE", ex.Status.ToString());
        }
        catch (Exception ex)
        {
            if (log != null)
                log("CLOUD ASR upload_failed code=OFFLINE error=" +
                    CloudAsrSettings.SafeName(ex.GetType().Name));
            return CloudAsrResult.Failure("OFFLINE", ex.GetType().Name);
        }
        finally
        {
            try { if (request != null) request.Abort(); } catch { }
        }
    }

    private static byte[] BuildMultipart(string boundary, string model, byte[] wav)
    {
        var stream = new MemoryStream();
        Action<string> write = delegate(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        };
        write("--" + boundary + "\r\n");
        write("Content-Disposition: form-data; name=\"model\"\r\n\r\n");
        write(model + "\r\n");
        write("--" + boundary + "\r\n");
        write("Content-Disposition: form-data; name=\"file\"; filename=\"dictation.wav\"\r\n");
        write("Content-Type: audio/wav\r\n\r\n");
        stream.Write(wav, 0, wav.Length);
        write("\r\n--" + boundary + "--\r\n");
        return stream.ToArray();
    }

    private static string ReadBody(HttpWebResponse response)
    {
        using (Stream stream = response.GetResponseStream())
        {
            if (stream == null) return "";
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }
    }

    private static CloudAsrResult ParseResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return CloudAsrResult.Failure("EMPTY", "empty_body");
        try
        {
            var serializer = new JavaScriptSerializer();
            var document = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (document == null) return CloudAsrResult.Failure("EMPTY", "unexpected_body");
            object value;
            string text = document.TryGetValue("text", out value) ? (value as string ?? "") : "";
            text = text.Replace("\r", "").Trim();
            if (text.Length == 0) return CloudAsrResult.Failure("EMPTY", "empty_text");
            var result = new CloudAsrResult();
            result.Ok = true;
            result.Text = text;
            return result;
        }
        catch
        {
            return CloudAsrResult.Failure("EMPTY", "unparsable_body");
        }
    }
}

// 把文字按 Unicode 事件打进焦点输入框：不碰剪贴板、不自动回车。
internal static class CloudAsrTextInjection
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, INPUT[] inputs, int size);

    public static bool TypeText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        bool allSent = true;
        for (int index = 0; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\r') continue;
            if (!SendCharacter(character)) allSent = false;
        }
        return allSent;
    }

    private static bool SendCharacter(char character)
    {
        var inputs = new INPUT[2];
        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].u.ki.wVk = 0;
        inputs[0].u.ki.wScan = character;
        inputs[0].u.ki.dwFlags = KEYEVENTF_UNICODE;
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].u.ki.wVk = 0;
        inputs[1].u.ki.wScan = character;
        inputs[1].u.ki.dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP;
        return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
    }
}

// 一次按住说话 = 一个会话：开始采集 → 等录音键松开 → 停采 → 上传 → 把结果交回 Host。
// 会话本身不碰 UI、不判断焦点；文字落到哪里由 Host 决定。
internal sealed class CloudAsrSession
{
    private readonly string userStateRoot;
    private readonly string endpointName;
    private readonly Func<bool> voiceKeyHeld;
    private readonly Action<string> log;
    private readonly Action<CloudAsrSession> onFinished;
    private readonly Thread worker;
    private readonly System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
    private CloudAsrAudioRecorder recorder;
    private volatile bool cancelRequested;
    private long startedAtMs;
    private long releasedAtMs;

    public CloudAsrSession(string userStateRoot, string endpointName, Func<bool> voiceKeyHeldProbe,
        Action<string> logger, Action<CloudAsrSession> finishedCallback)
    {
        this.userStateRoot = userStateRoot;
        this.endpointName = string.IsNullOrWhiteSpace(endpointName) ? "CABLE Output" : endpointName;
        voiceKeyHeld = voiceKeyHeldProbe;
        log = logger;
        onFinished = finishedCallback;
        worker = new Thread(Run);
        worker.IsBackground = true;
        worker.Name = "Vibe Link cloud ASR session";
    }

    // 会话结束后由 Host 读取，必须等 onFinished 回调之后再读。
    public CloudAsrResult Completed { get; private set; }

    public void Start()
    {
        worker.Start();
    }

    public void Cancel()
    {
        cancelRequested = true;
        CloudAsrAudioRecorder active = recorder;
        if (active != null) { try { active.RequestStop(); } catch { } }
    }

    private void Run()
    {
        CloudAsrResult result;
        try
        {
            result = RunSession();
        }
        catch (Exception ex)
        {
            Log("CLOUD ASR session_error error=" + CloudAsrSettings.SafeName(ex.GetType().Name));
            result = CloudAsrResult.Failure("AUDIO_DEVICE", ex.GetType().Name);
        }
        finally
        {
            CloudAsrAudioRecorder active = recorder;
            recorder = null;
            if (active != null) { try { active.Dispose(); } catch { } }
        }
        if (result == null) result = CloudAsrResult.Failure("EMPTY", "no_result");
        Completed = result;
        try { if (onFinished != null) onFinished(this); }
        catch { }
    }

    private CloudAsrResult RunSession()
    {
        startedAtMs = clock.ElapsedMilliseconds;
        CloudAsrSettings settings = CloudAsrSettings.Load(userStateRoot, log);
        string apiKey;
        bool keyPresent = settings.TryReadApiKey(out apiKey);
        if (!settings.enabled || settings.consentedAt <= 0)
        {
            Log("CLOUD ASR skipped reason=not_enabled");
            return CloudAsrResult.Failure("NOT_CONFIGURED", "disabled");
        }
        if (!keyPresent)
        {
            Log("CLOUD ASR skipped reason=no_key");
            return CloudAsrResult.Failure("NO_KEY", "missing_key_file");
        }

        try
        {
            recorder = new CloudAsrAudioRecorder(endpointName);
        }
        catch (Exception ex)
        {
            Log("CLOUD ASR capture_open_failed error=" + CloudAsrSettings.SafeName(ex.GetType().Name));
            return CloudAsrResult.Failure("AUDIO_DEVICE", ex.GetType().Name);
        }
        Log("CLOUD ASR capture_started endpoint=" + CloudAsrSettings.SafeName(recorder.EndpointName) +
            " format=" + CloudAsrSettings.SafeName(recorder.FormatName));

        // 等松开录音键。IsVoiceKeyHeld 由冻结 Capture 维护，是这条链路的权威信号。
        bool sawHeld = false;
        while (!cancelRequested)
        {
            long elapsed = clock.ElapsedMilliseconds - startedAtMs;
            bool held = voiceKeyHeld != null && voiceKeyHeld();
            if (held) sawHeld = true;
            else if (sawHeld) break;
            else if (elapsed > CloudAsrPolicy.HoldSignalGraceMs) break;
            if (elapsed > CloudAsrPolicy.MaximumAudioMs + 5000) break;
            Thread.Sleep(20);
        }
        releasedAtMs = clock.ElapsedMilliseconds;
        if (!cancelRequested) Thread.Sleep(CloudAsrPolicy.CaptureTailMs);

        int audioMs;
        byte[] wav = recorder.FinishAsWav(out audioMs);
        int audioBytes = wav == null ? 0 : wav.Length;
        Log("CLOUD ASR capture_stopped bytes=" + audioBytes + " audio_ms=" + audioMs +
            " released_ms=" + releasedAtMs);

        if (!CloudAsrPolicy.ShouldUpload(settings.enabled, settings.consentedAt > 0, keyPresent,
            audioBytes, audioMs))
        {
            string code = audioMs > CloudAsrPolicy.MaximumAudioMs ? "TOO_LONG" : "TOO_SHORT";
            return CloudAsrResult.Failure(code, "audio_ms=" + audioMs);
        }

        Log("CLOUD ASR upload_begin bytes=" + audioBytes + " model=" +
            CloudAsrSettings.SafeName(settings.model) + " upload_ms=" + clock.ElapsedMilliseconds);
        CloudAsrResult response = CloudAsrClient.Transcribe(settings, apiKey, wav, log);
        long textAtMs = clock.ElapsedMilliseconds;
        response.AudioBytes = audioBytes;
        response.AudioMs = audioMs;
        response.ReleaseToTextMs = textAtMs - releasedAtMs;
        Log("CLOUD ASR upload_end ok=" + response.Ok + " release_to_text_ms=" + response.ReleaseToTextMs +
            " characters=" + (response.Text == null ? 0 : response.Text.Length));
        return response;
    }

    private void Log(string line)
    {
        if (log != null) log(line);
    }
}
