using System.Runtime.InteropServices;
using System.Text.Json;
using WhisperApp.Models;

namespace WhisperApp.Services;

/// Manages the global hotkey via a low-level keyboard hook (WH_KEYBOARD_LL) — supports
/// toggle/hold modes and modifier-only keys (e.g. Right Ctrl alone), matching the macOS
/// HotkeyManager's behavior. Unlike macOS, Windows delivers modifier keys through the same
/// WM_KEYDOWN/WM_KEYUP pipeline as regular keys, so no separate "flagsChanged" handling
/// is needed here.
///
/// Note: WH_KEYBOARD_LL only sees input for windows at the same (or lower) integrity level.
/// It won't fire while an elevated (Run as Administrator) window has focus.
public class HotkeyManager
{
    public static readonly HotkeyManager Shared = new();

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Kept as a field so the delegate isn't garbage-collected while the hook is installed.
    private LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;

    private HotkeyConfig _config;
    private bool _isHolding;
    private bool _modifierKeyDown;
    private long _lastModifierPressMs;
    private const long DoubleTapIntervalMs = 400;

    private const string SettingsKey = "hotkey.config";

    /// Called on activation (toggle: flip recording; hold: start recording).
    public Action? OnKeyDown;
    /// Called on deactivation (hold mode: stop recording).
    public Action? OnKeyUp;
    /// Returns true while recording — lets toggle mode stop with a single tap.
    public Func<bool>? IsActive;

    private HotkeyManager()
    {
        _config = LoadConfig() ?? HotkeyConfig.Default;
    }

    public HotkeyConfig CurrentConfig => _config;

    public void UpdateConfig(HotkeyConfig config)
    {
        _config = config;
        SaveConfig(config);
        // No need to reinstall the hook — HandleKey reads _config live.
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _proc = HookCallback;
        using var curModule = System.Diagnostics.Process.GetCurrentProcess().MainModule;
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc,
            GetModuleHandle(curModule?.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
        {
            System.Diagnostics.Debug.WriteLine("[HotkeyManager] Failed to install keyboard hook");
        }
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            int msg = (int)wParam;
            bool isDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
            bool isUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

            if (isDown || isUp)
            {
                try { HandleKey(vkCode, isDown); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[HotkeyManager] Handler error: {ex.Message}"); }
            }
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void HandleKey(int vkCode, bool isDown)
    {
        if (_config.IsModifierOnly)
        {
            HandleModifierKey(vkCode, isDown);
        }
        else
        {
            HandleComboKey(vkCode, isDown);
        }
    }

    /// Handle modifier-only hotkeys (e.g. Right Ctrl alone).
    private void HandleModifierKey(int vkCode, bool isDown)
    {
        if (vkCode != _config.KeyCode) return;

        if (isDown && !_modifierKeyDown)
        {
            _modifierKeyDown = true;
            if (_config.IsHoldMode)
            {
                OnKeyDown?.Invoke();
            }
            else if (IsActive?.Invoke() == true)
            {
                // Toggle mode, currently recording: single tap stops.
                _lastModifierPressMs = 0;
                OnKeyDown?.Invoke();
            }
            else
            {
                // Toggle mode, idle: require double-tap to avoid accidental triggers.
                long now = Environment.TickCount64;
                if (now - _lastModifierPressMs < DoubleTapIntervalMs)
                {
                    _lastModifierPressMs = 0;
                    OnKeyDown?.Invoke();
                }
                else
                {
                    _lastModifierPressMs = now;
                }
            }
        }
        else if (!isDown && _modifierKeyDown)
        {
            _modifierKeyDown = false;
            if (_config.IsHoldMode) OnKeyUp?.Invoke();
        }
    }

    /// Handle regular key+modifier combos (e.g. Ctrl+Alt+Space).
    private void HandleComboKey(int vkCode, bool isDown)
    {
        if (vkCode != _config.KeyCode) return;
        if (CurrentModifiers() != (HotkeyConfig.Mod)_config.Modifiers) return;

        if (isDown)
        {
            if (_config.IsHoldMode)
            {
                if (_isHolding) return;
                _isHolding = true;
                OnKeyDown?.Invoke();
            }
            else
            {
                OnKeyDown?.Invoke();
            }
        }
        else if (_config.IsHoldMode && _isHolding)
        {
            _isHolding = false;
            OnKeyUp?.Invoke();
        }
    }

    private static HotkeyConfig.Mod CurrentModifiers()
    {
        var mod = HotkeyConfig.Mod.None;
        if (IsKeyDown(VK.CONTROL)) mod |= HotkeyConfig.Mod.Control;
        if (IsKeyDown(VK.MENU)) mod |= HotkeyConfig.Mod.Alt;
        if (IsKeyDown(VK.SHIFT)) mod |= HotkeyConfig.Mod.Shift;
        if (IsKeyDown(VK.LWIN) || IsKeyDown(VK.RWIN)) mod |= HotkeyConfig.Mod.Win;
        return mod;
    }

    private static bool IsKeyDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    private static HotkeyConfig? LoadConfig()
    {
        var bytes = AppSettingsStore.GetBytes(SettingsKey);
        if (bytes == null) return null;
        try
        {
            return JsonSerializer.Deserialize<HotkeyConfig>(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static void SaveConfig(HotkeyConfig config)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(config);
        AppSettingsStore.SetBytes(SettingsKey, bytes);
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);
}
