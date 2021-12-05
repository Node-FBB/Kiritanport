using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kiritanport.Voiceroid
{
    /// <summary>
    /// 
    /// [メモ]
    /// 
    /// AIKANA表現
    /// 
    /// $1_1 短ポーズ
    /// $2_2 長ポーズ
    /// 
    /// <S> 開始
    /// <F> 通常     [。]
    /// <R> 疑問　   [？]
    /// <A> 断定　   [！]
    /// <H> 呼びかけ [～]
    /// <C> 接続 　　[、]
    /// <N> なし 　　[　]
    /// 
    /// $1(Pau MSEC=?)    [ ? = pausetime ]
    /// (Vol ABSLEVEL=?)  [ ? = 0.0 - 2.0 ]
    /// (Spd ABSSPEED=?)  [ ? = 0.5 - 4.0 ]
    /// (Pit ABSLEVEL=?)  [ ? = 0.5 - 2.0 ]
    /// (EMPH ABSLEVEL=?) [ ? = 0.0 - 2.0 ]
    /// 
    /// </summary>
    internal class AIKanaParser
    {
        //メモ
        //small kana <-> capital kana
        //拗音　diphthongs　（じぇ、ふぁ、てぃ、しゃ　等）
        //促音　geminate consonants（きって、よっと　等の小さい「つ」））
        //促音+拗音　contracted sounds
        //濁音　voiced sounds

        private class Letter
        {
            public const string Kana_Hira = "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんヴー";
            public const string Kana_Kata = "ァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤュユョヨラリルレロヮワヰヱヲンヴー";

            //AIKanaで使わない文字
            public const string Kana_Bad = "ゎヮヵヶゐヰゑヱをヲ";
            public const string Kana_Alt = "わワカケいイえエおオ";

            //拗音は前の文字と合わせて1泊(mora)相当、促音は1拍相当
            public const string Kana_Di = "ぁぃぅぇぉゃゅょゎァィゥェォャュョヮ";//拗音
            public const string Kana_GC = "っッ";//促音
        }

        public enum Type
        {
            None,
            Kana,
            Pause,
            Tag,
        }

        public enum Accent
        {
            None,
            Up,
            Down,
        }

        public class Element
        {
            public Element(Type type, Accent accent, char value)
            {
                this.type = type;
                this.accent = accent;
                this.value = value;
            }

            public Type type = Type.None;
            public Accent accent = Accent.None;
            public char value;
        }

        public readonly List<Element> elements = new();

        /// <summary>
        /// AIKanaから生成
        /// </summary>
        /// <param name="kana">AIKana</param>
        public AIKanaParser(string kana)
        {
            var accent = Accent.None;

            for (int i = 0; i < kana.Length; i++)
            {
                var c = kana[i];

                if (GetByteCount(c) == 2)
                {
                    if (accent == Accent.None)
                    {
                        accent = Accent.Down;
                    }

                    elements.Add(new Element(Type.Kana, accent, c));
                }
                else
                {
                    switch (c)
                    {
                        case '|':
                            elements.Add(new Element(Type.Pause, Accent.None, kana.ElementAt(i + 1)));
                            accent = Accent.None;
                            break;
                        case '$':
                            elements.Add(new Element(Type.Pause, Accent.None, kana.ElementAt(i + 1)));
                            accent = Accent.None;
                            break;
                        case '<':
                            elements.Add(new Element(Type.Tag, Accent.None, kana.ElementAt(i + 1)));
                            accent = Accent.None;
                            break;
                        case '!':
                            if (accent == Accent.Down)
                            {
                                for (int x = elements.Count - 1; x > 0; x--)
                                {
                                    if (elements[x].type == Type.Kana)
                                    {
                                        elements[x].accent = Accent.Up;
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }
                            }
                            accent = Accent.Down;
                            break;
                        case '^':
                            accent = Accent.Up;
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 辞書データをセミコロンで区切った時の4番目と5番目の文字列に該当する部分から生成.
        /// 辞書データ　[1.品詞];[2.単語];[3.優先度];[4.読み];[5.イントネーション]
        /// 
        /// 拍の数え方に問題があるので要修正
        /// </summary>
        /// <param name="part4">単語辞書データ4番目の文字列</param>
        /// <param name="part5">単語辞書データ5番目の文字列</param>
        public AIKanaParser(string part4, string part5)
        {
            var start = new Element(Type.Tag, Accent.None, 'S');
            elements.Add(start);

            if (part5.EndsWith(":*"))
            {
                //part5 = part5.Remove(part5.Length - 2);
                part5 = part5[..^2];
            }

            var part5_data = part5.Split(',');

            foreach (string data in part5_data)
            {
                if (data.Equals("s1") || data.Equals("s2"))
                {
                    elements.Add(new Element(Type.Pause, Accent.None, data[1]));
                }
                else
                {
                    int[] a = new int[3];
                    for (int i = 0; i < a.Length; i++)
                    {
                        a[i] = int.Parse(data.Split('-')[i]);
                    }

                    var accent = Accent.None;


                    for (int i = 0; i < a[2]; i++)
                    {

                        if (i == (a[0] - 1))
                        {
                            accent = Accent.Up;
                        }

                        if (i == a[1])
                        {
                            accent = Accent.Down;
                        }

                        elements.Add(new Element(Type.Kana, accent, part4[0]));
                        //part4 = part4.Remove(0, 1);
                        part4 = part4[1..];

                        if (part4.Length > 0 && Letter.Kana_Di.Contains(part4[0]))
                        {
                            elements.Add(new Element(Type.Kana, accent, part4[0]));
                            //part4 = part4.Remove(0, 1);
                            part4 = part4[1..];
                        }
                    }
                }
            }

            var end = new Element(Type.Tag, Accent.None, 'N');
            elements.Add(end);

        }

        /// <summary>
        /// 辞書データをセミコロンで区切った時の4番目と5番目の文字列に該当する部分へ変換する.
        /// 辞書データ　[1.品詞];[2.単語];[3.優先度];[4.読み];[5.イントネーション]
        /// 
        /// 変換時、フレーズタグは無視され、任意長ポーズは短ポーズ扱いとなるので注意
        /// </summary>
        /// <param name="part4">単語辞書データ4番目の文字列</param>
        /// <param name="part5">単語辞書データ5番目の文字列</param>
        public void GetWordDicData(out string part4, out string part5)
        {
            part4 = "";
            part5 = "";

            int[] a = { 1, 0, 0 };
            var accent = Accent.None;

            for (int i = 0; i < elements.Count; i++)
            {
                var e = elements[i];

                if (e.type == Type.Kana)
                {
                    if (Letter.Kana_Di.Contains(e.value))
                    {
                        continue;
                    }

                    part4 += e.value;

                    if (i < elements.Count - 1)
                    {
                        var e2 = elements[i + 1];
                        if (e2.type == Type.Kana && Letter.Kana_Di.Contains(e2.value))
                        {
                            part4 += e2.value;
                        }
                    }

                    a[2]++;

                    if (e.accent != accent)
                    {
                        if (e.accent == Accent.Up)
                        {
                            a[0] = a[2];
                        }
                        else
                        {
                            a[1] = a[2] - 1;
                        }

                        accent = e.accent;
                    }
                }
                if (e.type == Type.Pause)
                {
                    if (part5.Length > 0)
                    {
                        part5 += ",";
                    }

                    //part5 += a[0] + "-" + a[1] + "-" + a[2];
                    part5 += $"{a[0]}-{a[1]}-{a[2]}";

                    if (e.value == '1' || e.value == '2')
                    {
                        if (part5.Length > 0)
                        {
                            part5 += ",";
                        }

                        part5 += $"s{e.value}";
                    }
                    else
                    {
                        if (part5.Length > 0)
                        {
                            part5 += ",";
                        }
                        //任意長ポーズは長ポーズに変換
                        part5 += "s2";
                    }

                    a[0] = 1;
                    a[1] = 0;
                    a[2] = 0;
                }
            }

            if (a[2] > 0)
            {
                if (part5.Length > 0)
                {
                    part5 += ",";
                }
                //part5 += a[0] + "-" + a[1] + "-" + a[2];
                part5 += $"{a[0]}-{a[1]}-{a[2]}";
            }

            part5 += ":*";
        }

        public string KKana
        {
            get
            {
                string ret = "";

                foreach (Element element in elements)
                {
                    if (element.type == Type.Kana)
                    {
                        ret += $"{element.value}";
                    }
                }

                return ret;
            }
        }

        public string HKana
        {
            get
            {
                string ret = "";

                foreach (Element element in elements)
                {
                    if (element.type == Type.Kana)
                    {
                        ret += $"{Letter.Kana_Hira.ElementAt(Letter.Kana_Kata.IndexOf(element.value))}";
                    }
                }

                return ret;
            }
        }

        /// <summary>
        /// 全角/半角を調べる
        /// </summary>
        /// <param name="c"></param>
        /// <returns>全角なら2、半角なら1</returns>
        public static int GetByteCount(char c)
        {
            return Encoding.GetEncoding("Shift_JIS").GetByteCount(c.ToString());
        }


        /// <summary>
        /// 文字列がAIKANAかどうかを判別する（簡易版）
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static bool IsAIKANA_lite(string text)
        {
            if (!text.StartsWith("<S>"))
            {
                return false;
            }
            if (!text.EndsWith("<F>") || !text.EndsWith("<R>") || !text.EndsWith("<A>") || !text.EndsWith("<H>") || !text.EndsWith("<C>") || !text.EndsWith("<N>"))
            {
                return false;
            }

            return true;
        }

        public override string ToString()
        {
            string ret = "";
            var e_prev = elements[0];

            for (int i = 0; i < elements.Count; i++)
            {
                var e_curr = elements[i];
                switch (e_curr.type)
                {
                    case Type.Kana:
                        if (e_prev.accent != e_curr.accent)
                        {
                            if (e_curr.accent == Accent.Up)
                            {
                                ret += "^";
                            }
                            else
                            {
                                ret += "!";
                            }
                        }
                        ret += e_curr.value;
                        break;
                    case Type.Pause:
                        switch (e_curr.value)
                        {
                            case '0':
                                ret += "|0";
                                break;
                            case '1':
                                ret += "$1_1";
                                break;
                            case '2':
                                ret += "$2_2";
                                break;
                        }
                        break;
                    case Type.Tag:
                        ret += $"<{e_curr.value}>";
                        break;
                }

                e_prev = e_curr;
            }

            return ret;
        }
    }
}
