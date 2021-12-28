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
    internal class PhraseEditView : ListBoxItem
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
                if (Parent is PhraseList parent && parent.Parent is Grid root && root.Parent is MainWindow main)
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
                if (Parent is PhraseList parent)
                {
                    return parent.Items.IndexOf(this);
                }
                return -1;
            }
        }

        public TextBox Text { get; init; } = default!;
        public AccentEditView Kana { get; init; } = default!;
        public ComboBox Presets { get; init; } = default!;
        public CheckBox Check { get; init; } = default!;
        public bool IsFocusOnLoadead { get; set; } = false;
        public ListBox SearchResult { get; init; } = default!;

        /// <summary>
        /// クリア条件
        /// Kanaの内容が変更された時
        /// </summary>
        public Wave? Wave;

        public bool AccentVisible
        {
            get
            {
                return Kana.IsVisible;
            }
            set
            {
                if (Parent is not PhraseList parent)
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

        public bool SearchResultVisible
        {
            get
            {
                return SearchResult.IsVisible;
            }
            set
            {
                if (Parent is not PhraseList parent)
                {
                    return;
                }
                if (value)
                {
                    if (SearchResult.Items.Count > 0 && parent.Parent is Grid root)
                    {
                        Point p = Text.TranslatePoint(new Point(0, 0), root);
                        SearchResult.Margin = new Thickness(p.X, p.Y + Text.ActualHeight, 0, 0);
                        SearchResult.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    SearchResult.Visibility = Visibility.Collapsed;
                }
            }
        }

        public PhraseEditView()
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
                Focusable = false,
                IsChecked = true,
            };
            Kana = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                DataContext = this,
            };

            SearchResult = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = Visibility.Collapsed,
                Focusable = false,
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
            Unselected += (sender, e) =>
            {
                SearchResultVisible = false;
            };
            Loaded += (sender, e) =>
            {
                if (IsFocusOnLoadead)
                {
                    Text.Focus();
                }
            };

            Text.PreviewKeyDown += Text_KeyDown;
            Text.KeyDown += Text_KeyDown;

            Text.GotFocus += (sender, e) =>
            {
                if (Parent is PhraseList parent && parent.SelectedItem != this)
                {
                    parent.SelectedItem = this;
                }
            };
            Text.TextChanged += (sender, e) =>
            {
                if (Parent is PhraseList parent)
                {
                    if (parent.AccentLock)
                    {
                        if (Text.Text == "")
                        {
                            if (!Kana.IsEmpty)
                            {
                                Kana.Text = "";
                                Notice(Brushes.LightGray);
                            }
                            if (SearchResultVisible)
                            {
                                SearchPhraseDictionary();
                            }
                        }
                    }
                    else
                    {
                        if (!Kana.IsEmpty)
                        {
                            Kana.Text = "";
                            Notice(Brushes.LightGray);
                        }
                        if (SearchResultVisible)
                        {
                            SearchPhraseDictionary();
                        }
                    }
                }
            };

            Kana.KeyDown += (sender, e) =>
            {
                if (Parent is not PhraseList parent)
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
                SearchResultVisible = false;
            };
            SearchResult.SelectionChanged += SearchResult_SelectionChanged;

            Base.Children.Add(Presets);
            Base.Children.Add(Text);
            Base.Children.Add(Check);
        }

        CancellationTokenSource? source = null;
        public void Notice(Brush color)
        {
            Notice(color, 100);
        }
        public void Notice(Brush color,int time_ms)
        {
            source?.Cancel();
            source = new CancellationTokenSource();

            var token = source.Token;

            Text.Background = color;
            SynchronizationContext? context = SynchronizationContext.Current;

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(time_ms);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                context?.Post(_ => Text.Background = Brushes.White, null);
            }, token);
        }

        private void SearchResult_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResult.SelectedItem is not string text)
            {
                return;
            }

            if (Main is MainWindow main)
            {
                if (Preset is VoicePreset preset)
                {
                    if (preset.Dialect == TDialect.Standard)
                    {
                        if (main.pdic_standard?.GetPhraseKana(text) is string kana)
                        {
                            Text.Text = text;

                            kana = kana["$2_2".Length..];
                            if (kana.EndsWith("$2_2"))
                            {
                                kana = kana[..^"$2_2".Length] + "<N>";
                            }

                            Kana.SetAIKANA(kana);
                            Notice(Brushes.LightBlue);
                        }
                    }
                }
            }

            SearchResult.SelectedIndex = -1;
        }

        public void Refresh()
        {
            Wave = null;
            Text.Text = "";
            Kana.Text = "";
            Check.IsChecked = true;
            Presets.SelectedIndex = -1;
        }

        public void SearchPhraseDictionary()
        {
            string text = Text.Text;

            if (Main is MainWindow main)
            {
                if (Preset is VoicePreset preset)
                {
                    if (preset.Dialect == TDialect.Standard)
                    {
                        if (main.pdic_standard?.FindPhrase(text, out List<string> phrases) == true)
                        {
                            if(text.Length == 0)
                            {
                                phrases.Reverse();
                            }


                            SearchResult.ItemsSource = phrases;
                            SearchResultVisible = true;
                        }
                    }
                }
            }
        }

        private void Text_KeyDown(object sender, KeyEventArgs e)
        {
            if (Parent is not PhraseList parent)
            {
                return;
            }

            e.Handled = true;

            switch (e.Key)
            {
                case Key.Escape:
                    if (SearchResultVisible)
                    {
                        SearchResultVisible = false;
                        break;
                    }
                    if (AccentVisible)
                    {
                        AccentVisible = false;
                        break;
                    }
                    if (!Kana.IsEmpty)
                    {
                        Kana.Text = "";
                        Notice(Brushes.LightGray);
                    }
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
                            if (parent.Items[Index - 1] is PhraseEditView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                        else
                        {
                            if (parent.Items[^1] is PhraseEditView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                    }
                    else
                    {
                        if (Index < parent.Items.Count - 1)
                        {
                            if (parent.Items[Index + 1] is PhraseEditView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                        else
                        {
                            if (parent.Items[0] is PhraseEditView dst)
                            {
                                dst.Text.Focus();
                            }
                        }
                    }
                    break;
                case Key.Space:
                    if (Keyboard.Modifiers == ModifierKeys.Control)
                    {
                        if (Kana.IsEmpty)
                        {
                            if (SearchResultVisible)
                            {
                                SearchResultVisible = false;
                            }
                            else
                            {
                                SearchPhraseDictionary();
                            }
                        }
                        else
                        {
                            if (Kana.Visibility == Visibility.Collapsed)
                            {
                                AccentVisible = true;
                            }
                            else
                            {
                                AccentVisible = false;
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
                        if (parent.Items[Index + 1] is PhraseEditView phrase)
                        {
                            phrase.Text.Focus();
                        }
                    }
                    break;
                case Key.Up:
                    if (Index > 0)
                    {
                        if (parent.Items[Index - 1] is PhraseEditView phrase)
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
