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

        public static Process? VoiceroidAPI => IsInit && !apis["VR"].HasExited ? apis["VR"] : null;
        public static Process? VoicevoxAPI => IsInit && !apis["VV"].HasExited ? apis["VV"] : null;
        public static Process? AssistantSeikaAPI => IsInit && !apis["AS"].HasExited ? apis["AS"] : null;
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

        public static string? GetKey(object? api)
        {
            if (api is Process process)
            {
                if (apis.ContainsValue(process))
                {
                    return apis.First(x => x.Value == process).Key;
                }
            }
            if (api is string key)
            {
                if (apis.ContainsKey(key))
                {
                    return key;
                }
            }
            return null;
        }

        public static void Init()
        {
            if (IsInit)
            {
                Exit();
                Task.Delay(100);
            }

            apis["VR"] = new Process();
            apis["VV"] = new Process();
            apis["AS"] = new Process();


            apis["VR"].StartInfo.FileName = @"..\..\..\..\CLIVoiceroid\bin\Debug\CLIVoiceroid.exe";
            apis["VV"].StartInfo.FileName = @"..\..\..\..\CLIVoicevox\bin\Debug\net6.0\CLIVoicevox.exe";
            apis["AS"].StartInfo.FileName = @"..\..\..\..\CLIAssistantSeika\bin\Debug\CLIAssistantSeika.exe";

            StreamReader reader = new(@".\settings.ini");
            while (reader.ReadLine() is string str)
            {
                if (str.StartsWith("DirVoiceroid="))
                {
                    string path = str["DirVoiceroid=".Length..];

                    if (Directory.Exists(path))
                    {
                        apis["VR"].StartInfo.Arguments = $"\"{path}\"";
                    }
                }
            }

            /*
            if (File.Exists(@".\APIs\VOICEROID2\VoiceroidAPI.exe"))
            {
                apis["VR"].StartInfo.FileName = @".\APIs\VOICEROID2\VoiceroidAPI.exe";
            }
            else
            {
                apis["VR"].StartInfo.FileName = @"..\..\..\..\VoiceroidAPI\bin\Debug\VoiceroidAPI.exe";
            }
            */
            /*
            if (File.Exists(@".\APIs\VOICEVOX\VoicevoxAPI.exe"))
            {
                apis["VV"].StartInfo.FileName = @".\APIs\VOICEVOX\VoicevoxAPI.exe";
            }
            else
            {
                apis["VV"].StartInfo.FileName = @"..\..\..\..\VoicevoxAPI\bin\Debug\net6.0\VoicevoxAPI.exe";
            }
            */



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

                if (GetKey(sender) is string key)
                {
#if DEBUG
                    File.AppendAllText(@".\api_log.txt", $"{key}:{e.Data}\n");
#endif
                    Log += $"{key}:{e.Data}\n";
                }
                MessageReceived?.Invoke(sender, new MyEventArgs() { Data = e.Data });

                if (process?.HasExited == true)
                {
                    return;
                }

                if (e.Data.StartsWith("wave>"))
                {
                    string name = e.Data["wave>".Length..];

                    MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(name);
                    Wave wave = new();
                    mmf.CreateViewStream().CopyTo(wave);
                    WaveReceived?.Invoke(sender, new MyEventArgs() { Data = wave });

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
            else if (api is string name)
            {
                apis[name]?.StandardInput.WriteLine("param<" + text);
            }
        }
        public static void Speech(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("speech<" + text);
            }
            else if (api is string key)
            {
                apis[key]?.StandardInput.WriteLine("speech<" + text);
            }
        }
        public static void Dictionary(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("dictionary<" + text);
            }
            else if (api is string key)
            {
                apis[key]?.StandardInput.WriteLine("dictionary<" + text);
            }
        }
        public static void DictionaryKansai(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("dictionary_kansai<" + text);
            }
            else if (api is string key)
            {
                apis[key]?.StandardInput.WriteLine("dictionary_kansai<" + text);
            }
        }
        public static void Kana(object? api, string text)
        {
            if (api is Process process)
            {
                process.StandardInput.WriteLine("kana<" + text);
            }
            else if (api is string key)
            {
                apis[key]?.StandardInput.WriteLine("kana<" + text);
            }
        }

        public static void Cancel()
        {
            apis["VV"]?.StandardInput.WriteLine("cancel");
        }
    }
}
