using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Xml.Serialization;

namespace Kiritanport
{
    public class Configure
    {
        public VoicePreset[] Presets { get; set; } = Array.Empty<VoicePreset>();
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private readonly bool uicheck = false;
        private int api_cnt = 0;
        private bool IsInit { get; set; } = false;

        private SpeechData? prev;

        private WordDictionary? wdic_standard;
        private WordDictionary? wdic_kansai;
        internal PhraseDictionary? pdic_standard;
        internal PhraseDictionary? pdic_kansai;

        private Configure? configure;

        private const string PathConfigre = @".\configure.xml";
        private const string PathWordDictionary = @".\test.wdic";
        private const string PathPhraseDictionary = @".\test.pdic";

        private new bool IsEnabled
        {
            set
            {
                if (Content is Grid root)
                {
                    foreach (UIElement ui in root.Children)
                    {
                        if (ui == BT_Stop)
                        {
                            continue;
                        }

                        if (!IsVisible)
                        {
                            if (ui is not Label && ui is not TextBlock && ui is not StatusBar)
                            {
                                ui.IsEnabled = value;
                            }
                        }
                        else if (ui.IsVisible && ui is not Label && ui is not TextBlock && ui is not StatusBar)
                        {
                            ui.IsEnabled = value;
                        }
                    }
                }
            }
        }

        public MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            InitializeComponent();
            IsEnabled = false;
            BT_Stop.IsEnabled = false;

            if (uicheck)
            {
                return;
            }

            APIManager.MessageReceived += APIManager_MessageReceived;
            APIManager.Init();
        }
        private void APIManager_MessageReceived(object sender, MyEventArgs e)
        {
            if (sender is not Process process || e.Data is not string mes)
            {
                throw new("invalid message received.");
            }

            TB_Log.Text = $"{APIManager.GetKey(sender)} : [ {mes} ]";

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
        private void Init()
        {
            //設定の読み込みをする
            LoadConfig();

            LB_WordList.SelectionChanged += (sender, e) =>
            {
                if (LB_WordList.SelectedIndex == -1)
                {
                    TB_WordText.Text = "";
                    TB_WordKana.Text = "";
                    CB_WordClass.SelectedIndex = -1;
                }
            };
            PL_PhraseList.TextSelected += (sender, e) =>
            {
                if (e.Data is string str)
                {
                    LB_WordList.SelectedIndex = -1;
                    TB_WordText.Text = str;
                }
            };
            PL_PhraseList.KanaSelected += (sender, e) =>
            {
                if (e.Data is string str)
                {
                    LB_WordList.SelectedIndex = -1;
                    TB_WordKana.Text = str;
                }
            };
            PL_PhraseList.OnSpeech += (sender, e) =>
            {
                if (e.Data is SpeechData data)
                {
                    prev = data;

                    TB_WordKana.IsEnabled = true;
                    TB_WordText.IsEnabled = true;
                    CB_WordClass.IsEnabled = true;

                    WordDictionary? dic = data.Dialect == TDialect.Standard ? wdic_standard : wdic_kansai;

                    if (dic is null)
                    {
                        return;
                    }

                    dic.FindWords(data.Text, out List<Word> words);

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

            IInputElement? element = null;
            PL_PhraseList.SpeechEnd += (sender, e) =>
            {
                IsEnabled = true;
                BT_Stop.IsEnabled = false;
                element?.Focus();
            };
            PL_PhraseList.SpeechBegin += (sender, e) =>
            {
                element = FocusManager.GetFocusedElement(this);

                IsEnabled = false;
                BT_Stop.IsEnabled = true;
            };
            PL_PhraseList.CheckStateChanged += (sender, e) =>
            {
                bool on = false;
                bool off = false;

                foreach (SubControls.PhraseView phrase in PL_PhraseList.Items)
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
                    CB_All.IsChecked = null;
                }
                else if (on)
                {
                    CB_All.IsChecked = true;
                }
                else
                {
                    CB_All.IsChecked = false;
                }
            };

            CB_All.Checked += (sender, e) =>
            {
                foreach (SubControls.PhraseView phrase in PL_PhraseList.Items)
                {
                    phrase.Check.IsChecked = true;
                }
            };
            CB_All.Unchecked += (sender, e) =>
            {
                foreach (SubControls.PhraseView phrase in PL_PhraseList.Items)
                {
                    phrase.Check.IsChecked = false;
                }
            };

            TB_Preset_Name.TextChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem == null)
                {
                    return;
                }
                PL_PhraseList.RenamePreset(CB_PresetList.SelectedIndex);

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
                    VP_Vol.Value = preset1.Volume;
                    VP_Spd.Value = preset1.Speed;
                    VP_Pit.Value = preset1.Pitch;
                    VP_EMPH.Value = preset1.PitchRange;

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
            VP_Vol.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Volume = (float)VP_Vol.Value;
                }
            };
            VP_Spd.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Speed = (float)VP_Spd.Value;
                }
            };
            VP_Pit.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Pitch = (float)VP_Pit.Value;
                }
            };
            VP_EMPH.ValueChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.PitchRange = (float)VP_EMPH.Value;
                }
            };
            IsEnabled = true;
            IsInit = true;
        }
        private void LoadConfig()
        {
            if (File.Exists(PathConfigre))
            {
                XmlSerializer serializer = new(typeof(Configure));
                using StreamReader reader = new(PathConfigre);
                if (serializer.Deserialize(reader) is Configure c)
                {
                    configure = c;

                    foreach (VoicePreset preset in configure.Presets)
                    {
                        foreach (ComboBoxItem item_src in CB_VoiceList.Items)
                        {
                            if (item_src.Content is VoicePreset loaded && loaded.VoiceName == preset.VoiceName)
                            {
                                ComboBoxItem item_dst = new()
                                {
                                    Content = preset,
                                    DataContext = item_src.DataContext
                                };

                                Binding binding = new()
                                {
                                    Source = item_dst,
                                    Path = new PropertyPath("DataContext"),
                                    Mode = BindingMode.Default,
                                };

                                PL_PhraseList.AddPreset(preset, binding);
                                CB_PresetList.Items.Add(item_dst);
                            }
                        }
                    }
                }
            }
            else
            {
                //テスト用プリセット
                for (int i = 0; i < 3 && i < CB_VoiceList.Items.Count; i++)
                {
                    if (CB_VoiceList.Items[i] is ComboBoxItem item_src)
                    {
                        if (item_src.Content is VoicePreset preset_normal)
                        {
                            VoicePreset preset = (VoicePreset)preset_normal.Clone();

                            preset.PresetName = $"Voice{i}";
                            preset.Type = TType.User;
                            var item_dst = new ComboBoxItem() { Content = preset, DataContext = item_src.DataContext };

                            Binding binding = new()
                            {
                                Source = item_dst,
                                Path = new PropertyPath("DataContext"),
                                Mode = BindingMode.Default,
                            };
                            PL_PhraseList.AddPreset(preset, binding);
                            CB_PresetList.Items.Add(item_dst);
                        }
                    }
                }

                List<VoicePreset> presets = new();

                foreach (ComboBoxItem item_src in CB_PresetList.Items)
                {
                    if (item_src.Content is VoicePreset preset)
                    {
                        presets.Add(preset);
                    }
                }

                configure = new Configure()
                {
                    Presets = presets.ToArray(),
                };
            }

            WordDictionary wdic = new(@".\test.wdic");
            PhraseDictionary pdic = new(@".\test.pdic");

            wdic_standard = wdic;
            wdic_kansai = wdic;
            pdic_standard = pdic;
            pdic_kansai = pdic;

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
        }
        private void SaveConfig()
        {
            if (IsInit)
            {
                XmlSerializer serializer = new(typeof(Configure));
                using StreamWriter writer = new(PathConfigre);
                serializer.Serialize(writer, configure);

                wdic_standard?.Save();
                pdic_standard?.Save();
                if (wdic_standard != wdic_kansai)
                {
                    wdic_kansai?.Save();
                }
                if (pdic_standard != pdic_kansai)
                {
                    pdic_kansai?.Save();
                }
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveConfig();
            APIManager.Exit();
        }
        private void PL_PhraseList_Loaded(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.AddPhraseLine(false);
        }
        private void BT_Play_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.SpeechPhraseList(CB_Cache.IsChecked == true);
        }
        private void BT_Stop_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.SpeechCancel();
        }
        private void BT_Save_Click(object sender, RoutedEventArgs e)
        {
            List<Wave> waves = new();
            VoicePreset? prev = null;
            string text = "";

            //処理が強引すぎるで後で直す
            foreach (SubControls.PhraseView curr in PL_PhraseList.Items)
            {
                if (curr.Check.IsChecked != true)
                {
                    continue;
                }

                if (curr.Preset is null || curr.Wave is null)
                {
                    MessageBox.Show("保存の前に再生してください");
                    break;
                }

                if (prev == curr.Preset || prev is null)
                {
                    text += curr.Text.Text;

                    Wave tmp = new(curr.Wave);

                    int silence = 150;
                    if ("。！？♪".Contains(curr.Text.Text[^1]))
                    {
                        silence = 800;
                    }
                    else if ("、".Contains(curr.Text.Text[^1]))
                    {
                        silence = 370;
                    }

                    tmp.CreateSilence(silence / 1000.0);

                    waves.Add(tmp);

                    prev = curr.Preset;
                }
                else
                {
                    Wave? output = null;

                    foreach (Wave wave in waves)
                    {
                        if (output is null)
                        {
                            output = wave;
                        }
                        else
                        {
                            output.Append(wave);
                        }
                    }


                    if (output is null)
                    {
                        continue;
                    }

                    if (Ext.GCMZDrops.ProjectPath is string proj_path && Directory.GetParent(proj_path) is DirectoryInfo proj_dir)
                    {
                        string output_path = $@"{proj_dir.FullName}\{DateTime.Now.Ticks}";
                        File.WriteAllText(output_path + ".txt", text, Encoding.GetEncoding("Shift_JIS"));

                        output.Save(output_path + ".wav");

                        List<string> files = new()
                        {
                            output_path + ".wav",
                            output_path + ".txt",
                        };

                        Ext.GCMZDrops.SendFiles(files, output.PlayTime.TotalMilliseconds, 1);
                    }

                    waves = new List<Wave>();
                    text = "";

                    text += curr.Text.Text;

                    Wave tmp = new(curr.Wave);

                    int silence = 150;
                    if ("。！？♪".Contains(curr.Text.Text[^1]))
                    {
                        silence = 800;
                    }
                    else if ("、".Contains(curr.Text.Text[^1]))
                    {
                        silence = 370;
                    }

                    tmp.CreateSilence(silence / 1000.0);

                    waves.Add(tmp);

                    prev = curr.Preset;
                }
            }

            if (waves.Count > 0)
            {
                Wave? output = null;

                foreach (Wave wave in waves)
                {
                    if (output is null)
                    {
                        output = wave;
                    }
                    else
                    {
                        output.Append(wave);
                    }
                }

                waves = new List<Wave>();

                if (output is null)
                {
                    return;
                }

                if (Ext.GCMZDrops.ProjectPath is string proj_path && Directory.GetParent(proj_path) is DirectoryInfo proj_dir)
                {
                    string output_path = $@"{proj_dir.FullName}\{DateTime.Now.Ticks}";
                    File.WriteAllText(output_path + ".txt", text, Encoding.GetEncoding("Shift_JIS"));

                    output.Save(output_path + ".wav");

                    List<string> files = new()
                    {
                        output_path + ".wav",
                        output_path + ".txt",
                    };

                    Ext.GCMZDrops.SendFiles(files, output.PlayTime.TotalMilliseconds, 1);
                }
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
                    PL_PhraseList.AccentProvider = provider;
                }
            };
        }
        private void CB_PresetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
            {
                TB_Preset_Name.Text = preset.PresetName;
            }
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
            PL_PhraseList.AccentLock = true;
        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.AccentLock = false;
        }
        private void BT_WordPlay_Click(object sender, RoutedEventArgs e)
        {
            if (TB_WordKana.Text.Length == 0)
            {
                return;
            }
            PL_PhraseList.Speech($"<S>{TB_WordKana.Text}<N>");
        }
        private void BT_WordSave_Click(object sender, RoutedEventArgs e)
        {
            if (TB_WordText.Text.Length > 0 && TB_WordKana.Text.Length > 0
                && CB_WordClass.SelectedItem is ComboBoxItem citem && citem.Content is WordClass wclass
                && wdic_standard is not null && wdic_kansai is not null && prev is not null)
            {
                WordDictionary dic = prev.Dialect == TDialect.Standard ? wdic_standard : wdic_kansai;

                dic.AddWord($"{TB_WordText.Text}", $"<S>{TB_WordKana.Text}<N>", wclass, WordPriority.MID);
                dic.Save();

                dic.FindWords(prev.Text, out List<Word> words);

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
                APIManager.Dictionary(APIManager.VoiceroidAPI, wdic_standard.PathDic);
                APIManager.DictionaryKansai(APIManager.VoiceroidAPI, wdic_kansai.PathDic);
            }
        }
        private void BT_WordRemove_Click(object sender, RoutedEventArgs e)
        {
            if (LB_WordList.SelectedItem is ListBoxItem src && src.Content is Word word
                && wdic_standard is not null && wdic_kansai is not null && prev is not null)
            {
                WordDictionary dic = prev.Dialect == TDialect.Standard ? wdic_standard : wdic_kansai;
                dic.RemoveWord(word.Text, word.wordClass);
                APIManager.Dictionary(APIManager.VoiceroidAPI, wdic_standard.PathDic);
                APIManager.DictionaryKansai(APIManager.VoiceroidAPI, wdic_kansai.PathDic);
            }
        }
        private void BT_PhraseAdd_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.InsertPhraseLine(true);
        }
        private void BT_PhraseRemove_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.RemovePhraseLine(true);
        }
        private void BT_PhraseEdit_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.EditPhraseLine();
        }

    }
}
