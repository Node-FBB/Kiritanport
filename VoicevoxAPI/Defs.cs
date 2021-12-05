using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VoicevoxAPI
{
    public class AudioQuery
    {
        public AccentPhrase[] accent_phrases { set; get; } = Array.Empty<AccentPhrase>();
        public double speedScale { set; get; } = 1.0;
        public double pitchScale { set; get; } = 0.0;
        public double intonationScale { set; get; } = 1.0;
        public double volumeScale { set; get; } = 1.0;
        public double prePhonemeLength { set; get; } = 0.1;
        public double postPhonemeLength { set; get; } = 0.1;
        public int outputSamplingRate { set; get; } = 24000;
        public bool outputStereo { set; get; } = false;
        /// <summary>
        /// 全てのカナはカタカナで記述される
        /// アクセント句は/または、で区切る。、で区切った場合に限り無音区間が挿入される。
        /// カナの手前に_を入れるとそのカナは無声化される
        /// アクセント位置を'で指定する。全てのアクセント句にはアクセント位置を 1 つ指定する必要がある。
        /// </summary>
        public string? kana
        {
            set
            {
                _ = value;
            }
            get
            {
                if (accent_phrases is null)
                {
                    return null;
                }
                string ret = "";

                foreach (AccentPhrase phrase in accent_phrases)
                {
                    if (phrase.moras is null)
                    {
                        continue;
                    }
                    for (int i = 0; i < phrase.moras.Length; i++)
                    {
                        if (phrase.accent == i)
                        {
                            ret += "'";
                        }

                        if (phrase.moras[i].text is null)
                        {
                            continue;
                        }
                        ret += phrase.moras[i].text;
                    }
                    if (phrase.accent == phrase.moras.Length)
                    {
                        ret += "'";
                    }
                    if (phrase.pause_mora is null)
                    {
                        if (phrase != accent_phrases[^1])
                        {
                            ret += "/";
                        }
                        continue;
                    }
                    ret += phrase.pause_mora.text;
                }
                return ret;
            }
        }
    }


    public class AccentPhrase
    {
        public Mora[] moras { set; get; } = Array.Empty<Mora>();
        public int accent { set; get; } = 0;
        public Mora? pause_mora { set; get; }
    }

    public class Mora
    {
        public string text { set; get; } = "";
        public string? consonant { set; get; }
        public double? consonant_length { set; get; }
        public string vowel { set; get; } = "";
        public double vowel_length { set; get; } = 0.0;
        public double pitch { set; get; } = 0.0;
    }

    public class Speaker
    {
        public string? name { set; get; }
        public string? speaker_uuid { set; get; }
        public Style[]? styles { set; get; }
        public string? version { set; get; }
    }

    public class Style
    {
        public string? name { set; get; }
        public int? id { set; get; }
    }

    internal static class KanaConvarter
    {
        public const string Diphthong = "ァィゥェォャュョヮ";//diphthong　二重母音の判別用

        public const string Vowel_a = "アァカガサザタダナハバパマヤャラワヮ";
        public const string Vowel_i = "イィキギシジチヂニヒビピミリ";
        public const string Vowel_u = "ウゥクグスズツヅヌフブプムユュルヴ";
        public const string Vowel_e = "エェケゲセゼテデネヘベペメレ";
        public const string Vowel_o = "オォコゴソゾトドノホボポモヨョロ";

        internal static string AIKanaToAqKana(string aikana)
        {
            aikana = aikana[3..^3];

            aikana = aikana.Replace("!", "'");
            aikana = aikana.Replace("|0", "/");
            aikana = aikana.Replace("$1_1", "、");
            aikana = aikana.Replace("$2_2", "、");
            aikana = aikana.Replace("^", "");

            bool flag = false;
            string aqkana = "";

            int a = 0;

            for (int i = 0; i < aikana.Length; i++)
            {
                if (aikana[i] == '/' || aikana[i] == '、')
                {
                    if (flag)
                    {
                        flag = false;
                        a = i + 1;
                    }
                    else
                    {
                        aqkana += "'";
                    }
                }

                if (aikana[i] == '\'')
                {
                    if (i == a)
                    {
                        continue;
                    }

                    flag = true;
                }

                if (aikana[i] == 'D')
                {
                    aqkana = aqkana[..^1] + "_" + aqkana[^1];
                }
                else
                {
                    aqkana += aikana[i];
                }
            }
            if (!flag)
            {
                aqkana += "'";
            }

            for (int i = 0; i < aqkana.Length; i++)
            {
                if (aqkana[i] == 'ー')
                {
                    int cnt = 0;

                    while (true)
                    {
                        if (Vowel_a.Contains(aqkana[i - 1 - cnt]))
                        {
                            aqkana = $"{aqkana[..i]}ア{aqkana[(i + 1)..]}";
                            break;
                        }
                        else if (Vowel_i.Contains(aqkana[i - 1 - cnt]))
                        {
                            aqkana = $"{aqkana[..i]}イ{aqkana[(i + 1)..]}";
                            break;
                        }
                        else if (Vowel_u.Contains(aqkana[i - 1 - cnt]))
                        {
                            aqkana = $"{aqkana[..i]}ウ{aqkana[(i + 1)..]}";
                            break;
                        }
                        else if (Vowel_e.Contains(aqkana[i - 1 - cnt]))
                        {
                            aqkana = $"{aqkana[..i]}エ{aqkana[(i + 1)..]}";
                            break;
                        }
                        else if (Vowel_o.Contains(aqkana[i - 1 - cnt]))
                        {
                            aqkana = $"{aqkana[..i]}オ{aqkana[(i + 1)..]}";
                            break;
                        }
                        else if (aqkana[i - 1 - cnt] == 'ン')
                        {
                            aqkana = $"{aqkana[..i]}ン{aqkana[(i + 1)..]}";
                            break;
                        }
                        cnt++;
                    }
                }
            }

            return aqkana;
        }
        internal static string AqKanaToAIKana(string aqkana)
        {
            string aikana = "";

            int a = 0;

            for (int i = 0; i < aqkana.Length; i++)
            {
                switch (aqkana[i])
                {
                    case '\'':
                        if (aikana.Length == a + 1)
                        {
                            aikana = $"{aikana[..(a)]}^{aikana[(a)..]}!";
                        }
                        else
                        {
                            if (Diphthong.Contains(aikana[a + 1]))
                            {
                                aikana = $"{aikana[..(a)]}^{aikana[(a)..]}!";
                            }
                            else
                            {
                                aikana = $"{aikana[..(a + 1)]}^{aikana[(a + 1)..]}!";
                            }
                        }
                        break;

                    case '/':
                        if (aikana[^1] == '!')
                        {
                            aikana = aikana[..^1];
                        }
                        aikana += "|0";
                        a = aikana.Length;
                        break;
                    case '、':
                        if (aikana[^1] == '!')
                        {
                            aikana = aikana[..^1];
                        }
                        aikana += "$2_2";
                        a = aikana.Length;
                        break;
                    case '_':
                        aikana += aqkana[i + 1] + "D";
                        i++;
                        break;
                    default:
                        aikana += aqkana[i];
                        break;
                }
            }

            return "<S>" + aikana + "<N>";
        }
    }
}
