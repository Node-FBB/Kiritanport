using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AssistantSeikaAPI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("version>0.0.0");
            Console.WriteLine("Initializing ...");
            try
            {
                AssistantSeikaEngine.Init();
            }
            catch (Exception e)
            {
                Console.WriteLine($"error>{e.Message}");
                Console.WriteLine("Exit.");
                return;
            }
            Console.WriteLine("Ready.");

            string read_line;
            while ((read_line = Console.ReadLine()) != null)
            {
                string cmd = read_line.Split('<')[0];

                switch (cmd)
                {
                    case "exit":
                        return;

                    case "speech":
                        string text = read_line.Substring("speech<".Length);
                        AssistantSeikaEngine.TextToSpeech(text);
                        break;

                    case "param":
                        string param_str = read_line.Substring("param<".Length);
                        AssistantSeikaEngine.SetParam(param_str);
                        break;
                }
            }
        }
    }
}
