﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Kiritanport
{
    internal static class CustomMessageBox
    {
        static class Win32Native
        {
            [DllImport("user32.dll")]
            static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
            const int GWL_HINSTANCE = -6;

            public static IntPtr GetWindowHInstance(IntPtr hWnd) => GetWindowLong(hWnd, GWL_HINSTANCE);

            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentThreadId();

            [DllImport("user32.dll")]
            static extern IntPtr SetWindowsHookEx(int idHook, HOOKPROC lpfn, IntPtr hInstance, IntPtr threadId);
            const int WH_CBT = 5;
            public static IntPtr SetWindowsHookEx(HOOKPROC lpfn, IntPtr hInstance, IntPtr threadId) => SetWindowsHookEx(WH_CBT, lpfn, hInstance, threadId);

            [DllImport("user32.dll")]
            public static extern bool UnhookWindowsHookEx(IntPtr hHook);
            [DllImport("user32.dll")]
            public static extern IntPtr CallNextHookEx(IntPtr hHook, int nCode, IntPtr wParam, IntPtr lParam);

            public delegate IntPtr HOOKPROC(int nCode, IntPtr wParam, IntPtr lParam);
            public const int HCBT_ACTIVATE = 5;

            [DllImport("user32.dll")]
            static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
            public struct RECT
            {
                public int Left, Top, Right, Bottom;
                public int Width => this.Right - this.Left;
                public int Height => this.Bottom - this.Top;
            }

            public static RECT GetWindowRect(IntPtr hWnd)
            {
                GetWindowRect(hWnd, out RECT rc);
                return rc;
            }

            [DllImport("user32.dll")]
            static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
            const uint SWP_NOSIZE = 0x0001;
            const uint SWP_NOZORDER = 0x0004;
            const uint SWP_NOACTIVATE = 0x0010;

            public static bool SetWindowPos(IntPtr hWnd, int x, int y)
            {
                var flags = SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE;
                return SetWindowPos(hWnd, 0, x, y, 0, 0, flags);
            }
        }

        public static MessageBoxResult Show(Window owner, string text, string caption, MessageBoxButton buttons)
        {
            var helper = new WindowInteropHelper(owner);
            hOwner = helper.Handle;
            var hInstance = Win32Native.GetWindowHInstance(hOwner);
            var threadId = Win32Native.GetCurrentThreadId();
            hHook = Win32Native.SetWindowsHookEx(new Win32Native.HOOKPROC(HookProc), hInstance, threadId);
            return MessageBox.Show(text, caption, buttons);
        }

        public static MessageBoxResult Show(Window owner, string text, string caption)
        {
            var helper = new WindowInteropHelper(owner);
            hOwner = helper.Handle;
            var hInstance = Win32Native.GetWindowHInstance(hOwner);
            var threadId = Win32Native.GetCurrentThreadId();
            hHook = Win32Native.SetWindowsHookEx(new Win32Native.HOOKPROC(HookProc), hInstance, threadId);
            return MessageBox.Show(text, caption);
        }
        public static MessageBoxResult Show(Window owner, string text)
        {
            var helper = new WindowInteropHelper(owner);
            hOwner = helper.Handle;
            var hInstance = Win32Native.GetWindowHInstance(hOwner);
            var threadId = Win32Native.GetCurrentThreadId();
            hHook = Win32Native.SetWindowsHookEx(new Win32Native.HOOKPROC(HookProc), hInstance, threadId);
            return MessageBox.Show(text);
        }


        private static IntPtr hOwner = (IntPtr)0;
        private static IntPtr hHook = (IntPtr)0;
        private static IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == Win32Native.HCBT_ACTIVATE)
            {
                //　オーナーウィンドウとメッセージボックスの領域を取得
                var rcOwner = Win32Native.GetWindowRect(hOwner);
                var rcMsgBox = Win32Native.GetWindowRect(wParam);

                //　メッセージボックスをオーナーウィンドウの中央位置に移動
                var x = rcOwner.Left + (rcOwner.Width - rcMsgBox.Width) / 2;
                var y = rcOwner.Top + (rcOwner.Height - rcMsgBox.Height) / 2;
                Win32Native.SetWindowPos(wParam, x, y);

                //　フックを解除
                Win32Native.UnhookWindowsHookEx(hHook);
                hHook = (IntPtr)0;
            }
            return Win32Native.CallNextHookEx(hHook, nCode, wParam, lParam);
        }
    }
}
