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

namespace Kiritanport
{
    internal class SpeechData
    {
        public string Text = "";
        public TDialect Dialect;
    }

    internal enum TAccentProvider
    {
        Default,
        Voiceroid,
        Voicevox,
    }
    internal class PhraseListView : ListBox
    {
        private bool processing = false;

        //単語辞書用のイベント
        public event MyEventHandler? OnSpeech;
        public event MyEventHandler? TextSelected;
        public event MyEventHandler? KanaSelected;

        //発話中のUIロック用
        public event MyEventHandler? SpeechBegin;
        public event MyEventHandler? SpeechEnd;

        private event MyEventHandler? HidePhraseEditViewSignal;

        public TAccentProvider AccentProvider { set; get; } = TAccentProvider.Default;

        public bool AccentLock = false;

        public PhraseListView()
        {
            SelectionChanged += CustomListBox_SelectionChanged;
        }
        private void CustomListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source == this)
            {
                HidePhraseEditViewSignal?.Invoke(this, new MyEventArgs());
            }
        }
        public void Speech(string text)
        {
            if (SelectedItem is not PhraseView item)
            {
                return;
            }

            if (item.Presets.SelectedItem is ComboBoxItem citem
                && citem.Content is VoicePreset preset)
            {

                string param = "";
                param += $"voice:{preset.VoiceName} ";
                param += $"vol:{preset.Volume} ";
                param += $"spd:{preset.Speed} ";
                param += $"pit:{preset.Pitch} ";
                param += $"emph:{preset.PitchRange} ";
                param += $"dialect:{preset.Dialect}";

                APIManager.Param(citem.DataContext, param);
                APIManager.WaveReceived += APIManager_WaveReceived;
                APIManager.Speech(citem.DataContext, text);

                SpeechBegin?.Invoke(this, new MyEventArgs());
            }
            else
            {
                processing = false;
            }
        }

        private void SpeechPhrase(PhraseView item)
        {
            received_item = item;

            item.IsSelected = true;

            if (item.Presets.SelectedItem is ComboBoxItem citem
                && citem.Content is VoicePreset preset)
            {
                SpeechData speech = new();

                string param = "";
                param += $"voice:{preset.VoiceName} ";
                param += $"vol:{preset.Volume} ";
                param += $"spd:{preset.Speed} ";
                param += $"pit:{preset.Pitch} ";
                param += $"emph:{preset.PitchRange} ";
                param += $"dialect:{preset.Dialect}";

                APIManager.Param(citem.DataContext, param);

                speech.Dialect = preset.Dialect;

                APIManager.WaveReceived += APIManager_WaveReceived;
                APIManager.KanaReceived += MainWindow_KanaReceived;

                string text = item.Text.Text;
                string kana = item.Kana.AIKana;

                if (item.Kana.IsFocused && item.Kana.SelectedAIKana is not "<S>" and string selected)
                {
                    kana = selected;
                }

                if (kana != "<S>")
                {
                    APIManager.Speech(citem.DataContext, kana);
                }
                else
                {
                    switch (AccentProvider)
                    {
                        case TAccentProvider.Voiceroid:
                            prev_speech = true;
                            APIManager.Kana(APIManager.VoiceroidAPI, text);
                            break;
                        case TAccentProvider.Voicevox:
                            prev_speech = true;
                            APIManager.Kana(APIManager.VoicevoxAPI, text);
                            break;
                        default:
                            prev_speech = false;
                            APIManager.Speech(citem.DataContext, text);
                            break;
                    }

                }
                speech.Text = text;
                OnSpeech?.Invoke(this, new MyEventArgs() { Data = speech });
                SpeechBegin?.Invoke(this, new MyEventArgs());
            }
            else
            {
                processing = false;
            }
        }

        /// <summary>
        /// フレーズリストを上から順にすべて読み上げる
        /// </summary>
        public void SpeechPhraseList()
        {
            SynchronizationContext? context = SynchronizationContext.Current;

            Task.Factory.StartNew(async () =>
            {
                foreach (PhraseView item in Items)
                {
                    processing = true;

                    context?.Send(_ => SpeechPhrase(item), null);

                    while (processing)
                    {
                        await Task.Delay(10);
                    }
                }
            });
        }

        private bool prev_speech = false;
        private PhraseView? received_item;
        private void MainWindow_KanaReceived(object sender, MyEventArgs e)
        {
            APIManager.KanaReceived -= MainWindow_KanaReceived;

            if (received_item is not null && e.Data is string text && text.Length > 0)
            {
                received_item.Kana.SetAIKANA(text);
                if (prev_speech)
                {
                    APIManager.Speech(received_item.Presets.DataContext, text);
                }
            }

            received_item = null;
            prev_speech = false;
        }
        private void APIManager_WaveReceived(object sender, MyEventArgs e)
        {
            APIManager.WaveReceived -= APIManager_WaveReceived;
            SynchronizationContext? context = SynchronizationContext.Current;

            if (e.Data is MemoryStream stream)
            {
                WavePlayer.Play(stream);

                Task.Factory.StartNew(async () =>
                {
                    while (WavePlayer.IsPlaying())
                    {
                        await Task.Delay(10);
                    }
                    processing = false;
                    context?.Send(_ => SpeechEnd?.Invoke(this, new MyEventArgs()), null);
                });
            }
        }

        private readonly List<(VoicePreset preset, Binding binding)> bindlist = new();
        /// <summary>
        /// フレーズに設定されているプリセットリストに一括でプリセットを追加する
        /// </summary>
        /// <param name="preset"></param>
        /// <param name="binding"></param>
        public void AddPreset(VoicePreset preset, Binding binding)
        {
            bindlist.Add((preset, binding));

            foreach (PhraseView item in Items)
            {
                ComboBoxItem item_dst = new() { Content = preset };
                item_dst.SetBinding(DataContextProperty, binding);

                item.Presets.Items.Add(item_dst);
            }
        }
        /// <summary>
        /// フレーズに設定されているプリセット名を一括で変更する
        /// </summary>
        /// <param name="index"></param>
        public void RenamePreset(int index)
        {
            foreach (PhraseView item in Items)
            {
                if (item.Presets.Items[index] is ComboBoxItem citem)
                {
                    var content = citem.Content;
                    citem.Content = null;
                    citem.Content = content;
                }

                if (index == item.Presets.SelectedIndex)
                {
                    item.Presets.SelectedIndex = -1;
                    item.Presets.SelectedIndex = index;
                }
            }
        }
        /// <summary>
        /// フレーズリストにフレーズを追加する
        /// </summary>
        /// <param name="focus"></param>
        public void AddPhraseLine(bool focus)
        {
            PhraseView phrase = new();

            phrase.Text.SelectionChanged += (sender, e) =>
            {
                TextSelected?.Invoke(this, new MyEventArgs() { Data = phrase.Text.SelectedText });
            };

            phrase.Kana.SelectionChanged += (sender, e) =>
            {
                if (e.Data is string text)
                {
                    if (text.StartsWith("<S>") && text.EndsWith("<N>"))
                    {
                        KanaSelected?.Invoke(this, new MyEventArgs() { Data = text[3..^3] });
                    }
                    else
                    {
                        KanaSelected?.Invoke(this, new MyEventArgs() { Data = "" });
                    }
                }
            };

            HidePhraseEditViewSignal += (sender, e) =>
            {
                phrase.Kana.Visibility = Visibility.Collapsed;
            };

            foreach ((VoicePreset preset, Binding binding) in bindlist)
            {
                ComboBoxItem cbitem = new() { Content = preset };
                cbitem.SetBinding(DataContextProperty, binding);
                phrase.Presets.Items.Add(cbitem);
            }
            if (Parent is Grid parent)
            {
                parent.Children.Add(phrase.Kana);
            }
            if (focus)
            {
                phrase.Text.Loaded += (sender, e) =>
                {
                    phrase.Text.Focus();
                };
            }

            Items.Add(phrase);
        }

        private class PhraseView : ListBoxItem
        {
            // メモ
            // Content = grid
            // grid.children[0] > presets
            // grid.children[1] > text
            // item.DataContext = kana
            // parent(root_grid).view
            // presets.item.datacontext は cb.item.content(VoicePreset) の voiceName に対応するAPI(Process)にバインドされている

            public TextBox Text { get; init; } = default!;
            public PhraseEditView Kana { get; init; } = default!;
            public ComboBox Presets { get; init; } = default!;

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
                    if (Parent is PhraseListView parent)
                    {
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
                    if (e.Key == Key.Escape)
                    {
                        Kana.Text = "";
                    }

                    if (e.Key == Key.Enter)
                    {
                        if (Parent is PhraseListView parent)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                            {
                                parent.AddPhraseLine(true);

                            }
                            else
                            {
                                parent.SpeechPhrase(this);
                            }
                        }
                    }
                };

                Text.KeyDown += (sender, e) =>
                {
                    if (e.Key == Key.Escape)
                    {
                        Kana.Text = "";
                    }

                    if (e.Key == Key.Enter)
                    {
                        if (Parent is PhraseListView parent)
                        {
                            if (Keyboard.Modifiers == ModifierKeys.Shift)
                            {
                                parent.AddPhraseLine(true);

                            }
                            else
                            {
                                parent.SpeechPhrase(this);
                            }
                        }
                    }
                    if (e.Key == Key.Tab)
                    {
                        e.Handled = true;

                        if (Kana.Visibility == Visibility.Collapsed)
                        {
                            if (Parent is PhraseListView parent && parent.Parent is Grid g)
                            {
                                Point p = Text.TranslatePoint(new Point(0, 0), g);
                                Kana.Margin = new Thickness(p.X, p.Y + Text.ActualHeight, 0, 0);
                                Kana.Visibility = Visibility.Visible;
                            }
                        }
                        else
                        {
                            Kana.Visibility = Visibility.Collapsed;
                        }
                    }
                };
                grid.Children.Add(Presets);
                grid.Children.Add(Text);
            }
        }
    }
}
