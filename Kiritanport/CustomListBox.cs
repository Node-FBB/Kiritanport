using Kiritanport.Voiceroid;
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
    internal class CustomListBox : ListBox
    {
        private bool processing = false;

        public event MyEventHandler? OnSpeech;
        public event MyEventHandler? TextSelected;
        public event MyEventHandler? KanaSelected;
        private event MyEventHandler? HidePhraseEditViewSignal;

        public TAccentProvider AccentProvider { set; get; } = TAccentProvider.Voiceroid;

        public bool AccentLock = false;

        public CustomListBox()
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
        public void SpeechText(ListBoxItem item)
        {
            received_item = item;

            if (item.Content is Grid g
                && g.Children[0] is ComboBox cb
                && g.Children[1] is TextBox tb)
            {
                //item.Background = Brushes.YellowGreen;
                item.IsSelected = true;

                if (cb.SelectedItem is ComboBoxItem citem 
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

                    string text = tb.Text;

                    if (item.DataContext is PhraseEditView view)
                    {
                        string kana = view.GetAIKANA();

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
                    }
                    speech.Text = text;
                    OnSpeech?.Invoke(this, new MyEventArgs() { Data = speech });
                }
                else
                {
                    processing = false;
                }
            }
        }

        private bool prev_speech = false;
        private ListBoxItem? received_item;
        private void MainWindow_KanaReceived(object sender, MyEventArgs e)
        {
            APIManager.KanaReceived -= MainWindow_KanaReceived;

            if (received_item?.DataContext is PhraseEditView view)
            {
                if (e.Data is string text)
                {
                    if (text.Length > 0)
                    {
                        view.SetAIKANA(text);

                        if (prev_speech)
                        {
                            if (received_item.Content is Grid g
                                && g.Children[0] is ComboBox cb
                                && cb.SelectedItem is ComboBoxItem citem)
                            {
                                APIManager.Speech(citem.DataContext, text);
                            }
                        }
                    }
                }
            }
            received_item = null;
            prev_speech = false;
        }

        public void SpeechTexts()
        {
            SynchronizationContext? context = SynchronizationContext.Current;

            Task.Factory.StartNew(async () =>
            {
                foreach (ListBoxItem item in Items)
                {
                    processing = true;

                    context?.Send(_ => SpeechText(item), null);

                    while (processing)
                    {
                        await Task.Delay(10);
                    }
                }
            });
        }

        private readonly List<(VoicePreset preset, Binding binding)> bindlist = new();

        public void AddPreset(VoicePreset preset, Binding binding)
        {
            bindlist.Add((preset, binding));

            foreach (ListBoxItem item in Items)
            {
                if (item.Content is Grid g && g.Children[0] is ComboBox cb)
                {
                    ComboBoxItem item_dst = new() { Content = preset };
                    item_dst.SetBinding(DataContextProperty, binding);

                    cb.Items.Add(item_dst);
                }
            }
        }

        public void Rename(int index)
        {
            foreach (ListBoxItem item in Items)
            {
                if (item.Content is Grid g && g.Children[0] is ComboBox cb)
                {
                    if (cb.Items[index] is ComboBoxItem citem)
                    {
                        var content = citem.Content;
                        citem.Content = null;
                        citem.Content = content;
                    }

                    if (index == cb.SelectedIndex)
                    {
                        cb.SelectedIndex = -1;
                        cb.SelectedIndex = index;
                    }
                }
            }
        }
        public void AddLine(bool focus)
        {
            PhraseEditView view = new()
            {
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left,
                Visibility = Visibility.Collapsed
            };

            view.SelectionChanged += TB_Kana_SelectionChanged;

            HidePhraseEditViewSignal += (sender, e) =>
            {
                view.Visibility = Visibility.Collapsed;
            };

            if (Parent is Grid parent)
            {
                parent.Children.Add(view);
            }

            Grid grid = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            ListBoxItem item = new()
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Content = grid,
                Focusable = false,
                DataContext = view,
            };

            Items.Add(item);

            ComboBox cb = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 100,
                Focusable = false,
            };

            foreach ((VoicePreset preset, Binding binding) in bindlist)
            {
                ComboBoxItem cbitem = new() { Content = preset };
                cbitem.SetBinding(DataContextProperty, binding);
                cb.Items.Add(cbitem);
            }

            TextBox tb = new() { Margin = new Thickness(100, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
            view.DataContext = item;

            item.Selected += (_, _) =>
            {
                tb.Focus();
            };

            tb.SelectionChanged += TB_Text_SelectionChanged;

            tb.GotFocus += (_, _) =>
            {
                if (SelectedItem != item)
                {
                    SelectedItem = item;
                }
            };

            tb.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Down)
                {
                    e.Handled = true;
                    int index = Items.IndexOf(item);

                    if (index < Items.Count - 1)
                    {
                        if (Items[index + 1] is ListBoxItem litem && litem.Content is Grid g)
                        {
                            g.Children[1].Focus();
                        }
                    }
                }
                if (e.Key == Key.Up)
                {
                    e.Handled = true;
                    int index = Items.IndexOf(item);

                    if (index > 0)
                    {
                        if (Items[index - 1] is ListBoxItem litem && litem.Content is Grid g)
                        {
                            g.Children[1].Focus();
                        }
                    }
                }
            };

            tb.TextChanged += (sender, e) =>
            {
                if (AccentLock)
                {
                    if (tb.Text == "")
                    {
                        view.Clear();
                    }
                }
                else
                {
                    view.Clear();
                }
            };

            tb.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    view.Clear();
                }

                if (e.Key == Key.Enter)
                {
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        AddLine(true);
                    }
                    else
                    {
                        SpeechText(item);
                    }
                }
                if (e.Key == Key.Tab)
                {
                    e.Handled = true;

                    if (view.Visibility == Visibility.Collapsed)
                    {
                        if (Parent is Grid g)
                        {
                            Point p = tb.TranslatePoint(new Point(0, 0), g);
                            view.Margin = new Thickness(p.X, p.Y + tb.ActualHeight, 0, 0);
                            view.Visibility = Visibility.Visible;
                        }
                    }
                    else
                    {
                        view.Visibility = Visibility.Collapsed;
                    }
                }
            };

            if (focus)
            {
                tb.Loaded += (sender, e) =>
                {
                    tb.Focus();
                };
            }

            grid.Children.Add(cb);
            grid.Children.Add(tb);
        }

        private void TB_Text_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (e.Source is TextBox tb)
            {
                string text = tb.SelectedText;

                if (text.Length > 0)
                {
                    TextSelected?.Invoke(this, new MyEventArgs() { Data = text });
                }
            }
        }

        private void TB_Kana_SelectionChanged(object sender, MyEventArgs e)
        {
            if (e.Data is string text)
            {
                if (text.StartsWith("<S>") && text.EndsWith("<N>"))
                {
                    KanaSelected?.Invoke(this, new MyEventArgs() { Data = text[3..^3] });
                }
            }
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
                });
            }
        }
    }
}
