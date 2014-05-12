[![Build status](https://ci.appveyor.com/api/projects/status/lvi07odtaoeeohe3)](https://ci.appveyor.com/project/lontivero/open-winkeyboardhook) [![NuGet version](https://badge.fury.io/nu/Open.WinKeyboardHook.svg)](http://badge.fury.io/nu/Open.WinKeyboardHook)

Open.WinKeyboardHook
====================

A simple and easy-to-use .NET managed wrapper for Low Level Keyboard hooking.

Goals
-----
The main goal is to abstract away the complexities inherit to intercept and translate global keystrokes (KeyDown / KeyUp / KeyPress) in the system. 


Usage
-----

    public partial class TestForm : Form
    {
        private readonly IKeyboardInterceptor _interceptor;

        public TestForm()
        {
            InitializeComponent();

            // Everytime a key is press we want to display it in a TextBox
            _interceptor = new KeyboardInterceptor();
            _interceptor.KeyPress += (sender, args) => txt.Text += args.KeyChar;
        }

        // Start and Stop capturing keystroks
        private void BtnClick(object sender, EventArgs e)
        {
            if(!_capturing)
            {
                _interceptor.StartCapturing();
                btn.Text = "Stop";
                btn.BackColor = Color.Red;
            }
            else
            {
                _interceptor.StopCapturing();
                btn.Text = "Start";
                btn.BackColor = Color.Lime;
            }
            _capturing = !_capturing;
        }
    }

Real world example
------------------
Open.WinKeyboardHook is been used as the key component in [KeyPadawan](https://github.com/lontivero/KeyPadawan) project, a useful tool for presentation and screencasts that allow to display the shortcuts that a presenter uses.


Development
-----------
Open.WinKeyboardHook is been developed by [Lucas Ontivero](http://geeks.ms/blogs/lontivero) ([@lontivero](http://twitter.com/lontivero)). You are welcome to contribute code. You can send code both as a patch or a GitHub pull request.

 

