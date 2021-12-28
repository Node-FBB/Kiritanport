using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritanport.Voiceroid
{
    public struct Style
    {
        public string Name { set; get; }
        public float Value { set; get; }
    }

    public enum TDialect
    {
        Standard,
        Kansai
    }

    public enum TType
    {
        Normal,
        User
    }

    public class VoicePreset : ICloneable
    {
        public string PresetName { set; get; } = "";
        public TType Type { set; get; }
        public TDialect Dialect { set; get; }

        public string VoiceName { set; get; } = "";
        public float Volume { set; get; } = 1;
        public float Speed { set; get; } = 1;
        public float Pitch { set; get; } = 1;
        public float PitchRange { set; get; } = 1;
        public int MiddlePause { set; get; } = 150;
        public int LongPause { set; get; } = 370;
        public Style[] Styles { set; get; } = Array.Empty<Style>();

        public override string ToString()
        {
            return PresetName;
        }

        public object Clone()
        {
            VoicePreset clone = new VoicePreset()
            {
                PresetName = PresetName,
                Type = Type,
                Dialect = Dialect,
                VoiceName = VoiceName,
                Volume = Volume,
                Speed = Speed,
                Pitch = Pitch,
                PitchRange = PitchRange,
                MiddlePause = MiddlePause,
                LongPause = LongPause,
                Styles = new Style[Styles.Length]
            };

            Array.Copy(Styles, clone.Styles, Styles.Length);

            return clone;
        }
    }
}
