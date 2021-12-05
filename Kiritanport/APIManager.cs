using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kiritanport
{
    internal class MyEventArgs
    {
        public object? Data;
    }

    internal delegate void MyEventHandler(object sender, MyEventArgs e);
    internal static class APIManager
    {
        internal static event MyEventHandler? WaveReceived;
        internal static event MyEventHandler? KanaReceived;
        internal static event MyEventHandler? MessageReceived;

        private static readonly Dictionary<string, Process> apis = new();
        public static bool IsInit { get; private set; } = false;

        public static Process? VoiceroidAPI => IsInit ? apis["voiceroid"] : null;
        public static Process? VoicevoxAPI => IsInit ? apis["voicevox"] : null;
        public static string Log { get; private set; } = "";

        public static int RunningAPIsCount
        {
            get
            {
                int cnt = 0;
                foreach (Process process in apis.Values)
                {
                    if (process?.HasExited == false)
                    {
                        cnt++;
                    }
                }
                return cnt;
            }
        }

        public static int APIsCount => apis.Count;

        public static void Init()
        {
            if (IsInit)
            {
                Exit();
                Task.Delay(100);
            }

            apis["voiceroid"] = new Process();
            apis["voicevox"] = new Process();

            if (File.Exists(@".\APIs\VOICEROID2\VoiceroidAPI.exe"))
            {
                apis["voiceroid"].StartInfo.FileName = @".\APIs\VOICEROID2\VoiceroidAPI.exe";
            }
            else
            {
                apis["voiceroid"].StartInfo.FileName = @"..\..\..\..\VoiceroidAPI\bin\Debug\VoiceroidAPI.exe";
            }


            StreamReader reader = new(@".\settings.ini");
            while (reader.ReadLine() is string str)
            {
                if (str.StartsWith("DirVoiceroid="))
                {
                    string path = str["DirVoiceroid=".Length..];

                    if (Directory.Exists(path))
                    {
                        apis["voiceroid"].StartInfo.Arguments = $"\"{path}\"";
                    }
                }
            }


            if (File.Exists(@".\APIs\VOICEVOX\VoicevoxAPI.exe"))
            {
                apis["voicevox"].StartInfo.FileName = @".\APIs\VOICEVOX\VoicevoxAPI.exe";
            }
            else
            {
                apis["voicevox"].StartInfo.FileName = @"..\..\..\..\VoicevoxAPI\bin\Debug\net6.0\VoicevoxAPI.exe";
            }


            SynchronizationContext? syncContext = SynchronizationContext.Current;

            foreach (Process process in apis.Values)
            {
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.RedirectStandardInput = true;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.UseShellExecute = false;

                process.OutputDataReceived += (sender, e) =>
                {
                    syncContext?.Send(_ => DataReceived(sender, e), null);
                };

                process.Start();
                process.BeginOutputReadLine();
            }

            IsInit = true;
        }
        private static void DataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null && sender is Process process)
            {
#if DEBUG
                File.AppendAllText(@".\api_log.txt", $"{process.ProcessName}:{e.Data}\n");
#endif
                Log += $"{process.ProcessName}:{e.Data}\n";

                MessageReceived?.Invoke(sender, new MyEventArgs() { Data = e.Data });

                if (e.Data.StartsWith("wave>"))
                {
                    string name = e.Data["wave>".Length..];

                    MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name);
                    MemoryStream stream = new();
                    mmf.CreateViewStream().CopyTo(stream);
                    WaveReceived?.Invoke(sender, new MyEventArgs() { Data = stream });

                    mmf.Dispose();
                    process?.StandardInput.WriteLine("clear<" + name);
                }

                if (e.Data.StartsWith("aikana>"))
                {
                    string kana = e.Data["aikana>".Length..];
                    KanaReceived?.Invoke(sender, new MyEventArgs() { Data = kana });
                }
            }
        }

        public static void Exit()
        {
            foreach (Process process in apis.Values)
            {
                process?.StandardInput.WriteLine("exit");
                process?.Dispose();
            }
        }
        public static void Param(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("param<" + text);
            }
        }
        public static void Speech(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("speech<" + text);
            }
        }
        public static void Dictionary(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("dictionary<" + text);
            }
        }
        public static void DictionaryKansai(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("dictionary_kansai<" + text);
            }
        }
        public static void Kana(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("kana<" + text);
            }
        }
    }
}
