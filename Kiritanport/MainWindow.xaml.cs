using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace Kiritanport
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private readonly bool uicheck = false;
        private int api_cnt = 0;

        private WordDictionary? wdic_standard;
        private WordDictionary? wdic_kansai;
        private PhraseDictionary? pdic_standard;
        private PhraseDictionary? pdic_kansai;

        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitializeComponent();
        }
        private void Init()
        {
            WordDictionary wdic = new(@".\test.wdic");
            PhraseDictionary pdic = new(@".\test.pdic");

            wdic_standard = wdic;
            wdic_kansai = wdic;
            pdic_standard = pdic;
            pdic_kansai = pdic;

            //テスト用辞書
            wdic.AddWord("東北きりたん", "<S>^ポ!ポコ<N>", WordClass.人名, WordPriority.MID);
            wdic.Save();

            if (File.Exists(wdic.PathDic))
            {
                APIManager.Dictionary(APIManager.VoiceroidAPI, wdic_standard.PathDic);
                APIManager.DictionaryKansai(APIManager.VoiceroidAPI, wdic_kansai.PathDic);
            }
            if (File.Exists(pdic.PathDic))
            {
                APIManager.Dictionary(APIManager.VoiceroidAPI, pdic_standard.PathDic);
                APIManager.DictionaryKansai(APIManager.VoiceroidAPI, pdic_kansai.PathDic);
            }

            foreach (WordClass wclass in Enum.GetValues(typeof(WordClass)))
            {
                ComboBoxItem item = new() { Content = wclass };
                CB_WordClass.Items.Add(item);
            }

            LB_WordList.SelectionChanged += (sender, e) =>
            {
                if (LB_WordList.SelectedIndex == -1)
                {
                    TB_WordText.Text = "";
                    TB_WordKana.Text = "";
                    CB_WordClass.SelectedIndex = -1;
                }
            };

            CLB_PhraseList.TextSelected += (sender, e) =>
            {
                if (e.Data is string str)
                {
                    LB_WordList.SelectedIndex = -1;
                    TB_WordText.Text = str;
                }
            };

            CLB_PhraseList.KanaSelected += (sender, e) =>
            {
                if (e.Data is string str)
                {
                    LB_WordList.SelectedIndex = -1;
                    TB_WordKana.Text = str;
                }
            };

            CLB_PhraseList.OnSpeech += (sender, e) =>
            {
                if (e.Data is SpeechData str)
                {
                    WordDictionary dic = str.Dialect == TDialect.Standard ? wdic_standard : wdic_kansai;

                    dic.FindWords(str.Text, out List<Word> words);

                    LB_WordList.Items.Clear();

                    foreach (Word word in words)
                    {
                        ListBoxItem item = new() { Content = word };
                        item.Selected += (_, _) =>
                        {
                            TB_WordText.Text = word.Text;
                            TB_WordKana.Text = word.AIKana;

                            foreach (ComboBoxItem item2 in CB_WordClass.Items)
                            {
                                if (item2.Content is WordClass wc && wc == word.wordClass)
                                {
                                    CB_WordClass.SelectedItem = item2;
                                    break;
                                }
                            }
                        };

                        LB_WordList.Items.Add(item);
                    }
                }
            };


            //ユーザープリセットの読み込みをする


            //テスト用プリセット

            for (int i = 0; i < 3 && i < CB_VoiceList.Items.Count; i++)
            {
                if (CB_VoiceList.Items[i] is ComboBoxItem item_src)
                {
                    if (item_src.Content is VoicePreset preset_normal)
                    {
                        VoicePreset preset = (VoicePreset)preset_normal.Clone();

                        preset.PresetName = $"Voice{i}";
                        var item_dst = new ComboBoxItem() { Content = preset, DataContext = item_src.DataContext };

                        Binding binding = new()
                        {
                            Source = item_dst,
                            Path = new PropertyPath("DataContext"),
                            Mode = BindingMode.Default,
                        };
                        CLB_PhraseList.AddPreset(preset, binding);
                        CB_PresetList.Items.Add(item_dst);
                    }
                }
            }

            TB_Preset_Name.TextChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem == null)
                {
                    return;
                }
                CLB_PhraseList.Rename(CB_PresetList.SelectedIndex);

                if (CB_PresetList.SelectedItem is ComboBoxItem citem && citem.Content is VoicePreset preset)
                {
                    preset.PresetName = TB_Preset_Name.Text;
                    citem.Content = null;
                    citem.Content = preset;

                    CB_PresetList.SelectionChanged -= CB_PresetList_SelectionChanged;
                    var index = CB_PresetList.SelectedIndex;
                    CB_PresetList.SelectedIndex = -1;
                    CB_PresetList.SelectedIndex = index;
                    CB_PresetList.SelectionChanged += CB_PresetList_SelectionChanged;
                }

            };

            CB_PresetList.SelectionChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item1 && item1.Content is VoicePreset preset1)
                {
                    Num_Vol.Value = preset1.Volume;
                    Num_Spd.Value = preset1.Speed;
                    Num_Pit.Value = preset1.Pitch;
                    Num_EMPH.Value = preset1.PitchRange;

                    foreach (ComboBoxItem item2 in CB_VoiceList.Items)
                    {
                        if (item2.Content is VoicePreset preset2 && preset2.VoiceName == preset1.VoiceName)
                        {
                            CB_VoiceList.SelectedItem = item2;
                        }
                    }
                }
            };

            CB_VoiceList.SelectionChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item_dst && item_dst.Content is VoicePreset preset_dst)
                {
                    if (CB_VoiceList.SelectedItem is ComboBoxItem item_src && item_src.Content is VoicePreset preset_src)
                    {
                        preset_dst.VoiceName = preset_src.VoiceName;
                        item_dst.DataContext = item_src.DataContext;
                    }
                }
            };

            Num_Vol.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Volume = (float)Num_Vol.Value;
                }
            };
            Num_Spd.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Speed = (float)Num_Spd.Value;
                }
            };
            Num_Pit.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Pitch = (float)Num_Pit.Value;
                }
            };
            Num_EMPH.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.PitchRange = (float)Num_EMPH.Value;
                }
            };
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (uicheck)
            {
                return;
            }

            APIManager.MessageReceived += APIManager_MessageReceived;
            APIManager.Init();
        }

        private void APIManager_MessageReceived(object sender, MyEventArgs e)
        {
            if (sender is not Process process)
            {
                return;
            }

            if (e.Data is not string mes)
            {
                return;
            }

            TB_Log.Text = $"{process.ProcessName} : [ {mes} ]";

            if (mes.StartsWith("voice>"))
            {
                string voice_name = mes["voice>".Length..].Split(":")[0];
                string chara_name = mes["voice>".Length..].Split(":")[1];

                VoicePreset preset = new()
                {
                    VoiceName = voice_name,
                    Dialect = voice_name.Contains("west") ? TDialect.Kansai : TDialect.Standard,
                    Styles = voice_name.Contains("emo") ? new Voiceroid.Style[3] : Array.Empty<Voiceroid.Style>(),
                    Type = TType.Normal,
                    Volume = 1,
                    Speed = 1,
                    Pitch = 1,
                    PitchRange = 1,
                    MiddlePause = 150,
                    LongPause = 370,
                    PresetName = chara_name,
                };

                CB_VoiceList.Items.Add(new ComboBoxItem() { Content = preset, DataContext = process });
            }

            if (mes == "Ready." || mes == "Exit.")
            {
                api_cnt++;

                if (api_cnt == APIManager.APIsCount)
                {
                    Init();
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CLB_PhraseList.SpeechTexts();
        }

        private void CustomListBox_Loaded(object sender, RoutedEventArgs e)
        {
            CLB_PhraseList.AddLine(false);
        }

        private void CB_PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
            {
                TB_Preset_Name.Text = preset.PresetName;
            }
        }

        private void CB_AccentProviderList_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (TAccentProvider provider in Enum.GetValues<TAccentProvider>())
            {
                ComboBoxItem item = new() { Content = provider, };
                CB_AccentProviderList.Items.Add(item);
            }
            CB_AccentProviderList.SelectedIndex = (int)TAccentProvider.Default;
            CB_AccentProviderList.SelectionChanged += (_, _) =>
            {
                if (CB_AccentProviderList.SelectedItem is ComboBoxItem citem && citem.Content is TAccentProvider provider)
                {
                    if (provider == TAccentProvider.Voiceroid)
                    {
                        if (APIManager.VoiceroidAPI is null || APIManager.VoiceroidAPI.HasExited)
                        {
                            CB_AccentProviderList.SelectedIndex = (int)TAccentProvider.Default;
                        }
                    }
                    else if (provider == TAccentProvider.Voicevox)
                    {
                        if (APIManager.VoicevoxAPI is null || APIManager.VoicevoxAPI.HasExited)
                        {
                            CB_AccentProviderList.SelectedIndex = (int)TAccentProvider.Default;
                        }
                    }
                    CLB_PhraseList.AccentProvider = provider;
                }
            };
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            APIManager.Exit();
        }

        private void StatusBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (APIManager.Log.Length > 1024)
            {
                MessageBox.Show(APIManager.Log[^1024..], "APIs Log");
            }
            else
            {
                MessageBox.Show(APIManager.Log, "APIs Log");
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CLB_PhraseList.AccentLock = true;
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CLB_PhraseList.AccentLock = false;
        }
    }
}
