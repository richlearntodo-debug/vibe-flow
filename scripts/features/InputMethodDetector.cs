using System;
using System.Runtime.InteropServices;

// Which input method owns the keyboard right now decides whether a voice tool can
// be reached at all: WeChat's and Doubao's voice panels only exist while their own
// input method is active, and Doubao additionally filters synthetic input. Windows
// dictation (Win+H) and shortcut-driven tools do not depend on the active IME.
//
// Both identifiers are matched because TSF reports the TIP CLSID and the language
// profile GUID, and the two are easy to confuse: on this machine (verified
// 2026-09-10) Doubao is CLSID {9D2B2E2B-...} with profile {2B4D4B3A-...}, WeType is
// CLSID {86598FB9-...} with profile {607FDF85-...}, and Microsoft Pinyin is CLSID
// {81d4e9c9-...} with profile {FA550B04-...}.
internal static class InputEngineCatalog
{
    internal const string DoubaoEngine = "doubao";
    internal const string WeChatEngine = "wechat";
    internal const string MicrosoftPinyinEngine = "microsoft-pinyin";
    internal const string UnknownEngine = "unknown";

    private static readonly string[] DoubaoIdentifiers =
    {
        "9D2B2E2B-3C93-4D2F-9D35-6EEB85F0D2B0",
        "2B4D4B3A-4D4F-4C0A-8E66-7F771A2B9C10"
    };

    private static readonly string[] WeChatIdentifiers =
    {
        "86598FB9-66A2-463E-B9C2-AEB906D477AD",
        "607FDF85-FCC8-4DBD-A365-41296F980C9C"
    };

    private static readonly string[] MicrosoftPinyinIdentifiers =
    {
        "81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E",
        "FA550B04-5AD7-411F-A5AC-CA038EC515D7"
    };

    internal static string ClassifyEngine(string classId, string profileGuid)
    {
        if (Matches(DoubaoIdentifiers, classId) || Matches(DoubaoIdentifiers, profileGuid))
            return DoubaoEngine;
        if (Matches(WeChatIdentifiers, classId) || Matches(WeChatIdentifiers, profileGuid))
            return WeChatEngine;
        if (Matches(MicrosoftPinyinIdentifiers, classId) || Matches(MicrosoftPinyinIdentifiers, profileGuid))
            return MicrosoftPinyinEngine;
        return UnknownEngine;
    }

    internal static string DescribeEngine(string engineKey)
    {
        switch (engineKey)
        {
            case DoubaoEngine: return "豆包输入法";
            case WeChatEngine: return "微信输入法";
            case MicrosoftPinyinEngine: return "微软拼音";
            default: return "未知输入法";
        }
    }

    // Only the WeChat input method is offered as a voice tool whose panel is bound
    // to its own input method; Windows dictation (Win+H) and Typeless are driven by
    // system-level shortcuts that do not depend on the active input method. The
    // Doubao engine is still identified for diagnostics, but V2.0 no longer offers
    // it as a selectable voice tool.
    internal static bool ProviderRequiresOwnInputMethod(string normalizedProviderKey)
    {
        return normalizedProviderKey == WeChatEngine;
    }

    internal static bool ActiveEngineBlocksProvider(string normalizedProviderKey, string engineKey)
    {
        if (!ProviderRequiresOwnInputMethod(normalizedProviderKey)) return false;
        if (engineKey == UnknownEngine) return false;
        return !string.Equals(normalizedProviderKey, engineKey, StringComparison.Ordinal);
    }

    private static bool Matches(string[] identifiers, string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        string value = candidate.Trim().Trim('{', '}');
        for (int index = 0; index < identifiers.Length; index++)
        {
            if (string.Equals(value, identifiers[index], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}

internal sealed class ActiveInputEngine
{
    internal bool Known;
    internal string EngineKey = InputEngineCatalog.UnknownEngine;
    internal string ClassId = "";
    internal string ProfileGuid = "";
    internal int LanguageId = -1;
    internal long KeyboardLayout;

    internal string DisplayName
    {
        get { return InputEngineCatalog.DescribeEngine(EngineKey); }
    }
}

internal static class InputMethodDetector
{
    [ComImport, Guid("33C53A50-F456-4884-B049-85FD643ECFED")]
    private class TFInputProcessorProfilesComObject { }

    [Guid("71C6E74C-0F28-11D8-A82A-00065B84435C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfInputProcessorProfileMgr
    {
        int ActivateProfile(int profileType, ushort languageId, ref Guid classId, ref Guid profileGuid, IntPtr keyboardLayout, int flags);
        int DeactivateProfile(int profileType, ushort languageId, ref Guid classId, ref Guid profileGuid, IntPtr keyboardLayout, int flags);
        int GetProfile(int profileType, ushort languageId, ref Guid classId, ref Guid profileGuid, IntPtr keyboardLayout, out TFInputProcessorProfile profile);
        int EnumProfiles(ushort languageId, out IntPtr enumerator);
        int ReleaseInputProcessor(ref Guid classId, int flags);
        int RegisterProfile(ref Guid classId, ushort languageId,
            [MarshalAs(UnmanagedType.LPWStr)] string description, int descriptionLength,
            [MarshalAs(UnmanagedType.LPWStr)] string iconFile, int iconFileLength, int iconIndex,
            IntPtr substituteKeyboardLayout, int preferredLayout, int enabledByDefault, int flags);
        int UnregisterProfile(ref Guid classId, ushort languageId, int flags);
        int GetActiveProfile(ref Guid categoryId, IntPtr profile);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TFInputProcessorProfile
    {
        public int ProfileType;
        public ushort LanguageId;
        public Guid ClassId;
        public Guid ProfileGuid;
        public IntPtr KeyboardLayout;
        public int Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(int threadId);

    private static readonly Guid KeyboardCategory = new Guid("34745C63-B2F0-4784-8B67-5E12C8701A31");
    private const int ProfileBufferBytes = 256;

    internal static bool TryReadActiveEngine(out ActiveInputEngine engine, out string errorCode)
    {
        engine = new ActiveInputEngine();
        errorCode = "";
        // IMM32 is the reliable source for the active keyboard layout; TSF names the
        // owning text service. The profile is written into a caller-allocated buffer
        // because marshaling the struct as an out parameter fails on this stack.
        engine.KeyboardLayout = GetKeyboardLayout(0).ToInt64();
        IntPtr buffer = IntPtr.Zero;
        try
        {
            var profiles = (ITfInputProcessorProfileMgr)(object)new TFInputProcessorProfilesComObject();
            Guid category = KeyboardCategory;
            // The buffer is deliberately larger than the profile structure: the
            // service writes the structure it was compiled with, and a short buffer
            // corrupts the process heap instead of returning a clean error.
            buffer = Marshal.AllocHGlobal(ProfileBufferBytes);
            for (int index = 0; index < ProfileBufferBytes; index++) Marshal.WriteByte(buffer, index, 0);
            int hr = profiles.GetActiveProfile(ref category, buffer);
            if (hr != 0)
            {
                errorCode = "IME-TSF-0x" + hr.ToString("X8");
                return false;
            }
            TFInputProcessorProfile profile =
                (TFInputProcessorProfile)Marshal.PtrToStructure(buffer, typeof(TFInputProcessorProfile));
            engine.ClassId = profile.ClassId.ToString("B").ToUpperInvariant();
            engine.ProfileGuid = profile.ProfileGuid.ToString("B").ToUpperInvariant();
            engine.LanguageId = profile.LanguageId;
            engine.EngineKey = InputEngineCatalog.ClassifyEngine(engine.ClassId, engine.ProfileGuid);
            engine.Known = engine.EngineKey != InputEngineCatalog.UnknownEngine;
            return true;
        }
        catch (Exception ex)
        {
            errorCode = "IME-TSF-" + ex.GetType().Name +
                (ex.HResult != 0 ? ":0x" + ex.HResult.ToString("X8") : "");
            return false;
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
        }
    }
}
