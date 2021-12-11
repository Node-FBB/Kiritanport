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
    internal static class WavePlayer
    {
        private static readonly SoundPlayer soundPlayer = new();
        private static DateTime playtime;
        private static CancellationTokenSource? ctsource;

        public static event EventHandler? PlayStopped;
        public static void Play(Wave wave)
        {
            wave.Position = 0;

            soundPlayer.Stop();
            soundPlayer.Stream = wave;
            soundPlayer.Load();
            soundPlayer.Play();

            playtime = DateTime.Now + wave.PlayTime;

            if (ctsource is not null)
            {
                ctsource.Cancel();
            }
            ctsource = new CancellationTokenSource();

            SynchronizationContext? context = SynchronizationContext.Current;
            Task.Factory.StartNew(async () =>
            {
                await Task.Delay((int)Math.Ceiling(wave.PlayTime.TotalMilliseconds));
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
    }
}
