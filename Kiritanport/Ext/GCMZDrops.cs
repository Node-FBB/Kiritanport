using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Kiritanport.Ext
{
    internal static class GCMZDrops
    {
        private static class Win32Native
        {
            public const Int32 MAX_PATH = 260;

            [StructLayout(LayoutKind.Sequential)]
            public struct COPYDATASTRUCT
            {
                public IntPtr dwData;
                public Int32 cbData;
                public IntPtr lpData;
            }

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr SendMessage(IntPtr hWnd, Int32 Message, IntPtr wParam, ref COPYDATASTRUCT lParam);
            public const Int32 WM_COPYDATA = 0x004A;

            [DllImport("user32.dll", CharSet = CharSet.Unicode)]
            public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 4)]
        private class GCMZDropsData
        {
            public UInt32 Window;
            public Int32 Width;
            public Int32 Height;
            public Int32 VideoRate;
            public Int32 VideoScale;
            public Int32 AudioRate;
            public Int32 AudioCh;
            public Int32 GCMZAPIVer;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = Win32Native.MAX_PATH)]
            public string? ProjectPath;
        }

        private static GCMZDropsData? Init()
        {
            if (!IsEnable)
            {
                return null;
            }

            try
            {
                using Mutex mut = Mutex.OpenExisting("GCMZDropsMutex");
                try
                {
                    using MemoryMappedFile mmf = MemoryMappedFile.OpenExisting("GCMZDrops");
                    mut.WaitOne();

                    MemoryMappedViewStream? stream = mmf.CreateViewStream();

                    byte[] buf = new byte[Marshal.SizeOf<GCMZDropsData>()];
                    stream.Read(buf, 0, Marshal.SizeOf<GCMZDropsData>());

                    mut.ReleaseMutex();

                    IntPtr ptr = Marshal.AllocCoTaskMem(Marshal.SizeOf<GCMZDropsData>());
                    Marshal.Copy(buf, 0, ptr, Marshal.SizeOf<GCMZDropsData>());

                    GCMZDropsData data = new();
                    Marshal.PtrToStructure(ptr, data);

                    Marshal.FreeCoTaskMem(ptr);

                    return data;
                }
                catch (FileNotFoundException)
                {
                    return null;
                }
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                return null;
            }
        }
        private static void SendMessage(IntPtr dst, byte[] mes)
        {
            Win32Native.COPYDATASTRUCT data = new()
            {
                dwData = (IntPtr)1,
                cbData = mes.Length,
                lpData = Marshal.AllocHGlobal(mes.Length)
            };

            Marshal.Copy(mes, 0, data.lpData, mes.Length);

            Win32Native.SendMessage(dst, Win32Native.WM_COPYDATA, IntPtr.Zero, ref data);

            Marshal.FreeHGlobal(data.lpData);
        }

        //　相対パスを絶対パスにしつつ、JSONエスケープ処理のため\を\\にする
        private static string ToJSONPath(string path)
        {
            return Path.GetFullPath(path).Replace(@"\", @"\\");
        }
        //　Unicode文字列をUTF8エンコードに変換する
        private static byte[] ToUTF8(string uni_str)
        {
            byte[] uni_bytes = Encoding.Unicode.GetBytes(uni_str);
            byte[] utf8_bytes = Encoding.Convert(Encoding.Unicode, Encoding.UTF8, uni_bytes);
            return utf8_bytes;
        }
        public static string? ProjectPath
        {
            get
            {
                return Init()?.ProjectPath;
            }
        }
        public static bool IsEnable { set; get; } = true;
        public static bool IsUsable => Init()?.Width > 0;//nullとの比較は常にfalseになる
        public static bool SendFiles(List<string> files, double time_ms, int layer)
        {
            GCMZDropsData? data = Init();

            if (data == null || data.Width == 0)
            {
                return false;
            }

            //　[メモ]
            //
            //　JSON を UTF-8 エンコーディングで渡します。
            //　layer:
            //　　ドロップするレイヤーを決めます。
            //　　指定を省略することはできません。
            //　　　-1 ～ -100
            //　　拡張編集上での現在の表示位置からの相対位置へ挿入
            //　　例: 縦スクロールによって一番上に見えるレイヤーが Layer 3 のとき、-1 を指定すると Layer 3、-2 を指定すると Layer 4 へ挿入
            //　　　1 ～  100
            //　　スクロール位置に関わらず指定したレイヤー番号へ挿入
            //　frameAdvance:
            //　　ファイルのドロップした後、指定されたフレーム数だけカーソルを先に進めます。
            //　　進める必要がない場合は省略可能です。
            //　files:
            //　　投げ込むファイルへのフルパスを配列で渡します。
            //　　ファイル名は UTF-8 にする必要がありますが、拡張編集の仕様上 ShiftJIS の範囲内の文字しか扱えません。
            //

            var str1 = "";
            var str2 = "";
            foreach (string path in files)
            {
                if (path.EndsWith(".wav") || path.EndsWith(".txt"))
                {
                    if (str1.Length > 0)
                    {
                        str1 += ",";
                    }

                    str1 += "\"" + ToJSONPath(path) + "\"";
                }
                else if (path.EndsWith(".lab"))
                {
                    if (str2.Length > 0)
                    {
                        str2 += ",";
                    }

                    str2 += "\"" + ToJSONPath(path) + "\"";
                }
            }


            int length = (int)(time_ms / 1000 * data.VideoRate + 1);
            string mes;

            if (str2.Length > 0)
            {
                mes = "";
                mes += "{";
                mes += "\"layer\":" + layer + ",";
                mes += "\"files\":[" + str2 + "]";
                mes += "}";

                SendMessage((IntPtr)data.Window, ToUTF8(mes));

                mes = "";
                mes += "{";
                mes += "\"layer\":" + (layer + 1) + ",";
                mes += "\"frameAdvance\":" + length + ",";
                mes += "\"files\":[" + str1 + "]";
                mes += "}";

                SendMessage((IntPtr)data.Window, ToUTF8(mes));
            }
            else
            {
                mes = "";
                mes += "{";
                mes += "\"layer\":" + layer + ",";
                mes += "\"frameAdvance\":" + length + ",";
                mes += "\"files\":[" + str1 + "]";
                mes += "}";

                SendMessage((IntPtr)data.Window, ToUTF8(mes));
            }

            return true;
        }
    }
}
