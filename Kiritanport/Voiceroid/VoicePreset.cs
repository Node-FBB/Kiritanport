using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritanport.Voiceroid
{
    public struct Style
    {
        public string Name;
        public float Value;
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
        public string PresetName = "";
        public TType Type;
        public TDialect Dialect;

        public string VoiceName = "";
        public float Volume;
        public float Speed;
        public float Pitch;
        public float PitchRange;
        public int MiddlePause;
        public int LongPause;
        public Style[] Styles = Array.Empty<Style>();

        public override string ToString()
        {
            return PresetName;
        }

        public object Clone()
        {
            VoicePreset clone = new()
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
