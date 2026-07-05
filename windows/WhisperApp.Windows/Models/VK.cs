namespace WhisperApp.Models;

/// Windows virtual-key codes we care about (subset — see WinUser.h for the full list).
public static class VK
{
    public const int LSHIFT = 0xA0;
    public const int RSHIFT = 0xA1;
    public const int LCONTROL = 0xA2;
    public const int RCONTROL = 0xA3;
    public const int LMENU = 0xA4;   // Left Alt
    public const int RMENU = 0xA5;   // Right Alt
    public const int LWIN = 0x5B;
    public const int RWIN = 0x5C;
    public const int CAPITAL = 0x14; // Caps Lock

    public const int SHIFT = 0x10;
    public const int CONTROL = 0x11;
    public const int MENU = 0x12;    // Alt

    public const int SPACE = 0x20;
    public const int RETURN = 0x0D;
    public const int ESCAPE = 0x1B;
    public const int TAB = 0x09;
    public const int BACK = 0x08;
    public const int DELETE = 0x2E;

    /// Modifier keys that generate their own WM_KEYDOWN/UP (unlike macOS, Windows does not
    /// need a separate "flagsChanged" event — modifier keys go through the normal key pipeline).
    public static readonly HashSet<int> ModifierKeyCodes = new()
    {
        LSHIFT, RSHIFT, LCONTROL, RCONTROL, LMENU, RMENU, LWIN, RWIN, CAPITAL
    };
}
