using System;

internal sealed partial class VibeMicForm
{
    internal static bool TryCloseBeforeTeardown(Func<bool> closeDeck, Action teardown)
    {
        if (closeDeck == null || !closeDeck()) return false;
        if (teardown != null) teardown();
        return true;
    }

    private bool IsNotesRecordingActive()
    {
        try { return IsVoiceKeyHeld() || string.Equals(currentVisualState, "recording", StringComparison.OrdinalIgnoreCase); }
        catch { return true; }
    }

    private bool GuardNotesOperation(string actionName)
    {
        if (!IsNotesRecordingActive()) return false;
        ShowActionToast(ActionResult.Create(actionName, "本机便签", ActionState.Canceled,
            "录音正在进行，本次操作未执行", "录音操作优先，便签不会抢焦点",
            "录音结束后重试", "NOTES-CANCELED-VOICE"));
        return true;
    }

    internal static ActionResult CreateNotesDeckOpenFailureResult()
    {
        return ActionResult.Create("打开便签 Deck", "本机便签", ActionState.Error,
            "便签 Deck 没有打开", "离开上次编辑前的保存没有完成，便签内容未被覆盖",
            "返回便签页检查冲突或磁盘权限，然后重试", "NOTES-DECK-OPEN-FLUSH-FAILED");
    }

    private void OpenNotesDeck()
    {
        if (GuardNotesOperation("打开便签 Deck")) return;
        try
        {
            if (notesDeckForm == null || notesDeckForm.IsDisposed)
            {
                notesDeckForm = new NotesDeckForm(notesStore, aiProviderStore, aiTextService,
                    IsNotesRecordingActive, this, delegate
                    {
                        if (!IsDisposed && currentPageIndex == (int)VibePageId.Notes) ShowPage((int)VibePageId.Notes);
                    });
                notesDeckForm.ApplyTheme(darkTheme);
            }
            if (!notesDeckForm.ShowDeck())
            {
                ActionResult result = CreateNotesDeckOpenFailureResult();
                HostLog("NOTES DECK OPEN BLOCKED code=" + result.ErrorCode);
                ShowActionToast(result);
                return;
            }
            HostLog("NOTES DECK OPEN visible=" + notesDeckForm.Visible + " handle=" + notesDeckForm.Handle.ToInt64());
        }
        catch (Exception ex)
        {
            HostLog("NOTES DECK OPEN FAILED type=" + ex.GetType().Name);
            ShowActionToast(ActionResult.Create("打开便签 Deck", "本机便签", ActionState.Error,
                "便签 Deck 没有打开", "窗口创建失败", "重新打开；仍失败请查看自检", "NOTES-DECK-OPEN-FAILED"));
        }
    }

    private bool CloseNotesDeck()
    {
        if (notesDeckForm == null || notesDeckForm.IsDisposed) return true;
        if (!notesDeckForm.CloseWithOwner()) return false;
        notesDeckForm = null;
        return true;
    }

    private void HideNotesDeckForRecording()
    {
        if (applicationExiting || IsDisposed) return;
        if (InvokeRequired)
        {
            DispatchUi(HideNotesDeckForRecording);
            return;
        }
        if (notesDeckForm == null || notesDeckForm.IsDisposed) return;
        notesDeckForm.HideForRecording();
    }

    private void RefreshNotesDeckTheme()
    {
        if (notesDeckForm != null && !notesDeckForm.IsDisposed) notesDeckForm.ApplyTheme(darkTheme);
    }
}
