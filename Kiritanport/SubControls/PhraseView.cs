using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Kiritanport.SubControls
{
    internal class PhraseView : ListBoxItem
    {
        // メモ
        // Content = grid
        // grid.children[0] > presets
        // grid.children[1] > text
        // item.DataContext = kana
        // parent(root_grid).view
        // presets.item.datacontext は cb.item.content(VoicePreset) の voiceName に対応するAPI(Process)にバインドされている

        private Grid Base { get; init; } = default!;
        private MainWindow? Main
        {
            get
            {
                if (Parent is PhraseListView parent && parent.Parent is Grid root && root.Parent is MainWindow main)
                {
                    return main;
                }
                return null;
            }
        }
        public VoicePreset? Preset
        {
            get
            {
                if (Presets.SelectedItem is ComboBoxItem src && src.Content is VoicePreset preset)
                {
                    return preset;
                }
                return null;
            }
        }
        private int Index
        {
            get
            {
                if (Parent is PhraseListView parent)
                {
                    return parent.Items.IndexOf(this);
                }
                return -1;
            }
        }

        public TextBox Text { get; init; } = default!;
        public AccentView Kana { get; init; } = default!;
        public ComboBox Presets { get; init; } = default!;
        public CheckBox Check { get; init; } = default!;

        /// <summary>
        /// クリア条件
        /// Kanaの内容が変更された時
        /// </summary>
        public Wave? Wave;

        public bool IsAccentVisible
        {
            get
            {
                return Kana.IsVisible;
            }
            set
            {
                if (Parent is not PhraseListView parent)
                {
                    return;
                }
                if (value)
                {
                    if (!Kana.IsEmpty && parent.Parent is Grid root)
                    {
                        Point p = Text.TranslatePoint(new Point(0, 0), root);
                        Kana.Margin = new Thickness(p.X, p.Y + Text.ActualHeight, 0, 0);
                        Kana.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    Kana.Visibility = Visibility.Collapsed;
                }
            }
        }
        public PhraseView()
        {
            Base = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            Presets = new()
            {
                //Margin = new Thickness(30, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 100,
                Focusable = false,
            };
            Text = new()
            {
                Margin = new Thickness(100, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            Check = new()
            {
                Width = 20,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, -5, 0),
            };
            Kana = new()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed,
                DataContext = this,
            };

            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Content = Base;
            Focusable = false;
            DataContext = Kana;

            Selected += (sender, e) =>
            {
                Text.Focus();
            };

            Text.PreviewKeyDown += Text_KeyDown;
            Text.KeyDown += Text_KeyDown;

            Text.GotFocus += (sender, e) =>
            {
                if (Parent is PhraseListView parent && parent.SelectedItem != this)
                {
                    parent.SelectedItem = this;
                }
            };
            Text.TextChanged += (sender, e) =>
            {
                if (Parent is PhraseListView parent)
                {
                    if (parent.AccentLock)
                    {
                        if (Text.Text == "")
                        {
                            Kana.Text = "";
                        }
                    }
                    else
                    {
                        Kana.Text = "";
                    }
                }
            };

            Kana.KeyDown += (sender, e) =>
            {
                if (Parent is not PhraseListView parent)
                {
                    return;
                }

                if (e.Key == Key.Escape)
                {
                    Kana.Text = "";
                }

                if (e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        parent.AddPhraseLine(true);

                    }
                    else
                    {
                        parent.SpeechSelected();
                    }
                }
            };
            Kana.KanaChanged += (sender, e) =>
            {
                Wave = null;
            };

            Base.Children.Add(Presets);
            Base.Children.Add(Text);
            Base.Children.Add(Check);
        }

        private void Text_KeyDown(object sender, KeyEventArgs e)
        {
            if (Parent is not PhraseListView parent)
            {
                return;
            }

            e.Handled = true;

            switch (e.Key)
            {
                case Key.Escape:
                    Kana.Text = "";
                    break;

                case Key.Enter:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        parent.InsertPhraseLine(true);
                    }
                    else
                    {
                        parent.SpeechSelected();
                        Check.IsChecked = true;
                    }
                    break;
                case Key.Tab:
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        if (Index > 0)
                        {
                            if (parent.Items[Index - 1] is PhraseView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                        else
                        {
                            if (parent.Items[^1] is PhraseView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                    }
                    else
                    {
                        if (Index < parent.Items.Count - 1)
                        {
                            if (parent.Items[Index + 1] is PhraseView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                        else
                        {
                            if (parent.Items[0] is PhraseView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                    }
                    break;
                case Key.Space:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (Kana.IsEmpty && Text.Text.Length > 0)
                        {
#if DEBUG
#warning フレーズ辞書検索実装予定地
#endif
                            string text = Text.Text;

                            if (Main is MainWindow main)
                            {
                                if (Preset is VoicePreset preset)
                                {
                                    if (preset.Dialect == TDialect.Standard)
                                    {
                                        if (main.pdic_standard?.FindPhrase(text, out List<string> phrases) == true)
                                        {
                                            string str = "";
                                            foreach (string p in phrases)
                                            {
                                                str += p + "\n";
                                            }
                                            MessageBox.Show(str);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (Kana.Visibility == Visibility.Collapsed)
                            {
                                IsAccentVisible = true;
                            }
                            else
                            {
                                IsAccentVisible = false;
                            }
                        }
                    }
                    else
                    {
                        e.Handled = false;
                    }
                    break;
                case Key.Down:
                    if (Index < parent.Items.Count - 1)
                    {
                        if (parent.Items[Index + 1] is PhraseView phrase)
                        {
                            phrase.Text.Focus();
                        }
                    }
                    break;
                case Key.Up:
                    if (Index > 0)
                    {
                        if (parent.Items[Index - 1] is PhraseView phrase)
                        {
                            phrase.Text.Focus();
                        }
                    }
                    break;
                default:
                    e.Handled = false;
                    break;
            }
        }
    }
}
