using Kiritanport.Voiceroid;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;

namespace Kiritanport
{
    public class Configure
    {
        public VoicePreset[] Presets { get; set; } = Array.Empty<VoicePreset>();
        public string PathPhraseDictionary { get; set; } = @".\tmp\tmp.pdic";
        public string PathWordDictionary { get; set; } = @".\tmp\tmp.wdic";
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    public partial class MainWindow : Window
    {
        private readonly bool no_api = false;//APIを起動しないかどうか（UIの挙動確認用）
        private readonly bool expand_dragmove_area = true;//DragMoveの範囲をウィンドウヘッダーから全ウィンドウ領域に広げるかどうか
        private int api_cnt = 0;
        private bool IsInit { get; set; } = false;

        private SpeechData? prev;

        private WordDictionary? wdic_standard;
        private WordDictionary? wdic_kansai;
        internal PhraseDictionary? pdic_standard;
        internal PhraseDictionary? pdic_kansai;

        private Configure? configure;

        private const string PathConfigre = @".\configure.xml";

        private readonly List<(Label Label, VoiceParameterView Param)> StyleControls = new();


        private new bool IsEnabled
        {
            set
            {
                if (Content is Grid root)
                {
                    foreach (UIElement ui in root.Children)
                    {
                        if (ui == BT_Stop || ui == G_Header)
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

            ContextMenu menu = new();
            MenuItem move = new() { Header = "移動(_M)" };
            move.Click += (sender, e) =>
            {
                Mouse.OverrideCursor = Cursors.SizeAll;
            };
            MenuItem minimize = new() { Header = "最小化(_N)" };
            minimize.Click += (sender, e) =>
            {
                WindowState = WindowState.Minimized;
            };
            MenuItem close = new() { Header = "閉じる(_C)", InputGestureText = "Alt+F4" };
            close.Click += Close_Click;

            menu.Items.Add(move);
            menu.Items.Add(minimize);
            menu.Items.Add(new Separator());
            menu.Items.Add(close);
            G_Header.ContextMenu = menu;

            if (no_api)
            {
                Init();
                return;
            }

            StyleControls.Add((L_Style0, VP_Style0));
            StyleControls.Add((L_Style1, VP_Style1));
            StyleControls.Add((L_Style2, VP_Style2));
            StyleControls.Add((L_Style3, VP_Style3));

            APIManager.MessageReceived += APIManager_MessageReceived;
            APIManager.Init();
        }
        private void APIManager_MessageReceived(object sender, MyEventArgs e)
        {
            if (sender is not Process process || e.Data is not string mes)
            {
                throw new("invalid message received.");
            }

            if (mes.Length > 64)
            {
                TB_Log.Text = $"{APIManager.GetKey(sender)} : [ {mes[..64]} ]";
            }
            else
            {
                TB_Log.Text = $"{APIManager.GetKey(sender)} : [ {mes} ]";
            }


            if (mes.StartsWith("voice>"))
            {
                string json_str = mes["voice>".Length..];

                if (JsonSerializer.Deserialize<VoicePreset>(json_str) is VoicePreset preset)
                {
                    CB_VoiceList.Items.Add(new ComboBoxItem() { Content = preset, DataContext = process });
                }
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

            if (CB_VoiceList.Items.Count == 0)
            {
                CustomMessageBox.Show(this, "使用可能なボイスが見つかりませんでした。");
                return;
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
            PL_PhraseList.SelectionChanged += (sender, e) =>
            {
                if (PL_PhraseList.PresetIndex is int index)
                {
                    CB_PresetList.SelectedIndex = index;
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

            //IInputElement? element = null;
            PL_PhraseList.SpeechEnd += (sender, e) =>
            {
                IsEnabled = true;
                BT_Stop.IsEnabled = false;

                PL_PhraseList.Focus();
                //element?.Focus();
            };
            PL_PhraseList.SpeechBegin += (sender, e) =>
            {
                //element = FocusManager.GetFocusedElement(this);

                IsEnabled = false;
                BT_Stop.IsEnabled = true;
            };
            PL_PhraseList.CheckStateChanged += (sender, e) =>
            {
                CB_All.IsChecked = PL_PhraseList.IsAllChecked;
            };

            CB_All.Checked += (sender, e) =>
            {
                PL_PhraseList.IsAllChecked = true;
            };
            CB_All.Unchecked += (sender, e) =>
            {
                PL_PhraseList.IsAllChecked = false;
            };
            TB_Preset_Name.TextChanged += TB_Preset_Name_TextChanged;

            CB_PresetList.SelectionChanged += (_, _) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item_dst && item_dst.Content is VoicePreset preset_dst)
                {
                    VP_Vol.Value = preset_dst.Volume;
                    VP_Spd.Value = preset_dst.Speed;
                    VP_Pit.Value = preset_dst.Pitch;
                    VP_EMPH.Value = preset_dst.PitchRange;

                    foreach ((Label Label, VoiceParameterView Param) control in StyleControls)
                    {
                        control.Label.Content = "";
                        control.Param.Value = 0;
                        control.Param.IsEnabled = false;
                        control.Param.Visibility = Visibility.Hidden;
                    }

                    if (preset_dst.Styles.Length <= StyleControls.Count)
                    {
                        for (int i = 0; i < preset_dst.Styles.Length; i++)
                        {
                            string label = preset_dst.Styles[i].Name;
                            // パワフル＝怒り　A
                            // セクシー＝悲しみ S
                            // あたふた＝喜び J
                            if (preset_dst.VoiceName == "itako_emo_44")
                            {
                                switch (label)
                                {
                                    case "J":
                                        label = "あたふた";
                                        break;
                                    case "A":
                                        label = "パワフル";
                                        break;
                                    case "S":
                                        label = "セクシー";
                                        break;
                                }
                            }
                            else
                            {
                                switch (label)
                                {
                                    case "J":
                                        label = "喜び";
                                        break;
                                    case "A":
                                        label = "怒り";
                                        break;
                                    case "S":
                                        label = "悲しみ";
                                        break;
                                }
                            }


                            StyleControls[i].Label.Content = label;
                            StyleControls[i].Param.Value = preset_dst.Styles[i].Value;
                            StyleControls[i].Param.IsEnabled = true;
                            StyleControls[i].Param.Visibility = Visibility.Visible;
                        }
                    }

                    if (preset_dst.Num is int num)
                    {
                        VP_Num.Value = num;
                    }
                    else
                    {
                        VP_Num.Value = 1;
                    }

                    foreach (ComboBoxItem item_src in CB_VoiceList.Items)
                    {
                        if (item_src.Content is VoicePreset preset_src && preset_src.VoiceName == preset_dst.VoiceName)
                        {
                            CB_VoiceList.SelectionChanged -= CB_VoiceList_SelectionChanged;
                            CB_VoiceList.SelectedItem = item_src;
                            CB_VoiceList.SelectionChanged += CB_VoiceList_SelectionChanged;
                        }
                    }
                }
            };
            CB_VoiceList.SelectionChanged += CB_VoiceList_SelectionChanged;

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

            VP_Gain.ValueChanged += (_, _) =>
            {
                PL_PhraseList.MasterVolume = VP_Gain.Value;
            };

            for (int i = 0; i < StyleControls.Count; i++)
            {
                int id = i;
                StyleControls[id].Param.ValueChanged += (sender, e) =>
                {
                    if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                    {
                        if (preset.Styles.Length > id)
                        {
                            preset.Styles[id].Value = (float)StyleControls[id].Param.Value;
                        }
                    }
                };
            }
            VP_Num.ValueChanged += (sender, e) =>
            {
                if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
                {
                    preset.Num = (int)VP_Num.Value;
                }
            };

            TB_PauseShort.TextChanged += TB_PauseShort_TextChanged;
            TB_PauseLong.TextChanged += TB_PauseLong_TextChanged;
            TB_PauseSentence.TextChanged += TB_PauseSentence_TextChanged;

            TB_PauseShort.LostFocus += TB_Pause_LostFocus;
            TB_PauseLong.LostFocus += TB_Pause_LostFocus;
            TB_PauseSentence.LostFocus += TB_Pause_LostFocus;

            CB_PresetList.SelectedIndex = 0;
            PL_PhraseList.SelectedIndex = 0;
            PL_PhraseList.PresetIndex = 0;

            IsEnabled = true;
            IsInit = true;
        }

        private void TB_Pause_LostFocus(object sender, RoutedEventArgs e)
        {
            TB_PauseShort.Text = PL_PhraseList.PauseShort.ToString();
            TB_PauseLong.Text = PL_PhraseList.PauseLong.ToString();
            TB_PauseSentence.Text = PL_PhraseList.PauseSentence.ToString();
        }

        private void TB_PauseSentence_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TB_PauseSentence.Text, out int pause))
            {
                if (pause < 0 || pause > 10000)
                {
                    TB_PauseSentence.Background = Brushes.Red;
                }
                else
                {
                    PL_PhraseList.PauseSentence = pause;
                    TB_PauseSentence.Background = Brushes.White;
                }
            }
            else
            {
                TB_PauseSentence.Background = Brushes.Red;
            }
        }

        private void TB_PauseLong_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TB_PauseLong.Text, out int pause))
            {
                //100~2000
                if (PL_PhraseList.PauseShort > pause || pause < 100 || pause > 2000)
                {
                    TB_PauseLong.Background = Brushes.Red;
                }
                else
                {
                    PL_PhraseList.PauseLong = pause;
                    TB_PauseLong.Background = Brushes.White;
                }
            }
            else
            {
                TB_PauseLong.Background = Brushes.Red;
            }
        }

        private void TB_PauseShort_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TB_PauseShort.Text, out int pause))
            {
                //80~500
                if (PL_PhraseList.PauseLong < pause || pause < 80 || pause > 500)
                {
                    TB_PauseShort.Background = Brushes.Red;
                }
                else
                {
                    PL_PhraseList.PauseShort = pause;
                    TB_PauseShort.Background = Brushes.White;
                }
            }
            else
            {
                TB_PauseShort.Background = Brushes.Red;
            }
        }

        private void CB_VoiceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CB_PresetList.SelectedItem is ComboBoxItem item_dst && item_dst.Content is VoicePreset preset_dst)
            {
                if (CB_VoiceList.SelectedItem is ComboBoxItem item_src && item_src.Content is VoicePreset preset_src)
                {
                    preset_dst.VoiceName = preset_src.VoiceName;
                    preset_dst.Styles = preset_src.Styles;

                    foreach ((Label Label, VoiceParameterView Param) control in StyleControls)
                    {
                        control.Label.Content = "";
                        control.Param.Value = 0;
                        control.Param.IsEnabled = false;
                        control.Param.Visibility = Visibility.Hidden;
                    }

                    if (preset_dst.Styles.Length <= StyleControls.Count)
                    {
                        for (int i = 0; i < preset_dst.Styles.Length; i++)
                        {
                            string label = preset_dst.Styles[i].Name;
                            // パワフル＝怒り　A
                            // セクシー＝悲しみ S
                            // あたふた＝喜び J
                            if (preset_dst.VoiceName == "itako_emo_44")
                            {
                                switch (label)
                                {
                                    case "J":
                                        label = "あたふた";
                                        break;
                                    case "A":
                                        label = "パワフル";
                                        break;
                                    case "S":
                                        label = "セクシー";
                                        break;
                                }
                            }
                            else
                            {
                                switch (label)
                                {
                                    case "J":
                                        label = "喜び";
                                        break;
                                    case "A":
                                        label = "怒り";
                                        break;
                                    case "S":
                                        label = "悲しみ";
                                        break;
                                }
                            }

                            StyleControls[i].Label.Content = label;
                            StyleControls[i].Param.Value = preset_dst.Styles[i].Value;
                            StyleControls[i].Param.IsEnabled = true;
                            StyleControls[i].Param.Visibility = Visibility.Visible;
                        }
                    }

                    item_dst.DataContext = item_src.DataContext;
                }
            }
        }

        private void TB_Preset_Name_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CB_PresetList.SelectedItem == null)
            {
                return;
            }

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

            PL_PhraseList.RenamePreset(CB_PresetList.SelectedIndex);
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

            if (configure is null)
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

            WordDictionary wdic = new(configure.PathWordDictionary);
            PhraseDictionary pdic = new(configure.PathPhraseDictionary);

            wdic_standard = wdic;
            wdic_kansai = wdic;
            pdic_standard = pdic;
            pdic_kansai = pdic;

            UpdatePdic(false, true);
            UpdateWdic(false, true);

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

                UpdateWdic(true, false);
                UpdatePdic(true, false);
            }
        }

        private void UpdateWdic(bool save, bool reload)
        {
            if (wdic_kansai is null || wdic_standard is null)
            {
                throw new();
            }

            if (save)
            {
                wdic_standard.Save();

                if (wdic_standard != wdic_kansai)
                {
                    wdic_kansai.Save();
                }
            }

            if (reload)
            {
                if (File.Exists(wdic_standard.PathDic))
                {
                    APIManager.Dictionary(APIManager.VoiceroidAPI, wdic_standard.PathDic);
                }
                if (File.Exists(wdic_kansai.PathDic))
                {
                    APIManager.DictionaryKansai(APIManager.VoiceroidAPI, wdic_kansai.PathDic);
                }
            }
        }
        private void UpdatePdic(bool save, bool reload)
        {
            if (pdic_kansai is null || pdic_standard is null)
            {
                throw new();
            }

            if (save)
            {
                pdic_standard.Save();

                if (pdic_standard != pdic_kansai)
                {
                    pdic_kansai.Save();
                }
            }

            if (reload)
            {
                if (File.Exists(pdic_standard.PathDic))
                {
                    APIManager.Dictionary(APIManager.VoiceroidAPI, pdic_standard.PathDic);
                }
                if (File.Exists(pdic_kansai.PathDic))
                {
                    APIManager.DictionaryKansai(APIManager.VoiceroidAPI, pdic_kansai.PathDic);
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
            PL_PhraseList.SpeechPhraseList(CB_Cache.IsChecked == true, true);
        }
        private void BT_Stop_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.SpeechCancel();
        }
        private void BT_Output_Click(object sender, RoutedEventArgs e)
        {
            Ext.GCMZDrops.IsEnable = MI_GCMZ.IsChecked;

            if (File.Exists(Ext.GCMZDrops.ProjectPath))
            {
                if (!Directory.Exists(PL_PhraseList.OutputDir))
                {
                    if (CustomMessageBox.Show(this, "Aviutlプロジェクトファイルと同じ場所へ出力しますか？(右クリックで指定を解除できます)", "確認", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                    {
                        if(Directory.GetParent(Ext.GCMZDrops.ProjectPath)?.FullName is string path)
                        {
                            PL_PhraseList.OutputDir = path;
                        }
                    }
                }
            }

            if (!Directory.Exists(PL_PhraseList.OutputDir))
            {
                if (CustomMessageBox.Show(this, "出力先の設定をします。(右クリックで指定を解除できます)", "確認", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                {
                    using CommonOpenFileDialog dialog = new()
                    {
                        Title = "出力フォルダ選択",
                        IsFolderPicker = true,
                    };

                    if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                    {
                        PL_PhraseList.OutputDir = dialog.FileName;
                    }
                    else
                    {
                        CustomMessageBox.Show(this, "出力をキャンセルしました。");
                        return;
                    }
                }
                else
                {
                    CustomMessageBox.Show(this, "出力をキャンセルしました。");
                    return;
                }
            }


            int cnt;

            if (CB_Cache.IsChecked == true)
            {
                cnt = PL_PhraseList.UncachedCheckedPhraseCount;
            }
            else
            {
                cnt = PL_PhraseList.CheckedPhraseCount;
            }

            if (cnt == 0)
            {
                PL_PhraseList.SaveWaves(this, new MyEventArgs());
            }
            else
            {
                MessageBoxResult result = CustomMessageBox.Show(this, $"{cnt}個のフレーズ音声を合成します。よろしいですか？", "確認", MessageBoxButton.OKCancel);

                if (result == MessageBoxResult.OK)
                {
                    PL_PhraseList.SpeechEnd += PL_PhraseList.SaveWaves;
                    PL_PhraseList.SpeechPhraseList(CB_Cache.IsChecked == true, false);
                }
            }
        }
        private void Button_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Directory.Exists(PL_PhraseList.OutputDir))
            {
                PL_PhraseList.OutputDir = "";
                CustomMessageBox.Show(this, "出力先の設定を解除しました。");
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
                TB_Preset_Name.TextChanged -= TB_Preset_Name_TextChanged;
                TB_Preset_Name.Text = preset.PresetName;
                TB_Preset_Name.TextChanged += TB_Preset_Name_TextChanged;
            }
        }
        private void StatusBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (APIManager.Log.Length > 1024)
            {
                CustomMessageBox.Show(this, APIManager.Log[^1024..], "APIs Log");
            }
            else
            {
                CustomMessageBox.Show(this, APIManager.Log, "APIs Log");
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
                UpdateWdic(false, true);

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
            }
        }
        private void BT_WordRemove_Click(object sender, RoutedEventArgs e)
        {
            if (LB_WordList.SelectedItem is ListBoxItem src && src.Content is Word word
                && wdic_standard is not null && wdic_kansai is not null && prev is not null)
            {
                WordDictionary dic = prev.Dialect == TDialect.Standard ? wdic_standard : wdic_kansai;
                dic.RemoveWord(word.Text, word.wordClass);
                dic.Save();
                UpdateWdic(false, true);

                LB_WordList.Items.Remove(src);
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

        private void BT_PhraseSave_Click(object sender, RoutedEventArgs e)
        {
            if (pdic_kansai is null || pdic_standard is null)
            {
                throw new();
            }

            var result = PL_PhraseList.GetCheckedPhraseKana(Brushes.LightGreen, Brushes.LightPink);

            foreach (var (text, kana, dialect) in result)
            {
                if (dialect == TDialect.Standard)
                {
                    pdic_standard.AddPhrase(text, kana);
                }
                else
                {
                    pdic_kansai.AddPhrase(text, kana);
                }
            }

            UpdatePdic(true, true);
        }
        private void BT_PhraseDelete_Click(object sender, RoutedEventArgs e)
        {
            if (pdic_kansai is null || pdic_standard is null)
            {
                throw new();
            }

            var result = PL_PhraseList.GetCheckedPhraseKana(Brushes.LightGray, null);
            foreach (var (text, _, dialect) in result)
            {
                if (dialect == TDialect.Standard)
                {
                    pdic_standard.RemovePhrase(text);
                }
                else
                {
                    pdic_kansai.RemovePhrase(text);
                }
            }

            UpdatePdic(true, true);
        }

        bool close_flag = false;
        bool minimize_flag = false;
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (BT_Close.IsMouseOver)
            {
                close_flag = true;
            }
            else if (BT_Minimize.IsMouseOver)
            {
                minimize_flag = true;
            }
            else if (BT_Topmost.IsMouseOver)
            {
                if (Topmost)
                {
                    Topmost = false;
                    BT_Topmost.Foreground.Opacity = 0.5;
                }
                else
                {
                    Topmost = true;
                    BT_Topmost.Foreground.Opacity = 1.0;
                }
            }
            else if (L_WindowControl.IsMouseOver)
            {
                G_Header.ContextMenu.IsOpen = true;
            }
            else if (expand_dragmove_area || G_Header.IsMouseOver)
            {
                //イベント処理が遅延するとエラーになるのでボタンが押されているか確認
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
        }
        private void BT_Close_MouseEnter(object sender, MouseEventArgs e)
        {
            BT_Close.Background = Brushes.Red;
            BT_Close.Foreground = Brushes.White;
        }
        private void BT_Close_MouseLeave(object sender, MouseEventArgs e)
        {
            BT_Close.Background = Brushes.White;
            BT_Close.Foreground = Brushes.Black;
            close_flag = false;
        }
        private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (close_flag)
            {
                Close();
            }
            if (minimize_flag)
            {
                WindowState = WindowState.Minimized;
            }
        }
        private void BT_Minimize_MouseEnter(object sender, MouseEventArgs e)
        {
            BT_Minimize.Background = new SolidColorBrush(new Color() { A = 255, R = 230, G = 230, B = 230 });
        }
        private void BT_Minimize_MouseLeave(object sender, MouseEventArgs e)
        {
            BT_Minimize.Background = Brushes.White;
            minimize_flag = false;
        }
        private void Window_Activated(object sender, EventArgs e)
        {
            L_WindowControl.Opacity = 1.0;
        }
        private void Window_Deactivated(object sender, EventArgs e)
        {
            L_WindowControl.Opacity = 0.5;
            Mouse.OverrideCursor = Cursors.Arrow;
        }
        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.OverrideCursor == Cursors.SizeAll)
            {
                e.Handled = true;
                Mouse.OverrideCursor = Cursors.Arrow;

                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    DragMove();
                }
            }
        }
        private void BT_AddPreset_Click(object sender, RoutedEventArgs e)
        {
            if (CB_VoiceList.Items[0] is ComboBoxItem item_src)
            {
                if (item_src.Content is VoicePreset preset_normal)
                {
                    VoicePreset preset = (VoicePreset)preset_normal.Clone();

                    preset.PresetName = $"新規ボイス";
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

                    CB_PresetList.SelectedItem = item_dst;

                    if (configure is null)
                    {
                        throw new();
                    }
                    List<VoicePreset> dst = new(configure.Presets);
                    dst.Add(preset);
                    configure.Presets = dst.ToArray();
                }
            }
        }
        private void BT_RemovePreset_Click(object sender, RoutedEventArgs e)
        {
            if (CB_PresetList.Items.Count < 2)
            {
                return;
            }

            if (CB_PresetList.SelectedItem is ComboBoxItem item && item.Content is VoicePreset preset)
            {
                if (PL_PhraseList.RemovePreset(preset))
                {
                    if (CB_PresetList.SelectedIndex > 0)
                    {
                        CB_PresetList.SelectedIndex--;
                    }
                    else
                    {
                        CB_PresetList.SelectedIndex++;
                    }

                    CB_PresetList.Items.Remove(item);

                    if (configure is null)
                    {
                        throw new();
                    }
                    List<VoicePreset> dst = new(configure.Presets);
                    dst.Remove(preset);
                    configure.Presets = dst.ToArray();
                }
                else
                {
                    CustomMessageBox.Show(this, "フレーズ一覧で使用中のボイスプリセットは削除できません");
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BT_UpPreset_Click(object sender, RoutedEventArgs e)
        {
            MovePreset(-1);
        }

        private void BT_DownPreset_Click(object sender, RoutedEventArgs e)
        {
            MovePreset(1);
        }

        private void MovePreset(int move_value)
        {
            if (CB_PresetList.SelectedItem is not ComboBoxItem item || item.Content is not VoicePreset preset)
            {
                return;
            }

            int index = CB_PresetList.SelectedIndex + move_value;

            if (index < 0 || index >= CB_PresetList.Items.Count)
            {
                return;
            }

            CB_PresetList.Items.Remove(item);
            CB_PresetList.Items.Insert(index, item);
            CB_PresetList.SelectedItem = item;

            PL_PhraseList.MovePreset(preset, index);

            if (configure is null)
            {
                throw new();
            }

            List<VoicePreset> dst = new();

            foreach (ComboBoxItem item_src in CB_PresetList.Items)
            {
                dst.Add((VoicePreset)item_src.Content);
            }

            foreach (VoicePreset src in configure.Presets)
            {
                if (!dst.Contains(src))
                {
                    dst.Add(src);
                }
            }

            configure.Presets = dst.ToArray();
        }

        private void MenuItem_CreateNewClick(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new()
            {
                Title = "新規作成",
                RestoreDirectory = true,
            };
            dialog.Filters.Add(new CommonFileDialogFilter("フレーズ辞書ファイル", "*.pdic"));
            dialog.Filters.Add(new CommonFileDialogFilter("全てのファイル", "*.*"));

            // ダイアログを表示する
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                string tmp = Path.GetFullPath(@".\tmp");

                if (Path.GetFullPath(dialog.FileName).Contains(tmp))
                {
                    CustomMessageBox.Show(this, "tmpフォルダの内容はアプリ終了時に削除されるため、他の場所を選択してください", "通知");
                    return;
                }

                if (File.Exists(dialog.FileName))
                {
                    if (CustomMessageBox.Show(this, "既にファイルが存在します。上書きしますか？", "確認", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel)
                    {
                        return;
                    }
                }

                if (configure is not null)
                {
                    configure.PathPhraseDictionary = dialog.FileName;

                    if (File.Exists(configure.PathPhraseDictionary))
                    {
                        File.Delete(configure.PathPhraseDictionary);
                    }

                    pdic_standard = new PhraseDictionary(configure.PathPhraseDictionary);
                    pdic_standard.Save();
                    pdic_kansai = pdic_standard;
                    UpdatePdic(false, true);
                }
            }
            dialog.Dispose();
        }

        private void MenuItem_OpenClick(object sender, RoutedEventArgs e)
        {

            CommonOpenFileDialog dialog = new()
            {
                Title = "フォルダを選択してください",
                IsFolderPicker = true,
            };

            dialog.Filters.Add(new CommonFileDialogFilter("フレーズ辞書ファイル", "*.pdic"));
            dialog.Filters.Add(new CommonFileDialogFilter("全てのファイル", "*.*"));

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (configure is not null)
                {
                    configure.PathPhraseDictionary = dialog.FileName;

                    pdic_standard = new PhraseDictionary(configure.PathPhraseDictionary);
                    pdic_kansai = pdic_standard;
                    UpdatePdic(false, true);
                }
            }

            dialog.Dispose();
        }
        private void MenuItem_CloseClick(object sender, RoutedEventArgs e)
        {
            if (configure is not null)
            {
                if (configure.PathPhraseDictionary == @".\tmp\tmp.pdic")
                {
                    if (CustomMessageBox.Show(this, "現在のフレーズ辞書内容はすべて破棄されますがよろしいですか？", "確認", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                    {
                        File.Delete(configure.PathPhraseDictionary);
                        pdic_standard = new PhraseDictionary(configure.PathPhraseDictionary);
                        pdic_kansai = pdic_standard;

                        UpdatePdic(true, true);
                    }
                }
                else
                {
                    configure.PathPhraseDictionary = @".\tmp\tmp.pdic";
                    if (File.Exists(configure.PathPhraseDictionary))
                    {
                        File.Delete(configure.PathPhraseDictionary);
                    }

                    pdic_standard = new PhraseDictionary(configure.PathPhraseDictionary);
                    pdic_kansai = pdic_standard;

                    UpdatePdic(true, true);
                }
            }
        }

        private void MenuItem_SaveClick(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new()
            {
                Title = "新規作成",
                RestoreDirectory = true,
            };
            dialog.Filters.Add(new CommonFileDialogFilter("フレーズ辞書ファイル", "*.pdic"));
            dialog.Filters.Add(new CommonFileDialogFilter("全てのファイル", "*.*"));

            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                if (configure is not null)
                {
                    configure.PathPhraseDictionary = dialog.FileName;
                    pdic_standard?.Save(configure.PathPhraseDictionary);
                }
            }

            dialog.Dispose();
        }

        private void PL_PhraseList_SpeechEnd(object sender, MyEventArgs e)
        {
            PL_PhraseList.SpeechEnd -= PL_PhraseList_SpeechEnd;
            if(e.Data is Wave wave)
            {
                CustomMessageBox.Show(this, $"max:{wave.GetMaxValue()} ave:{wave.GetAvarageValue()}");
            }
        }

        private void MI_CheckVolume_Click(object sender, RoutedEventArgs e)
        {
            PL_PhraseList.SpeechEnd += PL_PhraseList_SpeechEnd;
            PL_PhraseList.GetSelectedWave();
        }

    }
}
