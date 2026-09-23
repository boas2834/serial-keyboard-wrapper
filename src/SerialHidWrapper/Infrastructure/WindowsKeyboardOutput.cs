using System.ComponentModel;
using System.Runtime.InteropServices;
using SerialHidWrapper.Core;

namespace SerialHidWrapper.Infrastructure;

internal sealed class WindowsKeyboardOutput : IKeyboardOutput
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const uint KeyEventUnicode = 0x0004;
    private const ushort VirtualKeyTab = 0x09;
    private const ushort VirtualKeyReturn = 0x0D;

    public void Send(string text, OutputSuffix suffix)
    {
        ArgumentNullException.ThrowIfNull(text);

        var suffixInputCount = suffix == OutputSuffix.None ? 0 : 2;
        var inputs = new INPUT[(text.Length * 2) + suffixInputCount];
        var index = 0;

        foreach (var character in text)
        {
            inputs[index++] = CreateUnicodeInput(character, keyUp: false);
            inputs[index++] = CreateUnicodeInput(character, keyUp: true);
        }

        if (suffix != OutputSuffix.None)
        {
            var virtualKey = suffix == OutputSuffix.Enter ? VirtualKeyReturn : VirtualKeyTab;
            inputs[index++] = CreateVirtualKeyInput(virtualKey, keyUp: false);
            inputs[index] = CreateVirtualKeyInput(virtualKey, keyUp: true);
        }

        if (inputs.Length == 0)
            return;

        var inserted = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (inserted != inputs.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Tastatureingabe konnte nicht vollständig gesendet werden.");
    }

    private static INPUT CreateUnicodeInput(char character, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KEYBDINPUT
            {
                Scan = character,
                Flags = KeyEventUnicode | (keyUp ? KeyEventKeyUp : 0)
            }
        }
    };

    private static INPUT CreateVirtualKeyInput(ushort virtualKey, bool keyUp) => new()
    {
        Type = InputKeyboard,
        Union = new InputUnion
        {
            Keyboard = new KEYBDINPUT
            {
                VirtualKey = virtualKey,
                Flags = keyUp ? KeyEventKeyUp : 0
            }
        }
    };

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;

        [FieldOffset(0)]
        public HARDWAREINPUT Hardware;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint Message;
        public ushort ParameterLow;
        public ushort ParameterHigh;
    }
}
