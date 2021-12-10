using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Kiritanport.SubControls
{
    internal class AccentView : Grid
    {
        public new bool IsFocused => kana.IsFocused;
        public bool IsEmpty => kana.Text.Length == 0;
        public string Text { set { kana.Text = value; } get { return kana.Text; } }

        private event RoutedEventHandler Click;
        public event MyEventHandler? SelectionChanged;
        /// <summary>
        /// 内容の変更を通知するだけ
        /// </summary>
        public event MyEventHandler? KanaChanged;

        public const int MAX_LENGTH = 256;
        private readonly Button[][] controls;
        private readonly TextBox kana;
        private const int ROW_MAX = MAX_LENGTH;
        private const int COL_MAX = 3;
        private readonly List<PhraseElement> phrase = new();

        private const string START = "<S>";
        private const string PAUSE_1 = "$1_1";
        private const string PAUSE_2 = "$2_2";
        private const string BLOCK_END = "|0";
        private const string LETTER_S = "ぁぃぅぇぉゃゅょゎァィゥェォャュョヮ";
        private const string LETTER_L = "あいうえおやゆよわアイウエオヤユヨワ";

        public const string LETTER_H_KANA = "ぁあぃいぅうぇえぉおかがきぎくぐけげこごさざしじすずせぜそぞただちぢっつづてでとどなにぬねのはばぱひびぴふぶぷへべぺほぼぽまみむめもゃやゅゆょよらりるれろゎわゐゑをんヴー";
        public const string LETTER_K_KANA = "ァアィイゥウェエォオカガキギクグケゲコゴサザシジスズセゼソゾタダチヂッツヅテデトドナニヌネノハバパヒビピフブプヘベペホボポマミムメモャヤュユョヨラリルレロヮワヰヱヲンヴー";

        //　AIKANAは ヮ ヵ ヶ ヰ ヱ ヲ を使わない？
        private const string LETTER_BAD_KANA = "ゎヮヵヶゐヰゑヱをヲ";
        private const string LETTER_ALT_KANA = "わワカケいイえエおオ";

        internal class PhraseElement
        {
            //　[メモ]
            //
            //　TYPE_CHARを指定したとき、UpdateComponents 実行後
            //　Value は VALUE_UP または VALUE_DOWN のいづれかになる
            //
            public const int TYPE_UNKNOWN = 0;
            public const int TYPE_CHAR = 1 << 1;

            //　$で始まる半角文字列
            public const int TYPE_PAUSE_1 = 1 << 2;
            public const int TYPE_PAUSE_2 = 1 << 3;
            public const int TYPE_PAUSE_N = 1 << 4;

            //　|0
            public const int TYPE_BLOCK_END = 1 << 5;

            //　<> で囲まれている F,R,A,H,C
            public const int TYPE_PHRASE_END = 1 << 6;
            public const int TYPE_PHRASE_START = 1 << 7;

            public const int VALUE_U = -1;
            public const int VALUE_M = 0;
            public const int VALUE_D = 1;

            //　判別用
            public const int TYPE_PAUSE = TYPE_PAUSE_1 | TYPE_PAUSE_2 | TYPE_PAUSE_N;
            public const int TYPE_WARD_END = TYPE_PAUSE | TYPE_BLOCK_END;

            public PhraseElement(int Type, int Value, char Char)
            {
                this.Type = Type;
                this.Value = Value;
                this.Char = Char;
            }
            public PhraseElement()
            {
                Type = TYPE_UNKNOWN;
                Value = VALUE_M;
                Char = '×';
            }

            public int Type;
            public int Value;
            public char Char;
        }

        public AccentView()
        {
            kana = new TextBox()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 40, 0, 0),
                FontFamily = new FontFamily("Meiryo"),
                FontSize = 12,
                Padding = new Thickness(-3, 0, 0, 0),
                Visibility = Visibility.Collapsed,
            };

            kana.TextChanged += Kana_TextChanged;
            kana.SelectionChanged += Kana_SelectionChanged;

            Children.Add(kana);

            controls = new Button[ROW_MAX][];

            int w = 12;
            int h = 20;

            Height = 60;
            //Width = 200;

            for (int row = 0; row < ROW_MAX; row++)
            {
                controls[row] = new Button[COL_MAX];

                for (int col = 0; col < COL_MAX; col++)
                {
                    var b = controls[row][col] = new Button();

                    b.VerticalAlignment = VerticalAlignment.Top;
                    b.HorizontalAlignment = HorizontalAlignment.Left;

                    b.HorizontalContentAlignment = HorizontalAlignment.Center;
                    b.VerticalContentAlignment = VerticalAlignment.Center;

                    b.Width = w;

                    if (col == 1)
                    {
                        b.Height = h * 2;
                        b.Margin = new Thickness(row * w, 0, 0, 0);
                    }
                    else
                    {
                        b.Height = h;
                        b.Margin = new Thickness(row * w, 10 * col, 0, 0);
                    }

                    b.Padding = new Thickness(-5, 0, -5, 0);
                    b.Focusable = false;
                    b.DataContext = new Point(row, col);

                    b.Click += (sender, e) =>
                    {
                        Click?.Invoke(sender, e);
                    };

                    b.BorderBrush = Brushes.Black;
                    b.Background = Brushes.Black;

                    if (col == 1)
                    {
                        b.Foreground = Brushes.White;
                    }
                    else
                    {
                        b.Foreground = Brushes.Black;
                    }

                    b.Visibility = Visibility.Collapsed;

                    Children.Add(b);
                }
            }

            Click += PhraseEditView_Click;
        }

        private void Kana_SelectionChanged(object sender, RoutedEventArgs e)
        {
            SelectionChanged?.Invoke(sender, new MyEventArgs() { Data = SelectedAIKana });
        }

        private void Kana_TextChanged(object sender, TextChangedEventArgs e)
        {
            kana.Visibility = kana.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;

            if (kana.Text.Length == 0)
            {
                Clear();
                return;
            }

            TextChange change = e.Changes.Last();
            int row = change.Offset;
            int rem_count = change.RemovedLength;
            int add_length = change.AddedLength;
            Remove(row, rem_count);
            string str = kana.Text.Substring(row, add_length);
            Insert(row, str);
            Repaint();
        }

        private void PhraseEditView_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.DataContext is Point loc)
            {
                var row = (int)loc.X;
                //var col = (int)loc.Y;

                Toggle(row);
            }
        }
        public static bool IsZenkaku(char c)
        {
            return Encoding.GetEncoding("Shift_JIS").GetByteCount(c.ToString()) == 2;
        }

        public static bool IsHankaku(char c)
        {
            return Encoding.GetEncoding("Shift_JIS").GetByteCount(c.ToString()) == 1;
        }

        public void Repaint()
        {
            KanaChanged?.Invoke(this, new MyEventArgs());

            //　一旦消去
            for (int row = 0; row < ROW_MAX; row++)
            {
                for (int col = 0; col < COL_MAX; col++)
                {
                    if (controls[row][col].Visibility == Visibility.Collapsed)
                    {
                        continue;
                    }

                    controls[row][col].Visibility = Visibility.Collapsed;
                    controls[row][col].Background = Brushes.Black;
                    controls[row][col].Content = null;

                }
            }

            if (phrase.Count < 3)
            {
                return;
            }

            if ((phrase[^1].Type & (PhraseElement.TYPE_PHRASE_END | PhraseElement.TYPE_PAUSE_2)) == 0)
            {
                phrase.Add(new PhraseElement(PhraseElement.TYPE_PAUSE_2, PhraseElement.VALUE_M, 'P'));
            }

            for (int row = 0, col = 2, a = 0; row < MAX_LENGTH; row++)
            {

                var phrase_element_prev = phrase[row];
                var phrase_element = phrase[row + 1];

                if (phrase_element == phrase[^1])
                {
                    controls[row][1].Visibility = Visibility.Visible;
                    controls[row][1].Content = phrase_element.Char;

                    return;
                }

                var phrase_element_next = phrase[row + 2];



                if (phrase_element.Type == PhraseElement.TYPE_CHAR)
                {

                    if (phrase_element_prev.Type != PhraseElement.TYPE_CHAR)
                    {
                        if (phrase_element.Value == PhraseElement.VALUE_U)
                        {
                            a++;
                        }
                    }
                    else if (phrase_element_prev.Value != phrase_element.Value)
                    {
                        a++;
                    }


                    if (phrase_element.Value != PhraseElement.VALUE_M)
                    {
                        col = phrase_element.Value + 1;
                    }

                    controls[row][0].Visibility = Visibility.Visible;
                    controls[row][2].Visibility = Visibility.Visible;
                    controls[row][col].Background = Brushes.White;
                    controls[row][col].Content = phrase_element.Char;

                    if (a > 2)
                    {
                        controls[row][0].Background = Brushes.Gray;
                        controls[row][2].Background = Brushes.Gray;
                    }

                    continue;
                }

                controls[row][1].Visibility = Visibility.Visible;
                controls[row][1].Content = phrase_element.Char;


                if (phrase_element.Type == PhraseElement.TYPE_UNKNOWN)
                {
                    controls[row][1].Background = Brushes.Gray;
                    continue;
                }

                if (phrase_element_next.Type != PhraseElement.TYPE_CHAR)
                {
                    controls[row][1].Background = Brushes.Gray;
                    continue;
                }

                if ((phrase_element.Type & PhraseElement.TYPE_WARD_END) > 0)
                {

                    a = 0;
                    col = 2;

                    continue;
                }
            }
        }
        public void SetAIKANA(string ai_kana)
        {
            phrase.Clear();

            if (!ai_kana.StartsWith(START))
            {
                if (ai_kana.StartsWith(PAUSE_2))
                {
                    ai_kana = ai_kana[PAUSE_2.Length..];
                }
                else
                {
                    ai_kana = START + ai_kana;
                }
            }

            if (ai_kana.EndsWith(PAUSE_2))
            {
                //ai_kana = ai_kana.Remove(ai_kana.Length - PAUSE_2.Length);
                ai_kana = ai_kana[..^PAUSE_2.Length];
                ai_kana += "<N>";
            }

            for (int i = 0, value = PhraseElement.VALUE_D, a = 0; i < ai_kana.Length; i++)
            {
                var c = ai_kana[i];

                if (IsZenkaku(c))
                {
                    if (LETTER_S.Contains(c))
                    {
                        value = phrase[^1].Value;
                    }

                    phrase.Add(new PhraseElement(PhraseElement.TYPE_CHAR, value, c));
                }
                else
                {
                    //　[メモ]
                    //
                    //　AIKANA表現
                    //
                    //　$1_1 短ポーズ
                    //　$2_2 長ポーズ
                    //
                    //　<S> 開始
                    //　<F> 通常     [。]
                    //　<R> 疑問　   [？]
                    //　<A> 断定　   [！]
                    //　<H> 呼びかけ [♪]
                    //　<C> 接続 　　[、]
                    //　<N> なし 　　[　]
                    //
                    //　$1(Pau MSEC=?)    [ ? = pausetime ]
                    //　(Vol ABSLEVEL=?)  [ ? = 0.0 - 2.0 ]
                    //　(Spd ABSSPEED=?)  [ ? = 0.5 - 2.0 ]
                    //　(Pit ABSLEVEL=?)  [ ? = 0.5 - 2.0 ]
                    //　(EMPH ABSLEVEL=?) [ ? = 0.0 - 2.0 ]
                    //

                    switch (ai_kana[i])
                    {
                        case '$':
                            //switch (ai_kana.Substring(i, 4))
                            switch (ai_kana[i..(i + 4)])
                            {
                                case PAUSE_1:
                                    phrase.Add(new PhraseElement(PhraseElement.TYPE_PAUSE_1, PhraseElement.VALUE_M, 'p'));
                                    break;
                                case PAUSE_2:
                                    phrase.Add(new PhraseElement(PhraseElement.TYPE_PAUSE_2, PhraseElement.VALUE_M, 'P'));
                                    break;
                                case "$1(P":

                                    //　[メモ]
                                    //
                                    //　未実装（任意長ポーズ）
                                    //　value にポーズ時間入れてどうにか処理する？
                                    //　とりあえず、長ポーズに置き換えることで仮処理
                                    //
                                    //　処理を実装したら、GetAIKANAにも書き出し処理を追加すること
                                    //

                                    phrase.Add(new PhraseElement(PhraseElement.TYPE_PAUSE_2, 1000, 'P'));
                                    break;
                            }
                            value = PhraseElement.VALUE_D;
                            a = 0;
                            break;
                        case '|':
                            phrase.Add(new PhraseElement(PhraseElement.TYPE_BLOCK_END, PhraseElement.VALUE_M, '　'));
                            value = PhraseElement.VALUE_D;
                            a = 0;
                            break;
                        case '^':
                            value = PhraseElement.VALUE_U;
                            a++;
                            break;
                        case '!':
                            value = PhraseElement.VALUE_D;

                            if (a == 0)
                            {
                                int p = phrase.Count;

                                while (phrase[p - 1].Type == PhraseElement.TYPE_CHAR)
                                {
                                    phrase[p - 1].Value = PhraseElement.VALUE_U;

                                    p--;

                                    if (p == 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            a++;
                            break;
                        case '<':

                            if (ai_kana[i + 2] != '>')
                            {
                                throw new FormatException();
                            }

                            char tag = ai_kana[i + 1];

                            if (tag == 'S')
                            {
                                if (i > 0)
                                {
                                    throw new FormatException();
                                }
                                phrase.Add(new PhraseElement(PhraseElement.TYPE_PHRASE_START, PhraseElement.VALUE_M, 'S'));
                            }
                            else if ("FRAHCN".Contains(tag))
                            {
                                phrase.Add(new PhraseElement(PhraseElement.TYPE_PHRASE_END, PhraseElement.VALUE_M, "。？！♪、　"["FRAHCN".IndexOf(tag)]));
                                //イベントループ回避
                                kana.TextChanged -= Kana_TextChanged;
                                kana.Text = GetHKana();
                                kana.Visibility = kana.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
                                kana.TextChanged += Kana_TextChanged;
                                Repaint();
                                return;
                            }
                            break;
                    }
                }
            }
            throw new FormatException();
        }
        public string SelectedAIKana => GetAIKana(kana.SelectionStart, kana.SelectionLength);
        public string AIKana => GetAIKana(0, phrase.Count);

        private string GetAIKana(int offset, int length)
        {
            var ai_kana = START;

            for (int i = offset + 1, a = 0; i < offset + length + 1 && i < phrase.Count; i++)
            {
                var phrase_element_prev = phrase[i - 1];
                var phrase_element = phrase[i];


                if (i == offset + length)
                {
                    if (phrase_element.Type == PhraseElement.TYPE_CHAR)
                    {
                        if (phrase_element.Value != phrase_element_prev.Value && a < 2)
                        {
                            if (phrase_element.Value == PhraseElement.VALUE_U)
                            {
                                ai_kana += '^';
                            }
                            if (phrase_element.Value == PhraseElement.VALUE_D
                                && phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                            {
                                ai_kana += '!';
                            }
                        }

                        if ((phrase_element_prev.Type != PhraseElement.TYPE_CHAR)
                            && LETTER_S.Contains(phrase_element.Char))
                        {
                            ai_kana += LETTER_L[LETTER_S.IndexOf(phrase_element.Char)];
                        }
                        else
                        {
                            ai_kana += phrase_element.Char;
                        }

                        ai_kana += "<N>";
                    }
                    else
                    {
                        if (phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                        {
                            ai_kana += "<N>";
                        }
                        else
                        {
                            if (ai_kana.EndsWith(BLOCK_END))
                            {
                                ai_kana = ai_kana.Remove(ai_kana.Length - BLOCK_END.Length);
                                ai_kana += "<N>";
                            }
                            if (ai_kana.EndsWith(PAUSE_1))
                            {
                                ai_kana = ai_kana.Remove(ai_kana.Length - PAUSE_1.Length);
                                ai_kana += "<N>";
                            }
                            if (ai_kana.EndsWith(PAUSE_2))
                            {
                                ai_kana = ai_kana.Remove(ai_kana.Length - PAUSE_2.Length);
                                ai_kana += "<N>";
                            }
                        }
                    }
                    return ai_kana;

                }

                switch (phrase_element.Type)
                {
                    case PhraseElement.TYPE_CHAR:

                        //　1つのブロックにアクセントは2つまで
                        if (phrase_element.Value != phrase_element_prev.Value && a < 2)
                        {
                            if (phrase_element.Value == PhraseElement.VALUE_U)
                            {
                                ai_kana += '^';
                                a++;
                            }
                            if (phrase_element.Value == PhraseElement.VALUE_D
                                && phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                            {
                                ai_kana += '!';
                                a++;
                            }
                        }

                        if ((phrase_element_prev.Type != PhraseElement.TYPE_CHAR)
                            && LETTER_S.Contains(phrase_element.Char))
                        {
                            ai_kana += LETTER_L[LETTER_S.IndexOf(phrase_element.Char)];
                        }
                        else
                        {
                            ai_kana += phrase_element.Char;
                        }

                        break;

                    case PhraseElement.TYPE_BLOCK_END:

                        a = 0;

                        if (phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                        {
                            ai_kana += BLOCK_END;
                        }

                        break;

                    case PhraseElement.TYPE_PAUSE_1:

                        a = 0;

                        if (phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                        {
                            ai_kana += PAUSE_1;
                        }

                        break;

                    case PhraseElement.TYPE_PAUSE_2:

                        a = 0;

                        if (phrase_element_prev.Type == PhraseElement.TYPE_CHAR)
                        {
                            ai_kana += PAUSE_2;
                        }

                        break;

                    case PhraseElement.TYPE_PAUSE_N:

                        a = 0;

                        //　任意長ポーズは入力時の仮処理により長ポーズに置き換えられているため
                        //　現状、書き出しはできない
                        break;

                    case PhraseElement.TYPE_PHRASE_END:

                        if (phrase_element.Char == '。')
                        {
                            ai_kana += "<F>";
                        }
                        if (phrase_element.Char == '？')
                        {
                            ai_kana += "<R>";
                        }
                        if (phrase_element.Char == '！')
                        {
                            ai_kana += "<A>";
                        }
                        if (phrase_element.Char == '♪')
                        {
                            ai_kana += "<H>";
                        }
                        if (phrase_element.Char == '、')
                        {
                            ai_kana += "<C>";
                        }
                        if (phrase_element.Char == '　')
                        {
                            ai_kana += "<N>";
                        }

                        return ai_kana;
                }
            }

            return ai_kana;
        }

        public string GetHKana()
        {
            string result = "";

            if (phrase.Count < 2)
            {
                return result;
            }

            foreach (var elem in phrase)
            {
                if (elem.Type == PhraseElement.TYPE_CHAR)
                {
                    result += LETTER_H_KANA[LETTER_K_KANA.IndexOf(elem.Char)];
                }
                else
                {
                    result += '　';
                }
            }

            result = result[1..^1];

            return result;
        }

        public string GetKKana()
        {
            string result = "";

            if (phrase.Count < 2)
            {
                return result;
            }

            foreach (var elem in phrase)
            {
                if (elem.Type == PhraseElement.TYPE_CHAR)
                {
                    result += elem.Char;
                }
                else
                {
                    result += '　';
                }
            }

            result = result[1..^1];

            return result;
        }

        public void Insert(int row, string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                Insert(row + i, s[i]);
            }
        }

        private void Insert(int row, char c)
        {

            var phrase_element_prev = phrase[row];
            //var phrase_element = Phrase.ElementAt(row + 1);
            //var phrase_element_next = Phrase.ElementAt(row + 2);

            char kana = '　';

            if (LETTER_BAD_KANA.Contains(c))
            {
                c = LETTER_ALT_KANA[LETTER_BAD_KANA.IndexOf(c)];
            }

            if (LETTER_K_KANA.Contains(c))
            {
                kana = c;
            }
            else if (LETTER_H_KANA.Contains(c))
            {
                kana = LETTER_K_KANA[LETTER_H_KANA.IndexOf(c)];
            }

            if (kana == '　')
            {
                if (c == '　')
                {
                    phrase.Insert(row + 1, new PhraseElement(PhraseElement.TYPE_BLOCK_END, PhraseElement.VALUE_M, c));
                }
                else
                {
                    phrase.Insert(row + 1, new PhraseElement());
                }
            }
            else
            {
                if (phrase_element_prev.Type != PhraseElement.TYPE_CHAR)
                {
                    if (LETTER_S.Contains(kana))
                    {
                        kana = LETTER_L[LETTER_S.IndexOf(kana)];
                    }

                    phrase.Insert(row + 1, new PhraseElement(PhraseElement.TYPE_CHAR, PhraseElement.VALUE_D, kana));
                }
                else
                {
                    phrase.Insert(row + 1, new PhraseElement(PhraseElement.TYPE_CHAR, phrase_element_prev.Value, kana));
                }
            }
        }

        public void Remove(int row, int count)
        {
            while (count > 0)
            {
                Remove(row);
                count--;
            }
        }

        private void Remove(int row)
        {
            var phrase_element_prev = phrase[row];
            var phrase_element = phrase[row + 1];
            var phrase_element_next = phrase[row + 2];

            phrase.Remove(phrase_element);

            if (LETTER_S.Contains(phrase_element_next.Char))
            {
                if (phrase_element_prev.Type != PhraseElement.TYPE_CHAR)
                {
                    phrase_element_next.Char = LETTER_L[LETTER_S.IndexOf(phrase_element_next.Char)];
                }
                else
                {
                    phrase_element_next.Value = phrase_element_prev.Value;
                }
            }
        }

        private void Clear()
        {
            phrase.Clear();
            Repaint();
        }

        private void Toggle(int row)
        {
            var phrase_element_prev = phrase[row];
            var phrase_element = phrase[row + 1];

            if (phrase[^1] == phrase_element)
            {
                switch (phrase_element.Char)
                {

                    case '　':
                        phrase_element.Char = '。';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                    case '。':
                        phrase_element.Char = '？';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                    case '？':
                        phrase_element.Char = '！';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                    case '！':
                        phrase_element.Char = '♪';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                    case '♪':
                        phrase_element.Char = '、';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                    case '、':
                        phrase_element.Char = '　';
                        phrase_element.Type = PhraseElement.TYPE_PHRASE_END;
                        break;
                }
            }
            else
            {
                var phrase_element_next = phrase[row + 2];

                switch (phrase_element.Type)
                {
                    case PhraseElement.TYPE_PAUSE_1:
                        phrase_element.Type = PhraseElement.TYPE_PAUSE_2;
                        phrase_element.Char = 'P';
                        break;
                    case PhraseElement.TYPE_PAUSE_2:
                        phrase_element.Type = PhraseElement.TYPE_BLOCK_END;
                        phrase_element.Char = '　';
                        break;
                    case PhraseElement.TYPE_PAUSE_N:
                        phrase_element.Type = PhraseElement.TYPE_BLOCK_END;
                        phrase_element.Char = '　';
                        break;
                    case PhraseElement.TYPE_BLOCK_END:
                        phrase_element.Type = PhraseElement.TYPE_PAUSE_1;
                        phrase_element.Char = 'p';
                        break;
                    case PhraseElement.TYPE_CHAR:

                        phrase_element.Value *= -1;

                        if (LETTER_S.Contains(phrase_element.Char))
                        {
                            phrase_element_prev.Value = phrase_element.Value;
                        }

                        if (LETTER_S.Contains(phrase_element_next.Char))
                        {

                            phrase_element_next.Value = phrase_element.Value;

                        }

                        break;
                }
            }

            Repaint();
            SelectionChanged?.Invoke(this, new MyEventArgs() { Data = GetAIKana(kana.SelectionStart, kana.SelectionLength) });
        }
    }
}
