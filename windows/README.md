# Whisper for Windows

A Windows system-tray port of [WhisperApp](../README.md) (originally macOS/Swift): press a
hotkey, speak, and the corrected text is pasted into whatever you're typing.

Functionally equivalent to the current macOS build (v1.2, Groq-only): global hotkey
(hold-to-talk or toggle), Groq Whisper transcription, Groq Llama text correction, auto-paste,
floating waveform/status overlay, and a tray menu with language + STT/correction toggles.

## Requirements

- Windows 10 (1809+) or Windows 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build (the published exe is
  self-contained and needs no separate runtime install on the target machine)
- A microphone — the first recording attempt triggers the standard Windows "Allow app to
  access your microphone" prompt (Settings → Privacy & security → Microphone)

## Build & run (dev loop)

```powershell
cd windows
./run.ps1
```

## Build a distributable

```powershell
cd windows
./publish.ps1               # -> windows/dist/Whisper.exe (self-contained, single file)
```

If [Inno Setup](https://jrsoftware.org/isinfo.php)'s `ISCC.exe` is on `PATH`, `publish.ps1` also
produces `Whisper-Setup.exe` (per-user install, optional "run at sign-in" shortcut, Start Menu
entry, uninstaller) via `installer.iss`.

## Configure keys

Keys are read from (in order): the Settings window (saved to `%AppData%\Whisper\*.key`) →
environment variable. Unlike macOS, Windows GUI apps inherit your normal user environment
variables directly — no shell profile sourcing needed.

```powershell
# optional: set persistently instead of using the Settings UI
[Environment]::SetEnvironmentVariable("GROQ_API_KEY", "gsk_...", "User")
```

## Architecture

- WPF app (`net8.0-windows`), tray-only via `System.Windows.Forms.NotifyIcon`
  (`ShutdownMode="OnExplicitShutdown"` — no window means no window closing the app)
- Global hotkey: `WH_KEYBOARD_LL` low-level keyboard hook (`Services/HotkeyManager.cs`).
  Windows delivers modifier keys (Ctrl/Alt/Shift/Win/Caps Lock) through the same
  `WM_KEYDOWN`/`WM_KEYUP` pipeline as regular keys, so — unlike macOS, which needs a separate
  `flagsChanged` handler for modifier-only hotkeys — one code path handles both cases here.
- Audio: NAudio `WaveInEvent` capturing directly at 16kHz mono 16-bit PCM (the Windows wave
  mapper performs the format conversion at the driver level, so no manual resampler is needed)
- Cloud STT/LLM: `HttpClient` multipart/JSON requests, same provider registries as macOS
  (`Services/STTProvider.cs`, `Services/LLMProvider.cs`)
- Paste: clipboard (`Clipboard.SetText`, with retry — the Windows clipboard is transiently
  locked by other apps more often than macOS's pasteboard) + synthesized Ctrl+V via `SendInput`
- Floating overlay: borderless, transparent, `Topmost` WPF window with `WS_EX_TRANSPARENT |
  WS_EX_NOACTIVATE` applied via `SetWindowLong` so it never steals focus or intercepts clicks
  (equivalent to the macOS `NSPanel` with `.nonactivatingPanel` + `ignoresMouseEvents`)

## Known differences from macOS

- **Default hotkey is Right Ctrl (hold-to-talk), not Fn.** Windows keyboards don't expose the
  Fn key to applications — it's consumed by keyboard firmware before the OS ever sees it.
  Right Ctrl is the closest equivalent (a modifier-only key most users don't already rely on).
  Changeable in Settings, same as macOS.
- **No Accessibility-permission equivalent.** `SendInput` works without any permission prompt,
  but per Windows UIPI, it can't send synthetic keystrokes into a window running at a higher
  integrity level (e.g. an app you launched "as Administrator") if Whisper itself isn't
  elevated. If paste silently does nothing in one specific app, that's almost always why —
  the transcribed text is still on the clipboard, so Ctrl+V manually always works.
- **Tray icon stays static across stages**; per-stage iconography (mic / waveform / sparkles /
  checkmark) lives entirely in the floating status overlay and the tray tooltip text, rather
  than swapping the tray icon's glyph like macOS's SF Symbols do.
- **No local/offline STT fallback.** The macOS build's `whisper.cpp` fallback was already
  dropped from the current app (v1.2 is Groq-cloud-only per the STT/LLM registries) — this
  port matches that, so there's no local-model path to port.
