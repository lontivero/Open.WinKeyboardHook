using System;
using System.Windows.Forms;

namespace Open.WinKeyboardHook
{
    public interface IKeyboardInterceptor
    {
        event EventHandler<KeyEventArgs> KeyDown;
        event EventHandler<KeyEventArgs> KeyUp;
        event EventHandler<KeyPressEventArgs> KeyPress;
    }
}