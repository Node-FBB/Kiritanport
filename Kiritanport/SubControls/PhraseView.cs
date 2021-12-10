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

        public TextBox Text { get; init; } = default!;
        public AccentView Kana { get; init; } = default!;
        public ComboBox Presets { get; init; } = default!;

        /// <summary>
        /// クリア条件
        /// Kanaの内容が変更された時
        /// </summary>
        public MemoryStream? Wave;

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
            Kana = new()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };
            Grid grid = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            Content = grid;
            Focusable = false;
            DataContext = Kana;

            Presets = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 100,
                Focusable = false,
            };
            Text = new()
            {
                Margin = new Thickness(100, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            Kana.DataContext = this;

            Selected += (sender, e) =>
            {
                Text.Focus();
            };
            Text.GotFocus += (sender, e) =>
            {
                if (Parent is PhraseListView parent && parent.SelectedItem != this)
                {
                    parent.SelectedItem = this;
                }
            };
            Text.PreviewKeyDown += (sender, e) =>
            {
                if (Parent is not PhraseListView parent)
                {
                    return;
                }

                if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    e.Handled = true;

                    if (Kana.IsEmpty && Text.Text.Length > 0)
                    {
#if DEBUG
#warning フレーズ辞書検索実装予定地
#endif
                        string text = Text.Text;

                        if (parent.Parent is Grid root && root.Parent is MainWindow main)
                        {
                            if (Presets.SelectedItem is ComboBoxItem src && src.Content is VoicePreset preset)
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
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    int index = parent.Items.IndexOf(this);

                    if (index < parent.Items.Count - 1)
                    {
                        if (parent.Items[index + 1] is PhraseView litem)
                        {
                            litem.Text.Focus();
                        }
                    }
                }
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    int index = parent.Items.IndexOf(this);

                    if (index > 0)
                    {
                        if (parent.Items[index - 1] is PhraseView litem)
                        {
                            litem.Text.Focus();
                        }
                    }
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

            Text.KeyDown += (sender, e) =>
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
                        parent.InsertPhraseLine(true);

                    }
                    else
                    {
                        parent.SpeechSelected();
                    }
                }
                if (e.Key == Key.Tab)
                {
                    e.Handled = true;
                    int index = parent.Items.IndexOf(this);

                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        if (index > 0)
                        {
                            if (parent.Items[index - 1] is PhraseView dst)
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
                        if (index < parent.Items.Count - 1)
                        {
                            if (parent.Items[index + 1] is PhraseView dst)
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
                }
            };
            grid.Children.Add(Presets);
            grid.Children.Add(Text);
        }
    }
}
