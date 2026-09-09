[简体中文](https://github.com/KiYouJyo/Eizo.Playback/blob/v0.1.0/docs/RELEASE-NOTES-v0.1.0.md) | [日本語](https://github.com/KiYouJyo/Eizo.Playback/blob/v0.1.0/docs/RELEASE-NOTES-v0.1.0.ja.md) | [English](https://github.com/KiYouJyo/Eizo.Playback/blob/v0.1.0/docs/RELEASE-NOTES-v0.1.0.en.md)

# Eizo.Playback v0.1.0 首个公开播放器内核版本

- 建立独立于 Eizo UI 的播放抽象层，统一媒体打开、播放/暂停/停止、Seek、音量、倍速、状态与错误事件。
- 提供基于 LibVLC 的 Windows 后端，并通过独立适配层隔离 LibVLCSharp 类型，避免 Eizo UI 直接依赖后端 API。
- 提供 WinUI 3 `PlaybackView` 与 D3D11 SwapChain 视频输出，用于 Eizo 的原生 Windows 播放界面。
- 支持音频、视频、字幕轨发现与切换，外部字幕、字幕/音频延迟、标题与章节导航。
- 提供播放能力与运行时诊断，同时避免在诊断信息中泄露本地媒体路径等敏感输入信息。
- 提供四个 NuGet 包：`Eizo.Playback.Abstractions`、`Eizo.Playback.Core`、`Eizo.Playback.LibVLC`、`Eizo.Playback.LibVLC.WinUI`。
- Release 同时提供 SHA-256 清单；CI 会执行 Release 构建、测试、打包和包内容校验。

> v0.1.0 是 Eizo.Playback 的首个稳定集成基线。当前 Eizo v0.2.1 固定集成此版本。