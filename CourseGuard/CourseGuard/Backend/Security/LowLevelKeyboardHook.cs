using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CourseGuard.Backend.Security
{
    public static class LowLevelKeyboardHook
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private static LowLevelKeyboardProc? _proc = HookCallback;
        private static IntPtr _hookID = IntPtr.Zero;

        public static event EventHandler? OnCheatKeyPressed;

        public static void SetHook()
        {
            if (_hookID == IntPtr.Zero)
            {
                _hookID = SetHook(_proc!);
            }
        }

        public static void Unhook()
        {
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
            {
                int vkCode = Marshal.ReadInt32(lParam);

                bool isAltTab = (vkCode == 0x09 && IsKeyPressed(VK_MENU)); // Tab + Alt
                bool isAltEsc = (vkCode == 0x1B && IsKeyPressed(VK_MENU)); // Esc + Alt
                bool isCtrlEsc = (vkCode == 0x1B && IsKeyPressed(VK_CONTROL)); // Esc + Ctrl
                bool isLWin = vkCode == 0x5B;
                bool isRWin = vkCode == 0x5C;
                bool isAltF4 = (vkCode == 0x73 && IsKeyPressed(VK_MENU)); // F4 + Alt

                // Block copy-paste/cut shortcuts
                bool isCtrlC = (vkCode == 0x43 && IsKeyPressed(VK_CONTROL)); // Ctrl + C
                bool isCtrlV = (vkCode == 0x56 && IsKeyPressed(VK_CONTROL)); // Ctrl + V
                bool isCtrlX = (vkCode == 0x58 && IsKeyPressed(VK_CONTROL)); // Ctrl + X
                bool isShiftInsert = (vkCode == 0x2D && IsKeyPressed(VK_SHIFT)); // Shift + Insert

                if (isAltTab || isAltEsc || isCtrlEsc || isLWin || isRWin || isAltF4 || isCtrlC || isCtrlV || isCtrlX || isShiftInsert)
                {
                    OnCheatKeyPressed?.Invoke(null, EventArgs.Empty);
                    return (IntPtr)1; // Block the key
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

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
        private static extern short GetAsyncKeyState(int vKey);
        
        private const int VK_MENU = 0x12; // Alt
        private const int VK_CONTROL = 0x11; // Ctrl
        private const int VK_SHIFT = 0x10; // Shift
        
        private static bool IsKeyPressed(int vkCode)
        {
            return (GetAsyncKeyState(vkCode) & 0x8000) != 0;
        }
    }
}
