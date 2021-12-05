using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace Kiritanport
{
    internal class NumericUpDown : StackPanel
    {
        private readonly TextBox tbox;
        private readonly ScrollBar sbar;
        public RoutedPropertyChangedEventHandler<double>? ValueChanged;

        public double Value
        {
            set
            {
                sbar.Value = value;
            }
            get
            {
                return sbar.Value;
            }
        }
        public double SmallChange
        {
            set
            {
                sbar.SmallChange = value;
            }
            get
            {
                return sbar.SmallChange;
            }
        }
        public double LargeChange
        {
            set
            {
                sbar.LargeChange = value;
            }
            get
            {
                return sbar.LargeChange;
            }
        }
        public double Minimum
        {
            set
            {
                sbar.Minimum = value;
            }
            get
            {
                return sbar.Minimum;
            }
        }
        public double Maximum
        {
            set
            {
                sbar.Maximum = value;
            }
            get
            {
                return sbar.Maximum;
            }
        }

        public NumericUpDown()
        {
            Orientation = Orientation.Horizontal;
            HorizontalAlignment = HorizontalAlignment.Center;

            tbox = new TextBox()
            {
                Height = 21,
                Width = 30,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Center,
                Focusable = false,
                ContextMenu = null,
            };


            sbar = new ScrollBar()
            {
                Value = 1,
                SmallChange = 0.01,
                LargeChange = 0.1,
                Minimum = 0.5,
                Maximum = 2.0,
                RenderTransformOrigin = new(0.5, 0.5),
            };

            TransformGroup tgroup = new();
            tgroup.Children.Add(new RotateTransform(180));
            tgroup.Children.Add(new ScaleTransform(1.0, 0.666));
            sbar.RenderTransform = tgroup;

            Binding binding = new()
            {
                Source = sbar,
                Mode = BindingMode.Default,
                Path = new PropertyPath("Value")
            };

            tbox.SetBinding(TextBox.TextProperty, binding);

            Children.Add(tbox);
            Children.Add(sbar);

            tbox.MouseWheel += NumericScroll;
            sbar.MouseWheel += NumericScroll;

            sbar.ValueChanged += Sbar_ValueChanged;
        }

        private void Sbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            sbar.Value = Math.Round(sbar.Value, 2);

            ValueChanged?.Invoke(sender, e);
        }

        //マウスホイールを回転させた時にScrollBarの値を上下させる
        private void NumericScroll(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta > 0)
            {
                sbar.Value += sbar.LargeChange;
            }
            else
            {
                sbar.Value -= sbar.LargeChange;
            }
        }
    }
}
