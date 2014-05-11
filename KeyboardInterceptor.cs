using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32.SafeHandles;

namespace Open.WinKeyboardHook
{
    internal sealed class SafeWinHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWinHandle()
            : base(true)
        {
        }

        [SecurityCritical]
        protected override bool ReleaseHandle()
        {
            if(IsInvalid)
                return true;
            return NativeMethods.UnhookWindowsHookEx(handle);
        }
    }

    public sealed class KeyboardInterceptor : IKeyboardInterceptor
    {
        private static DeadKeyInfo _lastDeadKey;
        private readonly KeysConverter _keyConverter = new KeysConverter();
        private bool _isHooked;

        private EventHandler<KeyEventArgs> _keyDown;
        private EventHandler<KeyPressEventArgs> _keyPress;
        private EventHandler<KeyEventArgs> _keyUp;
        private LowLevelKeyboardProc _keyboardProc;
        private IntPtr _previousKeyboardHandler;

        public KeyboardInterceptor()
        {
            HookKeyboard();
        }

        #region IKeyboardInterceptor Members

        public event EventHandler<KeyEventArgs> KeyDown
        {
            add { _keyDown += value; }
            remove { if (_keyDown != null) _keyDown -= value; }
        }

        public event EventHandler<KeyEventArgs> KeyUp
        {
            add { _keyUp += value; }
            remove { if (_keyUp != null) _keyUp -= value; }
        }

        public event EventHandler<KeyPressEventArgs> KeyPress
        {
            add { _keyPress += value; }
            remove { if (_keyPress != null) _keyPress -= value; }
        }

        #endregion

        private void HookKeyboard()
        {
            VerifyPreviousHooking();

            _keyboardProc = keyboardHandler;
            using (var process = Process.GetCurrentProcess())
            {
                using (var module = process.MainModule)
                {
                    var moduleHandler = NativeMethods.GetModuleHandle(module.ModuleName);

                    _previousKeyboardHandler = NativeMethods.SetWindowsHookEx(
                        NativeMethods.WH_KEYBOARD_LL, _keyboardProc, moduleHandler, 0);

                    Console.WriteLine(Marshal.GetLastWin32Error());
                    //if (_previousKeyboardHandler.IsInvalid)
                    //{
                    //    throw new Win32Exception(Marshal.GetLastWin32Error());
                    //}

                    _isHooked = true;
                }
            }
        }

        private void VerifyPreviousHooking()
        {
            if (_isHooked)
            {
                throw new InvalidOperationException("It is already hooked");
            }
        }

        private IntPtr keyboardHandler(int nCode, IntPtr wParam, ref KBDLLHOOKSTRUCT kbdStruct)
        {
            IntPtr ret;
            Console.WriteLine(Marshal.GetLastWin32Error());

            try
            {
                if (nCode >= 0)
                {
                    var virtualKeyCode = (Keys) kbdStruct.KeyCode;
                    Keys keyData = BuildKeyData(virtualKeyCode);
                    var keyEventArgs = new KeyEventArgs(keyData);

                    var intParam = wParam.ToInt32();
                    if (intParam == NativeMethods.WM_KEYDOWN || intParam == NativeMethods.WM_SYSKEYDOWN)
                    {
                        RaiseKeyDownEvent(keyEventArgs);

                        string buffer = ToUnicode(kbdStruct);
                        if (!string.IsNullOrEmpty(buffer))
                        {
                            foreach (char rawKey in buffer)
                            {
                                string s = _keyConverter.ConvertToString(rawKey);
                                if (s != null)
                                {
                                    char key = s[0];
                                    RaiseKeyPressEvent(key);
                                }
                            }
                        }
                    }
                    else if (intParam == NativeMethods.WM_KEYUP || intParam == NativeMethods.WM_SYSKEYUP)
                    {
                        Debug.Print("Release({0})", (int) virtualKeyCode);
                        RaiseKeyUpEvent(keyEventArgs);
                    }
                }
            }
            finally
            {
                ret = NativeMethods.CallNextHookEx(_previousKeyboardHandler, nCode, wParam, ref kbdStruct);
            }

            return ret;
        }


        private static Keys BuildKeyData(Keys virtualKeyCode)
        {
            bool isDownControl = IsKeyPressed(NativeMethods.VK_LCONTROL) || IsKeyPressed(NativeMethods.VK_RCONTROL);
            bool isDownShift = IsKeyPressed(NativeMethods.VK_LSHIFT) || IsKeyPressed(NativeMethods.VK_RSHIFT);
            bool isDownAlt = IsKeyPressed(NativeMethods.VK_LALT) || IsKeyPressed(NativeMethods.VK_RALT) ||
                             IsKeyPressed(NativeMethods.VK_RMENU);
            bool isAltGr = IsKeyPressed(NativeMethods.VK_RMENU) && IsKeyPressed(NativeMethods.VK_LCONTROL);

            return virtualKeyCode |
                   (isDownControl ? Keys.Control : Keys.None) |
                   (isDownShift ? Keys.Shift : Keys.None) |
                   (isDownAlt ? Keys.Alt : Keys.None) |
                   (isAltGr ? (Keys) 524288 : Keys.None);
        }

        private static bool IsKeyPressed(byte virtualKeyCode)
        {
            return (NativeMethods.GetKeyState(virtualKeyCode) & 0x80) != 0;
        }

        private void RaiseKeyPressEvent(char key)
        {
            EventHandler<KeyPressEventArgs> keyPress = _keyPress;
            if (keyPress != null)
            {
                keyPress(this, new KeyPressEventArgs(key));
            }
        }

        private void RaiseKeyDownEvent(KeyEventArgs args)
        {
            EventHandler<KeyEventArgs> keyDown = _keyDown;
            if (keyDown != null)
            {
                keyDown(this, args);
            }
        }

        private void RaiseKeyUpEvent(KeyEventArgs args)
        {
            EventHandler<KeyEventArgs> keyUp = _keyUp;
            if (keyUp != null)
            {
                keyUp(this, args);
            }
        }

        private static string ToUnicode(KBDLLHOOKSTRUCT info)
        {
            string result = null;

            var keyState = new byte[256];
            var buffer = new StringBuilder(128);

            var success = NativeMethods.GetKeyboardState(keyState);
            if(!success) return string.Empty;

            bool isAltGr = IsKeyPressed(NativeMethods.VK_RMENU) && IsKeyPressed(NativeMethods.VK_LCONTROL);
            if (isAltGr) keyState[NativeMethods.VK_LCONTROL] = keyState[NativeMethods.VK_LALT] = 0x80;

            IntPtr layout = GetForegroundKeyboardLayout();
            int count = ToUnicode((Keys) info.KeyCode, info.ScanCode, keyState, buffer, layout);

            if (count > 0)
            {
                result = buffer.ToString(0, count);

                if (_lastDeadKey != null)
                {
                    ToUnicode(_lastDeadKey.KeyCode,
                              _lastDeadKey.ScanCode,
                              _lastDeadKey.KeyboardState,
                              buffer,
                              layout);

                    _lastDeadKey = null;
                }
            }
            else if (count < 0)
            {
                _lastDeadKey = new DeadKeyInfo(info, keyState);

                while (count < 0)
                {
                    count = ToUnicode(Keys.Decimal, buffer, layout);
                }
            }

            return result;
        }

        private static IntPtr GetForegroundKeyboardLayout()
        {
            IntPtr foregroundWnd = NativeMethods.GetForegroundWindow();

            if (foregroundWnd != IntPtr.Zero)
            {
                uint processId;
                uint threadId = NativeMethods.GetWindowThreadProcessId(foregroundWnd, out processId);

                return NativeMethods.GetKeyboardLayout(threadId);
            }

            return IntPtr.Zero;
        }

        private static int ToUnicode(Keys vk, StringBuilder buffer, IntPtr hkl)
        {
            return ToUnicode(vk, ToScanCode(vk), new byte[256], buffer, hkl);
        }

        private static int ToUnicode(Keys vk, uint sc, byte[] keyState, StringBuilder buffer, IntPtr hkl)
        {
            return NativeMethods.ToUnicodeEx((uint) vk, sc, keyState, buffer, buffer.Capacity, 0, hkl);
        }

        private static uint ToScanCode(Keys vk)
        {
            return NativeMethods.MapVirtualKey((uint) vk, 0);
        }

        #region Nested type: DeadKeyInfo

        private sealed class DeadKeyInfo
        {
            public readonly Keys KeyCode;
            public readonly Byte[] KeyboardState;
            public readonly UInt32 ScanCode;

            public DeadKeyInfo(KBDLLHOOKSTRUCT info, byte[] keyState)
            {
                KeyCode = (Keys) info.KeyCode;
                ScanCode = info.ScanCode;

                KeyboardState = keyState;
            }
        }

        #endregion
    }
}