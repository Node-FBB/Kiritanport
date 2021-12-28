using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Kiritanport.AssistantSeika
{
    [ServiceContract(SessionMode = SessionMode.Required)]
    interface IScAPIs
    {
        [OperationContract]
        string Verson();

        [OperationContract]
        Dictionary<int, string> AvatorList();

        [OperationContract]
        Dictionary<int, Dictionary<string, string>> AvatorList2();

        [OperationContract]
        Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> GetDefaultParams2(int cid);

        [OperationContract]
        Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> GetCurrentParams2(int cid);

        [OperationContract]
        void ResetParams2(int cid);

        [OperationContract]
        double Talk(int cid, string talktext, string filepath, Dictionary<string, decimal> effects, Dictionary<string, decimal> emotions);

        [OperationContract]
        double Talk2(int cid, string[] talktexts, string filepath, Dictionary<string, decimal> effects, Dictionary<string, decimal> emotions);

        [OperationContract]
        void TalkAsync(int cid, string talktext, Dictionary<string, decimal> effects, Dictionary<string, decimal> emotions);

        [OperationContract]
        void TalkAsync2(int cid, string[] talktexts, Dictionary<string, decimal> effects, Dictionary<string, decimal> emotions);

    }
    internal static class AssistantSeikaEngine
    {
        private static IScAPIs api = null;
        private static int cid = 0;

        public static void Init()
        {
            ChannelFactory<IScAPIs> factory = new ChannelFactory<IScAPIs>(new NetNamedPipeBinding() { }, new EndpointAddress("net.pipe://localhost/EchoSeika/CentralGate/ApiEntry"));
            while (factory.State != CommunicationState.Created)
            {
                Thread.Sleep(10);
            }
            api = factory.CreateChannel();
            (api as IContextChannel).OperationTimeout = new TimeSpan(24, 0, 0);
            while (factory.State != CommunicationState.Opened)
            {
                Thread.Sleep(10);
            }

            Console.WriteLine(api.Verson());

            foreach (var speaker in api.AvatorList())
            {
                var param = api.GetDefaultParams2(speaker.Key);

                VoicePreset preset = new VoicePreset()
                {
                    VoiceName = $"{speaker.Key}",
                    PresetName = speaker.Value,
                    Styles = new Style[param["emotion"].Count],
                };

                int cnt = 0;
                foreach (string key in param["emotion"].Keys)
                {
                    float value = (float)param["emotion"][key]["value"];
                    preset.Styles[cnt] = new Style { Name = key, Value = value };
                }


                Console.WriteLine($"voice>{JsonSerializer.Serialize(preset)}");
            }
        }

        static Dictionary<string, Dictionary<string, Dictionary<string, decimal>>> parameters;
        public static void SetParam(string param_str)
        {
            VoicePreset preset = JsonSerializer.Deserialize<VoicePreset>(param_str);

            cid = int.Parse(preset.VoiceName);
            parameters = api.GetDefaultParams2(cid);

            ApplyEffect("volume", preset.Volume);
            ApplyEffect("speed", preset.Speed);
            ApplyEffect("pitch", preset.Pitch);
            ApplyEffect("intonation", preset.PitchRange);

            foreach (Style emo in preset.Styles)
            {
                ApplyEmotion(emo.Name, emo.Value);
            }
        }

        private static void ApplyEmotion(string name, float value)
        {
            if (parameters["effect"].ContainsKey(name))
            {
                decimal min = parameters["effect"][name]["min"];
                decimal max = parameters["effect"][name]["max"];
                //decimal val = parameters["effect"][name]["value"];
                decimal dst = (decimal)((float)max * value);

                if (dst < min)
                {
                    dst = min;
                }
                if (dst > max)
                {
                    dst = max;
                }

                parameters["effect"][name]["value"] = dst;
            }
        }

        private static void ApplyEffect(string name, float value)
        {
            if (parameters["effect"].ContainsKey(name))
            {
                decimal min = parameters["effect"][name]["min"];
                decimal max = parameters["effect"][name]["max"];
                decimal val = parameters["effect"][name]["value"];
                decimal dst = (decimal)((float)val * value);

                if (dst < min)
                {
                    dst = min;
                }
                if (dst > max)
                {
                    dst = max;
                }

                parameters["effect"][name]["value"] = dst;
            }
        }
        public static void TextToSpeech(string text)
        {
            long id = DateTime.Now.Ticks;

            Console.WriteLine($"process<{id}");

            Dictionary<string, decimal> effects = new Dictionary<string, decimal>();

            if (parameters != null)
            {
                if (parameters["effect"].ContainsKey("volume"))
                {
                    effects["volume"] = parameters["effect"]["volume"]["value"];
                }
                if (parameters["effect"].ContainsKey("speed"))
                {
                    effects["speed"] = parameters["effect"]["speed"]["value"];
                }
                if (parameters["effect"].ContainsKey("pitch"))
                {
                    effects["pitch"] = parameters["effect"]["pitch"]["value"];
                }
                if (parameters["effect"].ContainsKey("intonation"))
                {
                    effects["intonation"] = parameters["effect"]["intonation"]["value"];
                }
            }

            api.Talk(cid, text, Path.GetFullPath(@".\tmp.wav"), effects, null);

            FileStream stream = new FileStream(@".\tmp.wav", FileMode.Open);
            MemoryStream wavData = new MemoryStream((int)stream.Length);
            stream.CopyTo(wavData);
            stream.Close();
            stream.Dispose();

            //File.Delete(@".\tmp.wav");

            string mmf_name = $"assistant_seika_{id}";
            MemoryMappedFile mmf = MemoryMappedFile.CreateNew(mmf_name, wavData.Length);

            wavData.Position = 0;
            wavData.CopyTo(mmf.CreateViewStream());

            Console.WriteLine($"wave>{mmf_name}");
            Console.WriteLine($"process>{id}");
        }
    }
}
