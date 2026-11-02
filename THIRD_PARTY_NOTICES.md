# Third-party notices

## VB-CABLE

VB-CABLE is developed and distributed by VB-Audio (origin: <https://www.vb-cable.com>, product page <https://vb-audio.com/Cable/>). **VB-CABLE is a donationware — all participations are welcome.**

Starting with this build, Vibe Flow bundles the **unmodified official VB-CABLE driver package** (`VBCABLE_Driver_Pack45.zip`, pinned SHA-256 `b950e39f01af1d04ea623c8f6d8eb9b6ea5c477c637295fabf20631c85116bfb`) so a first-run install works offline. Bundling is permitted by VB-Audio under the "VB-CABLE Distribution with other product" terms on <https://vb-audio.com/Services/licensing.htm>, which require that end users can identify VB-CABLE as a VB-Audio application and remain able to donate/license it — Vibe Flow shows this notice during installation and links to the official page. If the bundled package is missing or fails verification, the installer falls back to the official download URL. Only the free VB-CABLE package is bundled; VB-CABLE A+B / C+D are never included.

## Microsoft reference assemblies

The build dependency restore script downloads `System.Runtime 4.3.1` and `Microsoft.Windows.SDK.Contracts 10.0.26100.4948` from NuGet. These packages are published by Microsoft and remain subject to their respective licenses.

## NAudio

Vibe Flow uses NAudio 2.2.1 for event-driven WASAPI output to the virtual microphone. NAudio is Copyright Mark Heath and contributors and is distributed under the MIT License: <https://github.com/naudio/NAudio/blob/master/license.txt>.

## Inno Setup translation

The Windows installer is compiled with Inno Setup. The repository includes the official Inno Setup 6 Simplified Chinese message file at `installer/languages/ChineseSimplified.isl`, maintained by Zhenghan Yang and sourced from [`jrsoftware/issrc`](https://github.com/jrsoftware/issrc). Inno Setup itself is a build-time tool and is not included in the application package.

## RC003 ATVV protocol research

The RC003 ATVV investigation was informed by the open-source project [`HD838A/remote-mic-app`](https://github.com/HD838A/remote-mic-app), distributed under GPL-3.0. Vibe Flow is also distributed under GPL-3.0. The reference repository itself is not included here.

## Optional transcription clients

WeChat Input Method, NetEase Bage (网易八哥说), and Voquill are optional external transcription clients and are not bundled with Vibe Flow. V2.0 does not integrate iFlytek Voice Input (讯飞语音输入法), Sogou Input Method (搜狗输入法) or Doubao Input Method (豆包输入法); old configuration values for any of them are migrated to the stable WeChat profile with a one-time notice. For every selectable tool, the shortcut stored in Vibe Flow must equal the shortcut configured inside that tool. Typeless and Windows Voice Typing are no longer selectable voice tools in V2.0; their names remain here only because earlier builds referenced them. Their trademarks, services, privacy policies, licenses, and network behavior belong to their respective owners. The Voquill provider profile follows the current open-source Windows default hotkey documented in [`voquill/voquill`](https://github.com/voquill/voquill); no Voquill source code is included in Vibe Flow.

## Optional cloud ASR (experimental, off by default)

V2.0 also carries an experimental fourth voice slot that performs recognition itself by calling a
hosted HTTP API instead of driving a third-party input method. It is **disabled by default**, is only
reachable after the user accepts an explicit in-app consent dialog, and never sends anything while no
recording key is held. When enabled, the audio recorded during one press-to-talk session is uploaded
**once, after the recording key is released**, to the endpoint the user configured.

The shipped default is SiliconFlow (硅基流动, <https://siliconflow.cn>) at
`https://api.siliconflow.cn/v1/audio/transcriptions`, using the `FunAudioLLM/SenseVoiceSmall` model,
with `Qwen/Qwen3-ASR-1.7B` available as a paid alternative. Both the endpoint and the model name are
user-configurable. No provider code, model weights, SDK or API key is bundled or embedded: Vibe Flow
sends one HTTPS multipart request per dictation and keeps only the returned text, in memory, long
enough to type it into the focused input box. Account terms, pricing, availability, retention and
privacy policy are those of the provider the user chooses; the API key is read from a local file
outside the repository (default `%USERPROFILE%\.codex\secrets\siliconflow_api_key.txt`).

SenseVoice is published by FunAudioLLM under Apache-2.0 and Qwen by Alibaba Cloud under its own model
licence; neither is distributed with Vibe Flow. Model and service names are trademarks of their
respective owners.

## Product artwork

The Vibe Flow logo and application screenshots are project assets distributed with this repository under GPL-3.0 unless noted otherwise.
