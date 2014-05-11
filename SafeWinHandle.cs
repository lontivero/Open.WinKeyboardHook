using System.Security;
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
            return NativeMethods.UnhookWindowsHookEx(handle);
        }
    }
}