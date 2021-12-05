using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kiritanport.Voiceroid
{
    internal enum WordClass
    {
        普通名詞,
        固有名詞,
        人名,
        人名_姓,
        人名_名,
        地名,
        サ変名詞,
        形容動詞,
        記号
    }
    internal enum WordPriority
    {
        MAX = 1,
        HIGH = 1000,
        MID = 2000,
        LOW = 3000,
        MIN = 4000
    }

    internal class Word
    {
        public WordClass wordClass;
        public string Text
        {
            get;
            private set;
        }

        private readonly AIKanaParser kana;
        private readonly WordPriority priority;
        private static readonly Dictionary<string, WordClass> WordClassNames;

        public string Data
        {
            get
            {
                kana.GetWordDicData(out string part4, out string part5);

                //辞書逆引き
                var key = WordClassNames.First(x => x.Value == wordClass).Key;

                return key + ';' + Text + ';' + (int)priority + ';' + part4 + ';' + part5;
            }
        }

        static Word()
        {
            WordClassNames = new Dictionary<string, WordClass>
            {
                { "名詞-一般", WordClass.普通名詞 },
                { "名詞-固有名詞-一般", WordClass.固有名詞 },
                { "名詞-固有名詞-人名-一般", WordClass.人名 },
                { "名詞-固有名詞-人名-姓", WordClass.人名_姓 },
                { "名詞-固有名詞-人名-名", WordClass.人名_名 },
                { "名詞-固有名詞-地域-一般", WordClass.地名 },
                { "名詞-サ変接続", WordClass.サ変名詞 },
                { "名詞-形容動詞語幹", WordClass.形容動詞 },
                { "記号-一般", WordClass.記号 }
            };
        }

        public Word(string text, string kana, WordClass wordClass, WordPriority priority)
        {
            this.wordClass = wordClass;
            this.Text = text;
            this.kana = new AIKanaParser(kana);
            this.priority = priority;
        }

        public Word(string dic_text)
        {
            dic_text = dic_text.Remove(dic_text.Length - 2);
            var dic_data = dic_text.Split(';');

            if (WordClassNames.TryGetValue(dic_data[0], out WordClass value))
            {
                wordClass = value;
            }

            Text = dic_data[1];

            priority = (WordPriority)int.Parse(dic_data[2]);

            kana = new AIKanaParser(dic_data[3], dic_data[4]);

        }

        public string AIKana
        {
            get
            {
                return kana.ToString();
            }
        }

        public string HKana
        {
            get
            {
                return kana.HKana;
            }
        }

        public string KKana
        {
            get
            {
                return kana.KKana;
            }
        }

        public override string ToString()
        {
            //辞書逆引き
            //var key = s_wordClassList.First(x => x.Value == wordClass).Key;

            return $"{Text} : {wordClass}";
        }
    }

    internal class WordDictionary
    {
        readonly Dictionary<(string text, WordClass wclass), Word> dictionary;
        public string PathDic
        {
            get;
            private set;
        }

        public WordDictionary(string path)
        {
            dictionary = new();

            PathDic = path;

            if (File.Exists(path))
            {
                var reader = new StreamReader(path, Encoding.GetEncoding("Shift_JIS"));
                var header = reader.ReadLine();
                if (header == null)
                {
                    reader.Close();
                    reader.Dispose();
                    throw new FileFormatException();
                }
                var count_str = header[header.IndexOf("Count")..];
                var count = int.Parse(count_str.Split('\"')[1]);

                for (int i = 0; i < count; i++)
                {
                    var text = reader.ReadLine();
                    if (text == null)
                    {
                        break;
                    }
                    var word = new Word(text);
                    dictionary.Add((word.Text, word.wordClass), word);
                }

                reader.Close();
                reader.Dispose();
            }
        }

        public bool AddWord(string text, string kana, WordClass wordClass, WordPriority priority)
        {
            var result = true;

            if ((kana.StartsWith("<S>") && kana.EndsWith("<N>")) == false)
            {
                result = false;
                return result;
            }

            kana = kana.Remove(kana.Length - 3);
            kana = kana.Remove(0, 3);


            //　半角文字をすべて全角にする
            var ret = Microsoft.VisualBasic.Strings.StrConv(text, Microsoft.VisualBasic.VbStrConv.Wide, 0x411);
            if (ret == null)
            {
                result = false;
                return result;
            }
            text = ret;

            while (text.EndsWith("、"))
            {
                text = text.Remove(text.Length - 1);
            }

            Word value = new(text, kana, wordClass, priority);
            (string, WordClass) key = new(text, wordClass);

            dictionary.Remove(key);
            dictionary.Add(key, value);

            return result;
        }

        public bool RemoveWord(string text, WordClass wordClass)
        {
            var result = true;
            (string, WordClass) key = new(text, wordClass);

            if (!dictionary.Remove(key))
            {
                result = false;
            }

            return result;
        }

        public bool FindWords(string text, out List<Word> words)
        {
            var result = true;
            words = new List<Word>();

            //　半角文字をすべて全角にする
            var ret = Microsoft.VisualBasic.Strings.StrConv(text, Microsoft.VisualBasic.VbStrConv.Wide, 0x411);
            if (ret == null)
            {
                result = false;
                return result;
            }
            text = ret;

            foreach (Word word in dictionary.Values)
            {
                if (text.Contains(word.Text))
                {
                    words.Add(word);
                }
            }

            return result;
        }

        public bool FindWord(string text, WordClass wordClass, out Word? word)
        {
            var result = true;

            if (!dictionary.TryGetValue((text, wordClass), out word))
            {
                result = false;
            }

            return result;
        }

        public void Save()
        {
            Save(PathDic);
        }

        public void Save(string path)
        {
            PathDic = path;

            var writer = new StreamWriter(path, false, Encoding.GetEncoding("Shift_JIS"));

            var header = "# ComponentName=\"AITalk SDK\" ComponentVersion=\"1.0.0.1\" UpdateDateTime=\"";
            header += DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.FFF");
            header += "\" Type=\"Word\" Version=\"4.1\" Language=\"Japanese\" Dialect=\"Kansai\" Count=\"";
            header += dictionary.Count.ToString();
            header += "\"";

            writer.WriteLine(header);

            foreach (var word in dictionary)
            {
                writer.WriteLine(word.Value.Data);
            }

            writer.Flush();
            writer.Close();
            writer.Dispose();
        }
    }
}
