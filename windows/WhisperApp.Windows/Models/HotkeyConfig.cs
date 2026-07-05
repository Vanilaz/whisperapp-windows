namespace WhisperApp.Models;

/// Hotkey configuration: virtual-key code + modifiers + mode (toggle or hold).
/// Mirrors the macOS HotkeyConfig, adapted to Windows virtual-key codes.
public class HotkeyConfig
{
    [Flags]
    public enum Mod : uint
    {
        None = 0,
        Control = 1,
        Alt = 2,
        Shift = 4,
        Win = 8,
    }

    public int KeyCode { get; set; }
    public uint Modifiers { get; set; }
    public bool IsHoldMode { get; set; }
    /// True if the hotkey IS a modifier key by itself (e.g. Right Ctrl alone).
    public bool IsModifierOnly { get; set; }

    /// macOS defaults to the Fn key, which Windows keyboards don't expose to applications
    /// (it's consumed by keyboard firmware). Right Ctrl is the closest equivalent: a
    /// modifier-only key most users don't already rely on, held for "hold to talk".
    public static HotkeyConfig Default => new()
    {
        KeyCode = VK.RCONTROL,
        Modifiers = 0,
        IsHoldMode = true,
        IsModifierOnly = true,
    };

    /// Human-readable shortcut string (e.g., "Ctrl+Alt+Space" or "Right Ctrl").
    public string DisplayString
    {
        get
        {
            if (IsModifierOnly)
            {
                return KeyCode switch
                {
                    VK.RCONTROL => "Right Ctrl",
                    VK.LCONTROL => "Left Ctrl",
                    VK.RSHIFT => "Right Shift",
                    VK.LSHIFT => "Left Shift",
                    VK.RMENU => "Right Alt",
                    VK.LMENU => "Left Alt",
                    VK.CAPITAL => "Caps Lock",
                    VK.LWIN => "Left Win",
                    VK.RWIN => "Right Win",
                    _ => "Modifier",
                };
            }

            var parts = new List<string>();
            var mod = (Mod)Modifiers;
            if (mod.HasFlag(Mod.Control)) parts.Add("Ctrl");
            if (mod.HasFlag(Mod.Alt)) parts.Add("Alt");
            if (mod.HasFlag(Mod.Shift)) parts.Add("Shift");
            if (mod.HasFlag(Mod.Win)) parts.Add("Win");

            var keyNames = new Dictionary<int, string>
            {
                [VK.SPACE] = "Space",
                [VK.RETURN] = "Enter",
                [VK.ESCAPE] = "Esc",
                [VK.TAB] = "Tab",
                [VK.BACK] = "Backspace",
                [VK.DELETE] = "Delete",
            };
            string keyStr = keyNames.TryGetValue(KeyCode, out var name) ? name : KeyNameFor(KeyCode);
            parts.Add(keyStr);
            return string.Join("+", parts);
        }
    }

    private static string KeyNameFor(int vk)
    {
        try
        {
            var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(vk);
            return key.ToString();
        }
        catch
        {
            return $"Key{vk}";
        }
    }
}
