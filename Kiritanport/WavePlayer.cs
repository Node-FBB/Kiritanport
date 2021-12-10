using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kiritanport
{
    public static class WavePlayer
    {
        private static readonly SoundPlayer soundPlayer = new();
        private static DateTime playtime;
        private static CancellationTokenSource? ctsource;

        public static event EventHandler? PlayStopped;
        public static void Play(Stream stream)
        {
            stream.Position = 0;

            soundPlayer.Stop();
            soundPlayer.Stream = stream;
            soundPlayer.Load();
            soundPlayer.Play();

            WaveInfo info = new(stream);
            playtime = DateTime.Now + info.Time;

            if (ctsource is not null)
            {
                ctsource.Cancel();
            }
            ctsource = new CancellationTokenSource();

            SynchronizationContext? context = SynchronizationContext.Current;
            Task t = Task.Factory.StartNew(async () =>
            {
                await Task.Delay((int)Math.Ceiling(info.Time.TotalMilliseconds));
                if (ctsource.IsCancellationRequested)
                {
                    return;
                }
                context?.Post(_ => PlayStopped?.Invoke(null, new EventArgs()), null);
            }, ctsource.Token);
        }
        public static void Stop()
        {
            ctsource?.Cancel();
            soundPlayer.Stop();
            playtime = new();
            PlayStopped?.Invoke(null, new EventArgs());
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

            //1tick = 100ns > 1s = 10,000,000tick
            public TimeSpan Time => new(10_000_000L * Length / (Depth / 8) / Sampling / Channel);

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
