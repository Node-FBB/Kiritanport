using Kiritanport.SubControls;
using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
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
    internal class Waves
    {
        public int PauseShort { set; get; } = 150;
        public int PauseLong { set; get; } = 370;
        public int PauseSentence { set; get; } = 800;
        public string CustomPauseMark { set; get; } = "$";
        public string OutputDir { set; get; } = "";

        public VoicePreset Preset { get; init; }
        private readonly List<Wave> waves = new();
        public string Text { get; private set; } = "";
        public int Count => waves.Count;

        public Waves(VoicePreset preset)
        {
            Preset = preset;
        }

        public void Add(Wave wave, string text)
        {
            Text += text;

            Wave tmp = new(wave);

            int silence = 150;
            if ("。！？♪".Contains(text[^1]))
            {
                silence = 800;
            }
            else if ("、".Contains(text[^1]))
            {
                silence = 370;
            }

            if (text.Split(CustomPauseMark).Length >= 2)
            {
                if (int.TryParse(text.Split(CustomPauseMark).Last(), out int result))
                {
                    silence = result;

                    Text = Text[..^(text.Split(CustomPauseMark).Last().Length + CustomPauseMark.Length)];
                }
            }

            tmp.CreateSilence(silence / 1000.0);

            waves.Add(tmp);
        }

        public void Write()
        {
            Wave? tmp = null;

            foreach (Wave wave in waves)
            {
                if (tmp is null)
                {
                    tmp = new(wave);
                }
                else
                {
                    tmp.Append(wave);
                }
            }

            if (tmp is null)
            {
                return;
            }

            if (Directory.Exists(OutputDir))
            {
                string output_path = $@"{OutputDir}\{DateTime.Now.Ticks}";
                File.WriteAllText(output_path + ".txt", Text, Encoding.GetEncoding("Shift_JIS"));
                tmp.Save(output_path + ".wav");

                if (Ext.GCMZDrops.IsUsable)
                {
                    List<string> files = new()
                    {
                        output_path + ".wav",
                        output_path + ".txt",
                    };
                    int layer = 1;
                    if (Preset.Num is int num)
                    {
                        layer = num;
                    }

                    Ext.GCMZDrops.SendFiles(files, tmp.PlayTime.TotalMilliseconds, layer);
                }
            }
            else if (Ext.GCMZDrops.ProjectPath is string proj_path && Directory.GetParent(proj_path) is DirectoryInfo proj_dir)
            {
                string output_path = $@"{proj_dir.FullName}\{DateTime.Now.Ticks}";
                File.WriteAllText(output_path + ".txt", Text, Encoding.GetEncoding("Shift_JIS"));

                tmp.Save(output_path + ".wav");

                List<string> files = new()
                {
                    output_path + ".wav",
                    output_path + ".txt",
                };
                int layer = 1;
                if (Preset.Num is int num)
                {
                    layer = num;
                }

                Ext.GCMZDrops.SendFiles(files, tmp.PlayTime.TotalMilliseconds, layer);
            }
        }
    }
    internal class PhraseList : ListBox
    {
        private const int MAX_PHRASE_COUNT = 255;

        private bool processing = false;

        //単語辞書用のイベント
        public event MyEventHandler? OnSpeech;
        public event MyEventHandler? TextSelected;
        public event MyEventHandler? KanaSelected;

        //発話中のUIロック用
        public event MyEventHandler? SpeechBegin;
        public event MyEventHandler? SpeechEnd;

        public event EventHandler? CheckStateChanged;

        public TAccentProvider AccentProvider { set; get; } = TAccentProvider.Default;

        public bool AccentLock = false;
        public bool CustomPause = true;
        public string CustomPauseMark = "$";
        public string OutputDir = "";

        public VoicePreset? Preset => (SelectedItem as PhraseEditView)?.Preset;
        public int? PresetIndex
        {
            get => (SelectedItem as PhraseEditView)?.Presets.SelectedIndex;
            set
            {
                if (value is int v && SelectedItem is PhraseEditView phrase)
                {
                    phrase.Presets.SelectedIndex = v;
                }
            }
        }

        public PhraseList()
        {
            SelectionChanged += (sender, e) =>
            {
                foreach (PhraseEditView phrase in Items)
                {
                    phrase.Kana.Visibility = Visibility.Collapsed;
                }
            };
        }

        public bool? IsAllChecked
        {
            get
            {
                bool on = false;
                bool off = false;

                foreach (PhraseEditView phrase in Items)
                {
                    if (phrase.Check.IsChecked == true && on == false)
                    {
                        on = true;
                    }
                    if (phrase.Check.IsChecked == false && off == false)
                    {
                        off = true;
                    }
                }
                if (on && off)
                {
                    return null;
                }
                else if (on)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            set
            {
                foreach (PhraseEditView phrase in Items)
                {
                    phrase.Check.IsChecked = value;
                }
            }
        }

        public int UncachedCheckedPhraseCount
        {
            get
            {
                int cnt = 0;
                foreach (PhraseEditView curr in Items)
                {
                    if (curr.Check.IsChecked == true && curr.Wave is null && curr.Text.Text.Length > 0)
                    {
                        cnt++;
                    }
                }
                return cnt;
            }
        }
        public int CheckedPhraseCount
        {
            get
            {
                int cnt = 0;
                foreach (PhraseEditView curr in Items)
                {
                    if (curr.Check.IsChecked == true && curr.Text.Text.Length > 0)
                    {
                        cnt++;
                    }
                }
                return cnt;
            }
        }
        public int PauseShort { set; get; } = 150;
        public int PauseLong { set; get; } = 370;
        public int PauseSentence { set; get; } = 800;
        public double MasterVolume { set; get; } = 1.0;

        public new void Focus()
        {
            base.Focus();

            (SelectedItem as PhraseEditView)?.Text.Focus();
        }

        public void ClearAllText()
        {
            foreach(PhraseEditView item in Items)
            {
                item.Text.Text = "";
            }
        }

        /// <summary>
        /// success,failedに指定した色で約1秒間テキストボックスの色が変わる
        /// failedがnullの場合、AIKanaが取得できない場合でもCheckが入っているフレーズの文章を取得するが、
        /// 取得できない場合のkanaは空(="")になる。
        /// </summary>
        /// <param name="success"></param>
        /// <param name="failed"></param>
        /// <returns></returns>
        public List<(string text, string kana, TDialect dialect)> GetCheckedPhraseKana(Brush success, Brush? failed)
        {
            List<(string text, string kana, TDialect dialect)> result = new();

            foreach (PhraseEditView item in Items)
            {
                if (item.Check.IsChecked == true)
                {
                    string text = item.Text.Text;

                    if (text.Split(CustomPauseMark).Length >= 2)
                    {
                        if (int.TryParse(text.Split(CustomPauseMark).Last(), out int _))
                        {
                            text = text[..^(text.Split(CustomPauseMark).Last().Length + CustomPauseMark.Length)];
                        }
                    }

                    if (text.Length > 0 && item.Kana.Text.Length > 0 && item.Preset is not null)
                    {
                        result.Add((text, item.Kana.AIKana, item.Preset.Dialect));
                        item.Notice(success, 1000);
                    }
                    else
                    {
                        if (failed is not null)
                        {
                            item.Notice(failed, 1000);
                        }
                        else if (text.Length > 0 && item.Preset is not null)
                        {
                            result.Add((text, "", item.Preset.Dialect));
                            item.Notice(success, 1000);
                        }
                    }
                }
            }

            return result;
        }

        public void SaveWaves(object sender, MyEventArgs e)
        {
            SpeechEnd -= SaveWaves;

            if (e.Data is string mes && mes == "Canceled")
            {
                return;
            }

            Waves? waves = null;

            foreach (PhraseEditView curr in Items)
            {
                if (curr.Check.IsChecked != true)
                {
                    continue;
                }
                if (curr.Preset is null)
                {
                    continue;
                }

                //空白のフレーズをWAVファイル分割の区切りとして使う
                if (curr.Wave is null)
                {
                    if (waves is not null && waves.Count > 0)
                    {
                        waves.Write();
                        waves = null;
                    }
                    continue;
                }

                if (waves is null)
                {
                    waves = new Waves(curr.Preset)
                    {
                        PauseShort = PauseShort,
                        PauseLong = PauseLong,
                        PauseSentence = PauseSentence,
                        CustomPauseMark = CustomPauseMark,
                        OutputDir = OutputDir,
                    };
                }

                //プリセットの変化でWAVファイルを区切る
                if (waves.Count > 0 && curr.Preset != waves.Preset)
                {
                    waves.Write();
                    waves = new Waves(curr.Preset)
                    {
                        PauseShort = PauseShort,
                        PauseLong = PauseLong,
                        PauseSentence = PauseSentence,
                        CustomPauseMark = CustomPauseMark,
                        OutputDir = OutputDir,
                    };
                }
                waves.Add(curr.Wave, curr.Text.Text);
            }

            if (waves is not null && waves.Count > 0)
            {
                waves.Write();
            }
        }

        public void Speech(string text)
        {
            if (SelectedItem is not PhraseEditView item)
            {
                return;
            }

            if (item.Presets.SelectedItem is ComboBoxItem citem
                && citem.Content is VoicePreset preset)
            {
                SpeechBegin?.Invoke(this, new MyEventArgs());
                processing = true;
                notice = true;
                preset.LongPause = PauseLong;
                preset.MiddlePause = PauseShort;

                string param = JsonSerializer.Serialize(preset);
                /*
                param += $"voice:{preset.VoiceName} ";
                param += $"vol:{preset.Volume} ";
                param += $"spd:{preset.Speed} ";
                param += $"pit:{preset.Pitch} ";
                param += $"emph:{preset.PitchRange} ";
                param += $"dialect:{preset.Dialect}";
                */

                APIManager.Param(citem.DataContext, param);
                APIManager.WaveReceived += APIManager_WaveReceived;
                APIManager.Speech(citem.DataContext, text);

            }
            else
            {
                processing = false;
            }
        }

        bool notice = false;
        public void SpeechSelected()
        {
            if (SelectedItem is PhraseEditView sel)
            {
                notice = true;
                SpeechPhrase(sel, true);
            }
        }

        public void GetSelectedWave()
        {
            if (SelectedItem is PhraseEditView sel)
            {
                notice = true;
                SpeechPhrase(sel, false);
            }
        }

        public (string? Text, Wave? Wave) SelectedData
        {
            get
            {
                if (SelectedItem is PhraseEditView sel)
                {
                    return (sel.Text.Text, sel.Wave);
                }
                return (null, null);
            }
        }

        bool wave_only = false;
        private void SpeechPhrase(PhraseEditView item, bool with_sound)
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

                preset.LongPause = PauseLong;
                preset.MiddlePause = PauseShort;
                string param = JsonSerializer.Serialize(preset);
                /*
                string param = "";
                param += $"voice:{preset.VoiceName} ";
                param += $"vol:{preset.Volume} ";
                param += $"spd:{preset.Speed} ";
                param += $"pit:{preset.Pitch} ";
                param += $"emph:{preset.PitchRange} ";

                foreach(Style style in preset.Styles)
                {
                    param += $"[{style.Name}:{style.Value}] ";
                }

                param += $"dialect:{preset.Dialect}";
                */
                APIManager.Param(citem.DataContext, param);

                speech.Dialect = preset.Dialect;

                APIManager.WaveReceived += APIManager_WaveReceived;
                APIManager.KanaReceived += MainWindow_KanaReceived;

                string text = item.Text.Text;
                string kana = item.Kana.AIKana;

                if (text.Split(CustomPauseMark).Length >= 2 && CustomPause)
                {
                    if (int.TryParse(text.Split(CustomPauseMark).Last(), out int result))
                    {
                        text = text[..^(text.Split(CustomPauseMark).Last().Length + CustomPauseMark.Length)];
                    }
                }

                if (item.Kana.IsFocused && item.Kana.SelectedAIKana is not "<S>" and string selected)
                {
                    kana = selected;
                }

                wave_only = !with_sound;

                if (citem.DataContext == APIManager.AssistantSeikaAPI)
                {
                    wave_only = true;
                    APIManager.Speech(citem.DataContext, text);
                    APIManager.KanaReceived -= MainWindow_KanaReceived;
                }
                else if (kana != "<S>")
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
        private bool SpeechPhraseFromCache(PhraseEditView item)
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

        public void SpeechPhraseList(bool PrioritizeCache, bool with_sound)
        {
            ctsource = new CancellationTokenSource();
            SynchronizationContext? context = SynchronizationContext.Current;

            Task.Factory.StartNew(async () =>
            {
                foreach (PhraseEditView item in Items)
                {
                    if (ctsource.Token.IsCancellationRequested)
                    {
                        break;
                    }

                    bool check = false;
                    context?.Send(_ => check = item.Check.IsChecked == true, null);

                    if (!check)
                    {
                        continue;
                    }

                    int silence;
                    string text = "";

                    context?.Send(_ => text = item.Text.Text, null);

                    if (text.Length == 0)
                    {
                        continue;
                    }

                    if ("。！？♪".Contains(text[^1]))
                    {
                        silence = PauseSentence;
                    }
                    else if ('、' == text[^1])
                    {
                        silence = PauseLong;
                    }
                    else
                    {
                        silence = PauseShort;
                    }

                    if (text.Split(CustomPauseMark).Length >= 2)
                    {
                        if (int.TryParse(text.Split(CustomPauseMark).Last(), out int result))
                        {
                            silence = result;
                        }
                    }

                    bool cached = item.Wave is not null;

                    if (PrioritizeCache && cached)
                    {
                        if (with_sound)
                        {
                            context?.Send(_ => SpeechPhraseFromCache(item), null);
                        }
                    }
                    else
                    {
                        context?.Send(_ => SpeechPhrase(item, with_sound), null);
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

                SpeechEnd?.Invoke(this, new MyEventArgs() { Data = "Canceled" });
            }
        }

        private bool prev_speech = false;
        private PhraseEditView? received_item;
        private void MainWindow_KanaReceived(object sender, MyEventArgs e)
        {
            APIManager.KanaReceived -= MainWindow_KanaReceived;

            if (received_item is not null && e.Data is string text && text.Length > 0)
            {
                received_item.Kana.SetAIKANA(text);
                received_item.Notice(Brushes.LightBlue);
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

            if (e.Data is Wave wave)
            {

                if (MasterVolume != 1.0)
                {
                    if (wave.Gain(MasterVolume))
                    {
                        received_item?.Notice(Brushes.Red, 1000);
                    }
                }

                if (received_item is not null)
                {
                    received_item.Wave = wave;
                }

                if (wave_only)
                {
                    wave_only = false;
                    processing = false;
                    if (notice)
                    {
                        SpeechEnd?.Invoke(this, new MyEventArgs() { Data = wave });
                        notice = false;
                    }
                }
                else
                {
                    WavePlayer.Play(wave);

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

            foreach (PhraseEditView item in Items)
            {
                ComboBoxItem item_dst = new() { Content = preset, Focusable = false };
                item_dst.SetBinding(DataContextProperty, binding);

                item.Presets.Items.Add(item_dst);
            }
            foreach (PhraseEditView item in Stocks)
            {
                ComboBoxItem item_dst = new() { Content = preset, Focusable = false };
                item_dst.SetBinding(DataContextProperty, binding);

                item.Presets.Items.Add(item_dst);
            }
        }
        public bool RemovePreset(VoicePreset preset)
        {
            foreach (PhraseEditView item in Items)
            {
                if (item.Preset == preset)
                {
                    return false;
                }
            }

            foreach (PhraseEditView item in Items)
            {
                item.Presets.Items.Remove(item.Presets.Items.Cast<ComboBoxItem>().First(x => x.Content == preset));
            }
            foreach (PhraseEditView item in Stocks)
            {
                item.Presets.Items.Remove(item.Presets.Items.Cast<ComboBoxItem>().First(x => x.Content == preset));
            }

            return true;
        }
        public void MovePreset(VoicePreset preset, int index_dst)
        {
            foreach (PhraseEditView phrase in Items)
            {
                var item = phrase.Presets.Items.Cast<ComboBoxItem>().First(x => x.Content == preset);
                bool selected = item.IsSelected;

                if (phrase.Presets.Items.IndexOf(item) != index_dst)
                {
                    phrase.Presets.Items.Remove(item);
                    phrase.Presets.Items.Insert(index_dst, item);

                    if (selected)
                    {
                        phrase.Presets.SelectedItem = item;
                    }
                }
            }
        }

        /// <summary>
        /// フレーズに設定されているプリセット名を一括で変更する
        /// </summary>
        /// <param name="index"></param>
        public void RenamePreset(int index)
        {
            foreach (PhraseEditView item in Items)
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
            foreach (PhraseEditView item in Stocks)
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
            if (Items.Count > MAX_PHRASE_COUNT)
            {
                return;
            }

            PhraseEditView dst = CreatePhraseView();
            dst.IsFocusOnLoadead = focus;
            if (Items.Count > 0)
            {
                if (Items[^1] is PhraseEditView src)
                {
                    dst.Presets.SelectedIndex = src.Presets.SelectedIndex;
                }
                else
                {
                    dst.Presets.SelectedIndex = 0;
                }
            }
            Items.Add(dst);
            ScrollIntoView(dst);
            CheckStateChanged?.Invoke(this, new EventArgs());
        }
        public void RemovePhraseLine(bool focus)
        {
            if (SelectedItem is PhraseEditView phrase)
            {
                if (focus)
                {
                    SelectedIndex--;
                }

                Items.Remove(phrase);
                phrase.Refresh();
                Stocks.Enqueue(phrase);

                if (focus)
                {
                    if (SelectedIndex == -1 && Items.Count > 0)
                    {
                        SelectedIndex = 0;
                    }
                }
            }
            CheckStateChanged?.Invoke(this, new EventArgs());
        }
        public void InsertPhraseLine(bool focus)
        {
            if (Items.Count > MAX_PHRASE_COUNT)
            {
                return;
            }

            PhraseEditView dst = CreatePhraseView();
            dst.IsFocusOnLoadead = focus;

            if (SelectedItem is PhraseEditView src)
            {
                dst.Presets.SelectedIndex = src.Presets.SelectedIndex;
            }
            else
            {
                dst.Presets.SelectedIndex = 0;
            }
            Items.Insert(SelectedIndex + 1, dst);
            ScrollIntoView(dst);
            CheckStateChanged?.Invoke(this, new EventArgs());
        }

        public void EditPhraseLine()
        {
            if (SelectedItem is PhraseEditView src)
            {
                if (src.AccentVisible)
                {
                    src.AccentVisible = false;
                }
                else
                {
                    src.AccentVisible = true;
                }
            }
        }

        private readonly Queue<PhraseEditView> Stocks = new();
        private PhraseEditView CreatePhraseView()
        {
            if (Stocks.Count > 0)
            {
                return Stocks.Dequeue();
            }

            PhraseEditView phrase = new();

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

            phrase.Check.Checked += Check_Changed;
            phrase.Check.Unchecked += Check_Changed;

            foreach ((VoicePreset preset, Binding binding) in bindlist)
            {
                ComboBoxItem cbitem = new() { Content = preset, Focusable = false };
                cbitem.SetBinding(DataContextProperty, binding);
                phrase.Presets.Items.Add(cbitem);
            }
            if (Parent is Grid parent)
            {
                parent.Children.Add(phrase.Kana);
                parent.Children.Add(phrase.SearchResult);
            }
            return phrase;
        }

        private void Check_Changed(object sender, RoutedEventArgs e)
        {
            CheckStateChanged?.Invoke(sender, e);
        }
    }
}
