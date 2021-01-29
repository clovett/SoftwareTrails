using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for QuickFilterControl.xaml
    /// </summary>
    public partial class QuickFilterControl : UserControl
    {
        public event EventHandler FilterValueChanged;

        private DispatcherTimer _timer;

        public QuickFilterControl()
        {
            this.InitializeComponent();
        }

        public string FilterText
        {
            get
            {
                return this.InputFilterText.Text;
            }
            set
            {
                this.InputFilterText.Text = value;
            }
        }

        private void OnTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TextBox tv = sender as TextBox;
                if (tv != null)
                {
                    FiterEventTextChanged(tv.Text);
                }
            }

        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(300), DispatcherPriority.Background, OnTimerTick, this.Dispatcher);
                _timer.Start();
            }
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            StopTimer();
            var box = InputFilterText;
            FiterEventTextChanged(box.Text);
        }

        private void FiterEventTextChanged(string filter)
        {
            if (FilterValueChanged != null)
            {
                FilterValueChanged(this, EventArgs.Empty);
            }
        }

        private void OnInputFilterText_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox tb = sender as TextBox;
            if (tb != null && string.IsNullOrWhiteSpace(tb.Text) == false)
            {
                ClearFilter.Visibility = System.Windows.Visibility.Visible;
            }
            else
            {
                ClearFilter.Visibility = System.Windows.Visibility.Collapsed;
            }
            StartTimer();
        }


        private void OnClearFilterButton_Closed(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            this.InputFilterText.Text = string.Empty;
            FiterEventTextChanged(this.InputFilterText.Text);
        }
    }

}
