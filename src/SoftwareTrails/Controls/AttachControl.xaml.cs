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
using System.ComponentModel;
using System.Diagnostics;
using System.Collections.ObjectModel;
using SoftwareTrails.Utilities;
using System.Windows.Media.Animation;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for AttachControl.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public partial class AttachControl : UserControl, INotifyPropertyChanged
    {
        TypeToFind ttf;

        public AttachControl()
        {
            InitializeComponent();


            _filterControl.FilterValueChanged += OnFilterValueChanged;
            ttf = new TypeToFind(_processesGrid, new FindMatch(FindString));

            this.Loaded += new RoutedEventHandler(AttachControl_Loaded);
        }

        /// <summary>
        /// Find the given string in the items and return the index of matching item or -1.
        /// </summary>
        int FindString(string text)
        {
            int index = 0;
            foreach (Process p in _processesGrid.ItemsSource)
            {
                string name = p.ProcessName;
                if (name.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        void OnFilterValueChanged(object sender, EventArgs e)
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(_processesGrid.ItemsSource);
            view.Filter = GetIncludeFilter();
            view.Refresh();
        }

        Predicate<object> GetIncludeFilter()
        {
            string filter = this.FilterText;
            return (obj) =>
            {
                Process p = obj as Process;
                if (p == null)
                {
                    return false;
                }
                if (string.IsNullOrEmpty(filter)) {
                    return true;
                }
                bool match = p.ProcessName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                return match;
            };
        }

        void AttachControl_Loaded(object sender, RoutedEventArgs e)
        {
            // BeginInvoke adds another level of asyncronicity so the dialog can appear before the process list is fetched.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                UpdateProcessList();
                _processesGrid.Focus();
            }));
        }

        private void UpdateProcessList()
        {
            _processesGrid.ItemsSource = new ObservableCollection<Process>(this.Processes);
        }

        void buttonRefresh_Click(object sender, RoutedEventArgs e)
        {
            UpdateProcessList();
            OnFilterValueChanged(sender, e); // re-apply filter.
            if (!string.IsNullOrEmpty(this.FilterText))
            {
                // user might have forgotten they had filter applied, so flash the background to remind them.
                var brush = new SolidColorBrush(Colors.Yellow);
                _filterControl.Background = brush;
                brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(Colors.Yellow, Colors.Transparent, new Duration(TimeSpan.FromSeconds(3)), FillBehavior.HoldEnd));
            }
        }

        public List<Process> Processes
        {
            get
            {
                if (_processes == null)
                {
                    Processes = new List<Process>(GetProcesses());
                }
                return _processes;
            }
            private set
            {
                _processes = value;
                OnPropertyChanged("Processes");
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
        static IEnumerable<Process> GetProcesses()
        {
            using (var currentProcess = Process.GetCurrentProcess())
            {
                // Capture the pid since we will dispose of currentProcess before the lambda expression is evaluated.
                var pid = currentProcess.Id;
                List<Process> result = new List<Process>();
                foreach (Process p in Process.GetProcesses()){
                    try {
                        if (p.Id != pid)
                        {
                            result.Add(p);
                        }
                    } catch (Exception) {
                    }
                }

                result.Sort(new Comparison<Process>((a, b) =>
                {
                    return a.ProcessName.CompareTo(b.ProcessName);
                }));

                return result;
            }
        }

        public string FilterText
        {
            get
            {
                return _filterControl.FilterText;
            }
            set
            {
                _filterControl.FilterText = value;
            }
        }

        void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        public Process SelectedProcess
        {
            get
            {
                return _processesGrid.SelectedValue as Process;
            }
        }

        public static readonly RoutedCommand AttachCommand = new RoutedCommand("Attach", typeof(AttachControl));

        public static readonly RoutedEvent AttachEvent = System.Windows.EventManager.RegisterRoutedEvent(AttachCommand.Name,
                    RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(AttachControl));

        public event PropertyChangedEventHandler PropertyChanged;

        List<Process> _processes;

        private void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
        {
            Apply();
        }

        private void Apply()
        {

            if (SelectedProcess != null)
            {
                RaiseEvent(new RoutedEventArgs(AttachEvent));
            }
        }

        private void OnGridKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Apply();
            }
        }
    }
}
