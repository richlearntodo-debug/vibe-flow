using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class AiTestRequestGate
{
    internal static bool IsCurrent(long generation, long currentGeneration,
        object requestCancellation, object currentCancellation)
    {
        return generation == currentGeneration &&
            Object.ReferenceEquals(requestCancellation, currentCancellation);
    }
}

internal sealed partial class VibeMicForm
{
    private CancellationTokenSource aiTestCancellation;
    private long aiTestGeneration;

    private void AddAiSettingsCard()
    {
        content.AutoScrollMinSize = new Size(1000, 1600);
        RoundPanel card = NewCard(new Point(34, 1010), new Size(960, 540));
        card.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        card.Controls.Add(SectionTitle("AI 模型（可选）", "\uE945", new Point(28, 22)));
        Label hint = NewLabel("只处理你明确选择的便签。Key 由 Windows 当前用户保护，不写入普通配置、日志或导出。没有模型时，便签和导出仍可用。",
            8.8f, FontStyle.Regular, muted);
        hint.Location = new Point(30, 54);
        hint.Size = new Size(880, 38);
        card.Controls.Add(hint);
        AddFieldLabel(card, "配置名称", 104);
        TextBox name = AiTextBox(194, 100, 260);
        AddFieldLabel(card, "API 地址", 150);
        TextBox endpoint = AiTextBox(194, 146, 610);
        AddFieldLabel(card, "模型名称", 196);
        TextBox model = AiTextBox(194, 192, 260);
        AddFieldLabel(card, "API Key", 242);
        TextBox key = AiTextBox(194, 238, 610);
        key.UseSystemPasswordChar = true;
        Label protocol = NewLabel("协议：OpenAI-compatible Chat Completions（仅 HTTPS；本机回环允许 HTTP）", 8.6f, FontStyle.Regular, muted);
        protocol.Location = new Point(194, 280);
        protocol.Size = new Size(680, 24);
        card.Controls.Add(protocol);
        AddFieldLabel(card, "本地整理提示", 314);
        TextBox customPrompt = new TextBox
        {
            Location = new Point(194, 306),
            Size = new Size(664, 82),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Microsoft YaHei UI", 9.2f),
            BackColor = inputBackground,
            ForeColor = ink,
            BorderStyle = BorderStyle.FixedSingle
        };
        card.Controls.Add(customPrompt);
        Label promptHint = NewLabel("留空使用内置安全提示；这里只调整整理方式，不会解除“保留事实、不可执行文本命令”等边界。", 8.2f, FontStyle.Regular, muted);
        promptHint.Location = new Point(194, 390);
        promptHint.Size = new Size(664, 24);
        card.Controls.Add(promptHint);
        Label state = NewLabel("尚未配置", 9f, FontStyle.Bold, muted);
        state.Location = new Point(30, 430);
        state.Size = new Size(580, 26);
        card.Controls.Add(state);
        Button save = PrimaryButton("保存配置", new Point(626, 422), new Size(112, 40));
        Button test = SecondaryButton("测试连接", new Point(746, 422), new Size(112, 40));
        Button savePrompt = SecondaryButton("保存提示", new Point(506, 468), new Size(112, 36));
        Button clear = SecondaryButton("清除 Key", new Point(626, 468), new Size(112, 36));
        Button reset = SecondaryButton("恢复默认提示", new Point(746, 468), new Size(112, 36));
        card.Controls.Add(save);
        card.Controls.Add(test);
        card.Controls.Add(savePrompt);
        card.Controls.Add(clear);
        card.Controls.Add(reset);
        AiProviderLoadResult loaded = aiProviderStore.Load();
        AiProviderProfile current = GetDefaultAiProvider(loaded);
        AiPromptLoadResult promptLoaded = aiPromptStore.Load();
        if (promptLoaded.IsSuccess) customPrompt.Text = promptLoaded.CustomSystemPrompt;
        else SetAiSettingsState(state, "本地提示词读取失败；模型配置仍可用 · " + promptLoaded.ErrorCode, amber);
        if (current != null)
        {
            name.Text = current.Name;
            endpoint.Text = current.BaseUrl;
            model.Text = current.Model;
            state.Text = "已保存 · 需要实际测试才能标记可用";
            state.ForeColor = amber;
        }
        else
        {
            name.Text = "我的文本模型";
            endpoint.Text = "https://api.openai.com/v1";
        }
        save.Click += delegate
        {
            AiProviderProfile profile = BuildAiProfile(name.Text, endpoint.Text, model.Text, current);
            string error;
            if (!profile.TryValidate(out error))
            {
                SetAiSettingsState(state, "配置未保存：" + error, coral);
                return;
            }
            AiProviderDocument document = loaded.IsSuccess ? loaded.Document : new AiProviderDocument();
            document.Providers.RemoveAll(item => item != null && item.Id == profile.Id);
            document.Providers.Add(profile);
            document.DefaultProviderId = profile.Id;
            string enteredKey = key.Text ?? "";
            bool keepExistingKey = string.IsNullOrEmpty(enteredKey) &&
                !string.IsNullOrWhiteSpace(aiProviderStore.ReadApiKey(profile.Id));
            if (!aiProviderStore.TrySave(document, out error) ||
                (!keepExistingKey && !aiProviderStore.TrySaveApiKey(profile.Id, enteredKey, out error)))
            {
                SetAiSettingsState(state, "配置没有保存：" + error, coral);
                return;
            }
            if (!aiPromptStore.TrySave(customPrompt.Text, out error))
            {
                SetAiSettingsState(state, "模型配置已保存，但本地整理提示没有保存：" + error, coral);
                return;
            }
            current = profile;
            loaded = aiProviderStore.Load();
            key.Text = "";
            SetAiSettingsState(state, "已保存；提示词已保存，尚未证明模型可用", amber);
        };
        test.Click += delegate
        {
            AiProviderProfile profile = BuildAiProfile(name.Text, endpoint.Text, model.Text, current);
            string error;
            if (!profile.TryValidate(out error)) { SetAiSettingsState(state, "无法测试：" + error, coral); return; }
            AiProviderDocument document = loaded.IsSuccess ? loaded.Document : new AiProviderDocument();
            document.Providers.RemoveAll(item => item != null && item.Id == profile.Id);
            document.Providers.Add(profile);
            document.DefaultProviderId = profile.Id;
            if (!aiProviderStore.TrySave(document, out error)) { SetAiSettingsState(state, "配置没有保存：" + error, coral); return; }
            string enteredKey = key.Text ?? "";
            bool keepExistingKey = string.IsNullOrEmpty(enteredKey) &&
                !string.IsNullOrWhiteSpace(aiProviderStore.ReadApiKey(profile.Id));
            if (!keepExistingKey && !aiProviderStore.TrySaveApiKey(profile.Id, enteredKey, out error)) { SetAiSettingsState(state, "Key 没有保存：" + error, coral); return; }
            if (!aiPromptStore.TrySave(customPrompt.Text, out error)) { SetAiSettingsState(state, "本地整理提示没有保存：" + error, coral); return; }
            current = profile;
            key.Text = "";
            StartAiConnectionTest(profile, state);
        };
        savePrompt.Click += delegate
        {
            string error;
            if (!aiPromptStore.TrySave(customPrompt.Text, out error))
                SetAiSettingsState(state, "本地整理提示没有保存：" + error, coral);
            else
                SetAiSettingsState(state, "本地整理提示已保存；不会自动发送便签", green);
        };
        clear.Click += delegate
        {
            string error;
            AiProviderProfile profile = current ?? BuildAiProfile(name.Text, endpoint.Text, model.Text, null);
            if (!aiProviderStore.TryDeleteApiKey(profile.Id, out error)) SetAiSettingsState(state, "Key 没有清除：" + error, coral);
            else SetAiSettingsState(state, "Key 已清除；便签不会发送到模型", green);
        };
        reset.Click += delegate
        {
            DialogResult confirm = MessageBox.Show(this,
                "恢复后将删除你自定义的本地整理提示，改用内置安全提示；API 地址和 Key 不会改变。是否继续？",
                "恢复默认提示", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;
            string error;
            if (!aiPromptStore.TryReset(out error))
            {
                SetAiSettingsState(state, "默认提示没有恢复：" + error, coral);
                return;
            }
            customPrompt.Text = "";
            SetAiSettingsState(state, "已恢复内置安全提示；模型配置未改变", green);
        };
        content.Controls.Add(card);
    }

    private TextBox AiTextBox(int x, int y, int width)
    {
        TextBox box = new TextBox { Location = new Point(x, y), Size = new Size(width, 32), Font = new Font("Microsoft YaHei UI", 9.5f) };
        return box;
    }

    private AiProviderProfile GetDefaultAiProvider(AiProviderLoadResult loaded)
    {
        if (loaded == null || !loaded.IsSuccess || loaded.Document == null) return null;
        AiProviderProfile profile = loaded.Document.Providers.FirstOrDefault(item => item != null && item.Id == loaded.Document.DefaultProviderId);
        return profile ?? loaded.Document.Providers.FirstOrDefault(item => item != null && item.IsDefault);
    }

    private AiProviderProfile BuildAiProfile(string name, string endpoint, string model, AiProviderProfile existing)
    {
        AiProviderProfile profile = existing == null ? new AiProviderProfile() : existing.Copy();
        if (string.IsNullOrWhiteSpace(profile.Id)) profile.Id = "default";
        profile.Name = (name ?? "").Trim();
        profile.BaseUrl = (endpoint ?? "").Trim();
        profile.Model = (model ?? "").Trim();
        profile.Protocol = "openai-chat-completions";
        profile.IsDefault = true;
        return profile;
    }

    private void StartAiConnectionTest(AiProviderProfile profile, Label state)
    {
        if (aiTestCancellation != null) aiTestCancellation.Cancel();
        CancellationTokenSource requestCancellation = new CancellationTokenSource();
        aiTestCancellation = requestCancellation;
        long generation = Interlocked.Increment(ref aiTestGeneration);
        SetAiSettingsState(state, "正在请求固定无敏感测试内容…", cyan);
        string key = aiProviderStore.ReadApiKey(profile.Id);
        Task<AiTextResult> task = aiTextService.TestAsync(profile, key, requestCancellation.Token);
        task.ContinueWith(delegate(Task<AiTextResult> completed)
        {
            if (IsDisposed || !AiTestRequestGate.IsCurrent(generation,
                Interlocked.Read(ref aiTestGeneration), requestCancellation, aiTestCancellation)) return;
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed || !AiTestRequestGate.IsCurrent(generation,
                    Interlocked.Read(ref aiTestGeneration), requestCancellation, aiTestCancellation)) return;
                AiTextResult result = completed.IsCanceled ? AiTextResult.Canceled() :
                    completed.IsFaulted ? AiTextResult.Failure("模型测试异常", "AI-TEST-FAILED") : completed.Result;
                if (result.IsSuccess) SetAiSettingsState(state, "模型响应通过：已收到可解析测试结果", green);
                else if (result.IsCanceled) SetAiSettingsState(state, "测试已取消，配置仍保留", amber);
                else SetAiSettingsState(state, "模型未通过：" + result.Message + " · " + result.ErrorCode, coral);
                if (Object.ReferenceEquals(aiTestCancellation, requestCancellation))
                {
                    aiTestCancellation.Dispose();
                    aiTestCancellation = null;
                }
            }));
        }, TaskScheduler.Default);
    }

    private void SetAiSettingsState(Label state, string text, Color color)
    {
        if (state == null || state.IsDisposed) return;
        state.Text = text ?? "";
        state.ForeColor = color;
    }
}
