using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace WhisperApp.Services;

/// Copy text to the clipboard and simulate Ctrl+V into the focused app.
/// Unlike macOS (which requires explicit Accessibility permission to synthesize keystrokes),
/// Windows allows SendInput from any unelevated process into other unelevated windows —
/// the one caveat is UIPI: an app running elevated (as Administrator) won't accept synthetic
/// input from this non-elevated process. That's a known, documented limitation.
public static class Paster
{
    public static void Paste(string text)
    {
        // Clipboard access must happen on an STA/UI thread.
        WpfApplication.Current?.Dispatcher.Invoke(() => SetClipboardWithRetry(text));

        // Small delay so the clipboard write lands before the keystroke is synthesized.
        Task.Delay(50).ContinueWith(_ => SendCtrlV());
    }

    private static void SetClipboardWithRetry(string text)
    {
        // The Windows clipboard is briefly locked by other apps (clipboard managers, etc.)
        // fairly often — retry a few times instead of silently failing once.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return;
            }
            catch (COMException)
            {
                Thread.Sleep(30);
            }
        }
        System.Diagnostics.Debug.WriteLine("[Paster] Could not set clipboard after retries");
    }

    private static void SendCtrlV()
    {
        const int vkControl = 0x11;
        const int vkV = 0x56;

        var inputs = new[]
        {
            KeyInput(vkControl, down: true),
            KeyInput(vkV, down: true),
            KeyInput(vkV, down: false),
            KeyInput(vkControl, down: false),
        };
        uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            // SendInput rejects the whole call (returns 0) if cbSize doesn't exactly match
            // what Windows expects for INPUT on the current bitness - which silently means
            // "nothing was typed" with no exception thrown anywhere.
            int err = Marshal.GetLastWin32Error();
            System.Diagnostics.Debug.WriteLine($"[Paster] SendInput sent {sent}/{inputs.Length} events (Win32 error {err})");
        }
    }

    private static INPUT KeyInput(int vk, bool down) => new()
    {
        type = InputKeyboard,
        u = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = 0,
                dwFlags = down ? 0u : KeyEventFKeyUp,
                time = 0,
                dwExtraInfo = IntPtr.Zero,
            },
        },
    };

    private const int InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    // The native INPUT union must be sized to fit its LARGEST member (MOUSEINPUT, which is
    // bigger than KEYBDINPUT on both x86 and x64) even though we only ever populate `ki`.
    // Declaring only `ki` here made this struct too small, so Marshal.SizeOf<INPUT>() didn't
    // match what SendInput's cbSize parameter must equal - SendInput checks that and silently
    // returns 0 (sends nothing) on a mismatch, with no exception anywhere in managed code.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public int type;
        public InputUnion u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
