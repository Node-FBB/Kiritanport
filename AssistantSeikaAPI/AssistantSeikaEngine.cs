using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssistantSeikaAPI
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
                Console.WriteLine($"voice>{speaker.Key}:{speaker.Value}");
            }
        }

        public static void SetParam(string param_str)
        {
            foreach (string str in param_str.Split(' '))
            {
                if (str.StartsWith("voice:"))
                {
                    if (int.TryParse(str.Split(':')[1], out int result))
                    {
                        cid = result;
                    }
                }
                //パラメータの設定が未対応
                /*
                if (str.StartsWith("vol:"))
                {
                    float volume = float.Parse(str.Split(':')[1]);
                }
                if (str.StartsWith("spd:"))
                {
                    float speed = float.Parse(str.Split(':')[1]);
                }
                if (str.StartsWith("pit:"))
                {
                    float pitch = float.Parse(str.Split(':')[1]);
                }
                if (str.StartsWith("emph:"))
                {
                    float range = float.Parse(str.Split(':')[1]);
                }
                */
            }
        }
        public static void TextToSpeech(string text)
        {
            long id = DateTime.Now.Ticks;

            Console.WriteLine($"process<{id}");

            api.Talk(cid, text, Path.GetFullPath(@".\tmp.wav"), null, null);

            FileStream stream = new FileStream(@".\tmp.wav", FileMode.Open);
            MemoryStream wavData = new MemoryStream((int)stream.Length);
            stream.CopyTo(wavData);
            stream.Close();
            stream.Dispose();

            File.Delete(@".\tmp.wav");

            string mmf_name = $"assistant_seika_{id}";
            MemoryMappedFile mmf = MemoryMappedFile.CreateNew(mmf_name, wavData.Length);

            wavData.Position = 0;
            wavData.CopyTo(mmf.CreateViewStream());

            Console.WriteLine($"wave>{mmf_name}");
            Console.WriteLine($"process>{id}");
        }
    }
}
