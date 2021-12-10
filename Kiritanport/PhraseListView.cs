using Kiritanport.SubControls;
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
        bool notice = false;
        public void SpeechSelected()
        {
            if (SelectedItem is PhraseView sel)
            {
                notice = true;
                SpeechPhrase(sel);
            }
        }

        private void SpeechPhrase(PhraseView item)
        {
            if (item.Text.Text.Length == 0)
            {
                return;
            }

            if (item.Presets.SelectedItem is ComboBoxItem citem
                && citem.Content is VoicePreset preset)
            {
                SpeechBegin?.Invoke(this, new MyEventArgs());
                processing = true;
                received_item = item;
                item.IsSelected = true;
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
            }
        }
        private bool SpeechPhraseFromCache(PhraseView item)
        {
            if (item.Wave is null)
            {
                return false;
            }

            processing = true;
            item.IsSelected = true;
            SpeechBegin?.Invoke(this, new MyEventArgs());

            Task.Factory.StartNew(async () =>
            {
                WavePlayer.Play(item.Wave);
                while (WavePlayer.IsPlaying())
                {
                    await Task.Delay(10);
                }
                processing = false;
            });

            return true;
        }

        public void SpeechPhraseList(bool PrioritizeCache)
        {
            ctsource = new CancellationTokenSource();
            SynchronizationContext? context = SynchronizationContext.Current;

            Task.Factory.StartNew(async () =>
            {
                foreach (PhraseView item in Items)
                {
                    if (ctsource.Token.IsCancellationRequested)
                    {
                        break;
                    }
                    int silence;
                    string text = "";

                    context?.Send(_ => text = item.Text.Text, null);

                    if ("。！？♪".Contains(text[^1]))
                    {
                        silence = 800;
                    }
                    else if ('、' == text[^1])
                    {
                        silence = 370;
                    }
                    else
                    {
                        silence = 150;
                    }

                    bool cached = false;

                    if (PrioritizeCache)
                    {
                        context?.Send(_ => cached = SpeechPhraseFromCache(item), null);
                    }
                    if (!cached)
                    {
                        context?.Send(_ => SpeechPhrase(item), null);
                    }

                    while (processing)
                    {
                        await Task.Delay(10);
                    }
                    await Task.Delay(silence);
                }
                context?.Send(_ => SpeechEnd?.Invoke(this, new MyEventArgs()), null);
            }, ctsource.Token);
        }
        private CancellationTokenSource? ctsource;
        public void SpeechCancel()
        {
            if (processing)
            {
                ctsource?.Cancel();
                APIManager.KanaReceived -= MainWindow_KanaReceived;
                APIManager.WaveReceived -= APIManager_WaveReceived;
                APIManager.Cancel();
                WavePlayer.Stop();
                processing = false;
                received_item = null;
                prev_speech = false;

                SpeechEnd?.Invoke(this, new MyEventArgs());
            }
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
                    APIManager.Speech((received_item.Presets.SelectedItem as ComboBoxItem)?.DataContext, text);
                }
            }

            prev_speech = false;
        }
        private void APIManager_WaveReceived(object sender, MyEventArgs e)
        {
            APIManager.WaveReceived -= APIManager_WaveReceived;
            SynchronizationContext? context = SynchronizationContext.Current;

            if (e.Data is MemoryStream stream)
            {
                if (received_item is not null)
                {
                    received_item.Wave = stream;
                }

                WavePlayer.Play(stream);

                Task.Factory.StartNew(async () =>
                {
                    while (WavePlayer.IsPlaying())
                    {
                        await Task.Delay(10);
                    }
                    processing = false;
                    if (notice)
                    {
                        context?.Post(_ => SpeechEnd?.Invoke(this, new MyEventArgs()), null);
                        notice = false;
                    }
                });
            }
            received_item = null;
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
                ComboBoxItem item_dst = new() { Content = preset, Focusable = false };
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
            PhraseView dst = CreatePhraseView(focus);
            if (Items.Count > 0)
            {
                if (Items[^1] is PhraseView src)
                {
                    dst.Presets.SelectedIndex = src.Presets.SelectedIndex;
                }
            }
            Items.Add(dst);
            ScrollIntoView(dst);
        }
        public void RemovePhraseLine(bool focus)
        {
            if (SelectedItem is not null)
            {
                SelectedIndex--;
                if (focus)
                {
                    if (SelectedItem is PhraseView phrase)
                    {
                        phrase.Text.Focus();
                    }
                }
                Items.Remove(Items[SelectedIndex + 1]);
            }
        }
        public void InsertPhraseLine(bool focus)
        {
            PhraseView dst = CreatePhraseView(focus);
            if (SelectedItem is PhraseView src)
            {
                dst.Presets.SelectedIndex = src.Presets.SelectedIndex;
            }
            Items.Insert(SelectedIndex + 1, dst);
            ScrollIntoView(dst);
        }

        public void EditPhraseLine()
        {
            if (SelectedItem is PhraseView src)
            {
                if (src.IsAccentVisible)
                {
                    src.IsAccentVisible = false;
                }
                else
                {
                    src.IsAccentVisible = true;
                }
            }
        }
        private PhraseView CreatePhraseView(bool focus)
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
                ComboBoxItem cbitem = new() { Content = preset, Focusable = false };
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

            return phrase;
        }

    }
}
