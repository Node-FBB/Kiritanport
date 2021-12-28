using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kiritanport.Voiceroid
{
    internal class Phrase
    {
        private readonly string text;
        private readonly AIKanaParser kana;

        public Phrase(string text, string kana)
        {
            this.text = text;
            this.kana = new AIKanaParser(kana);
        }

        public string Text
        {
            get
            {
                return text;
            }
        }

        public string AIKana
        {
            get
            {
                return kana.ToString();
            }
        }
    }
    internal class PhraseDictionary
    {
        readonly Dictionary<string, Phrase> dictionary;
        public string PathDic
        {
            get;
            private set;
        }

        public PhraseDictionary(string path)
        {
            dictionary = new Dictionary<string, Phrase>();

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
                    _ = reader.ReadLine();
                    var text = reader.ReadLine();
                    var kana = reader.ReadLine();
                    if(text == null || kana == null)
                    {
                        break;
                    }

                    var phrase = new Phrase(text, kana);

                    dictionary.Add(text, phrase);
                }

                reader.Close();
                reader.Dispose();
            }
        }

        public bool AddPhrase(string text, string kana)
        {
            var result = true;

            if (kana.StartsWith("<S>") == false)
            {
                result = false;
            }
            kana = kana.Replace("<S>", "$2_2");
            kana = kana.Replace("<C>", "$2_2");
            kana = kana.Replace("<N>", "$2_2");


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

            dictionary.Remove(text);
            dictionary.Add(text, new Phrase(text, kana));

            return result;
        }

        public bool RemovePhrase(string text)
        {
            var result = true;
            
            //　半角文字をすべて全角にする
            var ret = Microsoft.VisualBasic.Strings.StrConv(text, Microsoft.VisualBasic.VbStrConv.Wide, 0x411);
            if(ret == null)
            {
                result = false;
                return result;
            }
            text = ret;
            while (text.EndsWith("、"))
            {
                text = text.Remove(text.Length - 1);
            }

            if (!dictionary.Remove(text))
            {
                result = false;
            }

            return result;
        }

        public bool FindPhrase(string text, out List<string> phrases)
        {
            var result = true;

            phrases = new List<string>();

            foreach (string phrase in dictionary.Keys)
            {
                if (phrase.StartsWith(text))
                {
                    phrases.Add(phrase);
                }

            }

            if (phrases.Count == 0)
            {
                result = false;
            }

            return result;
        }

        public string? GetPhraseKana(string text)
        {
            if (dictionary.ContainsKey(text))
            {
                return dictionary[text].AIKana;
            }
            return null;
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
            header += "\" Type=\"Phrase\" Version=\"3.3\" Language=\"Japanese\" Dialect=\"Kansai\" Count=\"";
            header += dictionary.Count.ToString();
            header += "\"";

            writer.WriteLine(header);

            for (int i = 0; i < dictionary.Count; i++)
            {
                writer.WriteLine("num:" + i);
                writer.WriteLine(dictionary.ElementAt(i).Value.Text);
                writer.WriteLine(dictionary.ElementAt(i).Value.AIKana);
            }

            writer.Flush();
            writer.Close();
            writer.Dispose();
        }
    }
}
