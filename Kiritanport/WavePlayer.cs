using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace Kiritanport
{
    public static class WavePlayer
    {
        private static DateTime playtime;
        static readonly SoundPlayer soundPlayer = new ();
        public static void Play(Stream stream)
        {
            stream.Position = 0;

            soundPlayer.Stop();
            soundPlayer.Stream = stream;
            soundPlayer.Load();
            soundPlayer.Play();

            WaveInfo info = new(stream);

            //44100Hz(44.1kHz) 16bit(=2byte) 1ch
            //time : wavファイルの長さ[ms]
            //var time = info.Length / 2 / 44.1;
            double time = 1000.0 * info.Length / (info.Depth / 8) / info.Sampling / info.Channel;

            playtime = DateTime.Now.AddMilliseconds(time);
        }
        public static void Stop()
        {
            soundPlayer.Stop();
        }

        public static bool IsPlaying()
        {
            return playtime > DateTime.Now;
        }

        public class WaveInfo
        {
            public int Size { get; private set; }
            public int Channel { get; private set; }
            public int Sampling { get; private set; }
            public int Depth { get; private set; }
            public int Length { get; private set; }

            public WaveInfo(Stream stream)
            {
                stream.Position = 0;

                byte[] buffer = new byte[44];
                stream.Read(buffer, 0, 44);

                Size = BitConverter.ToInt32(buffer, 4) + 8;
                Channel = BitConverter.ToInt16(buffer, 22);
                Sampling = BitConverter.ToInt32(buffer, 24);
                Depth = BitConverter.ToInt16(buffer, 34);
                Length = BitConverter.ToInt32(buffer, 40);
            }
        }
    }
}
