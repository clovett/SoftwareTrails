using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using UIController;
using System.IO.MemoryMappedFiles;
using System.Collections.Concurrent;
using System.Windows.Media.Animation;
using System.Reflection;
using System.Xml.Linq;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public partial class MainWindow : Window
    {        
        private ProfilerControlModel controller;
        private DispatcherTimer timer;
        private StackWatcher watcher;
        private Settings settings;
        private TemporaryFileCollection tempFiles = new TemporaryFileCollection();
        private Dispatcher dispatcher;

        public static readonly RoutedCommand LaunchCommand;
        public static readonly RoutedCommand ConnectCommand;
        public static readonly RoutedCommand ClearCommand;
        public static readonly RoutedCommand SortCommand;
        public static readonly RoutedCommand DgmlCommand;
        public static readonly RoutedCommand UpdateCommand;

        static MainWindow()
        {
            LaunchCommand = new RoutedUICommand("Launch", "Launch", typeof(MainWindow));
            ConnectCommand = new RoutedUICommand("Connect", "Connect", typeof(MainWindow));
            ClearCommand = new RoutedUICommand("Clear", "Clear", typeof(MainWindow));
            SortCommand = new RoutedUICommand("Sort", "Sort", typeof(MainWindow));
            DgmlCommand = new RoutedUICommand("Dgml", "Dgml", typeof(MainWindow));
            UpdateCommand = new RoutedUICommand("Update", "Update", typeof(MainWindow));
        }

        public MainWindow()
        {
            InitializeComponent();

            this.dispatcher = this.Dispatcher;

            LoadConfig();

            if (string.IsNullOrEmpty(settings.ProfilerOutputDirectory))
            {
                settings.ProfilerOutputDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ProfilerOutput");
            }

            Map.FilterChanged += OnFilterChanged;
            CallStackControl.Closed += OnCallStackViewClosed;
            IncludeFilter.FilterValueChanged += OnFilterValueChanged; 
            ExcludeFilter.FilterValueChanged += OnFilterValueChanged;
            CallStackControl.View.EmptyChanged += OnEmptyChanged;
            Map.EmptyChanged += new EventHandler(OnEmptyChanged);

            this.Background = (Brush)FindResource("InactiveBackground");

            this.AddHandler(CodeBlock.SelectionChangedEvent, new RoutedEventHandler(OnSelectionChanged));
            this.AddHandler(CodeBlock.GotKeyboardFocusEvent, new RoutedEventHandler(OnCodeBlockFocusChanged));

            // so the CommandEnabledContexts get updated correctly.
            CommandStatusWatcher.OnCommandStatusChanged();

            this.Loaded += OnMainWindowLoaded;

        }

        void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            CheckLastVersion();
        }

        void OnEmptyChanged(object sender, EventArgs e)
        {
            CommandStatusWatcher.OnCommandStatusChanged();
        }

        void OnCallStackViewClosed(object sender, EventArgs e)
        {
            Map.Selection = null;
        }

        void OnFilterChanged(object sender, EventArgs e)
        {
        }

        void OnWatcherRewind(object sender, EventArgs e)
        {
            Map.ResetCounters();
        }

        void ReplayHistory()
        {
            if (watcher != null)
            {
                // start over.
                watcher.Rewind();
            }
        }

        void OnFilterValueChanged(object sender, EventArgs e)
        {
            if (sender == IncludeFilter)
            {
                Map.IncludeFilter = IncludeFilter.FilterText;
                Map.ClearBlocks();
            }
            else
            {
                Map.ExcludeFilter = IncludeFilter.FilterText;
                Map.ClearBlocks();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            CallStackControl.View.Terminate();
            base.OnClosed(e);
            StopTimer();
            SaveConfig();
        }

        void LoadConfig()
        {
            settings = new Settings();

            if (File.Exists(ConfigFilename))
            {
                settings.Load(ConfigFilename);

                tempFiles.AddRange(settings.TemporaryFiles);
                tempFiles.CleanupTempFiles();

                Point location = settings.WindowLocation;
                if (settings.WindowSize.Width > 0)
                {
                    Rect bounds = new Rect(SystemParameters.VirtualScreenLeft,
                        SystemParameters.VirtualScreenTop,
                        SystemParameters.VirtualScreenWidth,
                        SystemParameters.VirtualScreenHeight);

                    Rect windowBounds = new Rect(location.X, location.Y,
                        settings.WindowSize.Width, settings.WindowSize.Height);

                    // Make sure this bounds is still legit (multimonitor setup may have changed).
                    if (windowBounds.Left < bounds.Left)
                    {
                        windowBounds.X = bounds.Left;
                    }
                    if (windowBounds.Top < bounds.Top)
                    {
                        windowBounds.Y = bounds.Top;
                    }
                    if (windowBounds.Right > bounds.Right)
                    {
                        windowBounds.Width = bounds.Right - windowBounds.Left;
                    }
                    if (windowBounds.Bottom > bounds.Bottom)
                    {
                        windowBounds.Height = bounds.Bottom - windowBounds.Top;
                    }

                    if (windowBounds.Width > 100 && windowBounds.Height > 100)
                    {
                        this.Left = windowBounds.X;
                        this.Top = windowBounds.Y;
                        this.Width = windowBounds.Width;
                        this.Height = windowBounds.Height;
                    }
                }

            }
        }

        void SaveConfig()
        {
            Settings s = this.settings;
            s.WindowLocation = new Point((int)this.Left, (int)this.Top);
            s.WindowSize = new Size((int)this.Width, (int)this.Height);
            s.TemporaryFiles = tempFiles.CleanupTempFiles();
            s.Save(ConfigFilename);
        }

        internal static string ConfigFilename
        {
            get
            {
                return String.Format("SoftwareTrails_{0}.config", Environment.Is64BitProcess ? "x64" : "x86");
            }
        }

        private void OnLaunchCommand(object sender, ExecutedRoutedEventArgs e)
        {
            OpenFileDialog od = new OpenFileDialog();
            od.Filter = "Executable Files (*.exe)|*.exe";
            if (od.ShowDialog() == true)
            {
                LaunchProcess(od.FileName);
            }
        }

        /// <summary>
        /// Not used yet, since we're not currently looking at object allocation.
        /// </summary>
        /// <param name="exe"></param>
        /// <returns></returns>
        private bool CheckConfig(string exe)
        {
            TargetConfiguration targetConfig = new TargetConfiguration(exe);
            targetConfig.Load();
            if (targetConfig.ConcurrentGC)
            {
                if (MessageBox.Show("The .config file needs to be edited to turn off concurrent garbage collection, click Ok to continue", "Permission to edit config file", MessageBoxButton.OKCancel, MessageBoxImage.Exclamation)
                    != MessageBoxResult.OK)
                {
                    return false;
                }
                targetConfig.ConcurrentGC = false;
                try
                {
                    FileUtilities.MakeReadWrite(targetConfig.ConfigPath);
                    targetConfig.Save();
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error saving updated configuration\r\n" + e.Message, "Error Saving Configuration", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
            }
            return true;
        }


        private void LaunchProcess(string exe)
        {
            Detach();

            // make sure file is not locked.
            string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetFileName(exe));
            File.Copy(exe, temp, true);

            bool is64bit = false;

            using (MinPEFileReader reader = new MinPEFileReader(temp))
            {
                if (!reader.IsExe)
                {
                    MessageBox.Show("The selected file does not appear to be executable", "Error Launching Process", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!reader.IsManaged && reader.Requires64Bits)
                {
                    is64bit = true;
                    //MessageBox.Show("The program is native 64bit which is not supported by this profiler, yet", "64bit not supported", MessageBoxButton.OK, MessageBoxImage.Error);
                    //return;
                }

                if (reader.IsManaged && reader.ClrMetadataVersion != null)
                {
                    Version v = new Version(reader.ClrMetadataVersion.Substring(1));
                    if (v < new Version(2, 0))
                    {
                        MessageBox.Show(
                            string.Format("The managed version {0} is not supported. The target application needs to be using .NET 2.0 or greater.", reader.ClrMetadataVersion),
                            ".NET Version Not Supported", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                if (reader.IsManaged)
                {
                    is64bit = !reader.IsManaged32Required;
                }

            }

            string profiler = settings.ProfilerDllPath = SelfInstaller.InstallProfiler(settings.ProfilerDllPath, is64bit);
            if (string.IsNullOrEmpty(profiler))
            {
                // user chose not to register it.
                return;
            }
            


            StringBuilder buffer = new StringBuilder();
            string arguments = null;
            try
            {
                ProcessStartInfo pi = new ProcessStartInfo(exe);
                pi.Arguments = arguments;
                pi.EnvironmentVariables.Add("COR_PROFILER", "{D795A307-4F19-4E49-B714-8641DF72F493}");
                pi.EnvironmentVariables.Add("COR_PROFILER_PATH", profiler);
                pi.EnvironmentVariables.Add("COR_ENABLE_PROFILING", "1");

                // If we're debugging then we might want the profiler attach prompt so we can debug the profiler also.
                if (!System.Diagnostics.Debugger.IsAttached)
                {
                    pi.EnvironmentVariables.Add("COR_PROFILER_ATTACHING", "1");
                }
                pi.CreateNoWindow = true;
                pi.UseShellExecute = false;// must be false in order to pass environment variables.
                pi.RedirectStandardError = true;

                bool attached = false;
                bool waiting = false;
                Process p = Process.Start(pi);

                p.Exited += new EventHandler((s, e) =>
                {
                    if (!attached && waiting)
                    {
                        Dispatcher.Invoke(new Action(() =>
                        {
                            MessageBox.Show("Process already exited with code " + p.ExitCode + Environment.NewLine + buffer.ToString(), "Error Attaching Process", MessageBoxButton.OK, MessageBoxImage.Error);
                        }));
                    }
                });

                p.ErrorDataReceived += new DataReceivedEventHandler((s,e) => {
                    if (!attached && !string.IsNullOrWhiteSpace(e.Data))
                    {
                        buffer.AppendLine(e.Data);
                    }
                });
                p.BeginErrorReadLine();

                System.Threading.Thread.Sleep(1000);

                if (p.HasExited)
                {
                    throw new Exception("Process already exited with code " + p.ExitCode);
                }
                else
                {
                    waiting = true;
                    Attach(p);
                    attached = true;
                }

            } 
            catch (System.TimeoutException ex) 
            {
                MessageBox.Show(ex.Message + Environment.NewLine + Properties.Resources.ProfilerNotFound, "Error Attaching Process", MessageBoxButton.OK, MessageBoxImage.Error);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + Environment.NewLine + buffer.ToString(), "Error Attaching Process", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnConnectCommand(object sender, ExecutedRoutedEventArgs e)
        {
            bool wasAttached = false;
            if (controller != null)
            {
                wasAttached = controller.IsAttached;
                Detach();
            }
            if (!wasAttached)
            {
                try
                {
                    Attach(null);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Connect Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Detach()
        {
            using (watcher)
            {
                watcher = null;
            }
            if (controller != null)
            {
                using (controller)
                {
                    controller.PropertyChanged -= new System.ComponentModel.PropertyChangedEventHandler(OnControllerPropertyChanged);
                    controller.Detach();
                    controller = null;
                }
            }

            ConnectButtonIcon.Source = (ImageSource)FindResource("DisconnectedIcon");
            ConnectButtonLabel.Text = "Connect";
            StopTimer();

            this.Background = (Brush)FindResource("InactiveBackground");

            CommandStatusWatcher.OnCommandStatusChanged();
        }

        private void OnClearCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Clear();
        }

        private void Clear()
        {
            ShowError("");
            try
            {
                if (controller != null && controller.IsAttached)
                {
                    controller.Clear();
                }
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }

            if (watcher != null)
            {
                watcher.Clear();
            }

            Map.ClearBlocks();
            CallStackControl.View.Watching = null;
            ClearProgress(); 
            CommandStatusWatcher.OnCommandStatusChanged();
        }

        private void CanClear(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (controller != null || (Map != null && Map.HasBlocks));
        }

        private void Attach(Process launched)
        {
            bool is64Bit = Environment.Is64BitOperatingSystem && (NativeMethods.IsWow64Process(launched) != NativeMethods.IsWow64.Yes);

            string profiler = settings.ProfilerDllPath = SelfInstaller.InstallProfiler(settings.ProfilerDllPath, is64Bit);
            
            if (string.IsNullOrEmpty(profiler))
            {
                // user chose not to register it.
                return;
            }

            controller = new ProfilerControlModel();
            controller.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(OnControllerPropertyChanged);

            try
            {
                if (launched == null)
                {
                    AttachDialog d = new AttachDialog();
                    d.Owner = this;
                    d.Controller = this.controller;
                    if (d.ShowDialog() == true)
                    {
                        // good then we'll hook up below...
                    }
                }
                else
                {
                    AttachDialog.AttachProcess(controller, launched, true);
                }
            }
            catch
            {
                // woops, something blew, so cleanup controller.
                Detach();
                throw;
            }

            if (controller.IsAttached)
            {
                
                ConnectButtonIcon.Source = (ImageSource)FindResource("ConnectedIcon");
                ConnectButtonLabel.Text = "Disconnect";

                StartTimer();


                this.Background = new SolidColorBrush(Colors.White);
                Map.ClearBlocks();

                watcher = new StackWatcher(controller, CallStackControl.View);
                watcher.Rewound += new EventHandler(OnWatcherRewind);
            }

            CommandStatusWatcher.OnCommandStatusChanged();
        }

        void OnControllerPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsAttached")
            {
                if (!controller.IsAttached )
                {
                    // this could be on the Process.IsExited thread, which is not the UI thread.
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Detach();
                    }));
                }
            }
        }


        CodeBlock focus;


        void OnCodeBlockFocusChanged(object sender, RoutedEventArgs e)
        {
            CodeBlock newFocus = e.OriginalSource as CodeBlock;
            if (focus != newFocus)
            {
                focus = newFocus;
                CommandStatusWatcher.OnCommandStatusChanged();
            }
        }


        void OnSelectionChanged(object sender, RoutedEventArgs e)
        {
            CodeBlock block = (CodeBlock)e.OriginalSource;

            bool codeBlockSelection = (block.View == Map);
            if (codeBlockSelection)
            {
                if (block.IsSelected)
                {
                    CallStackControl.View.Watching = block.Method;

                    // now replay so we can capture these call stacks.
                    ReplayHistory();
                }
                else
                {
                    CallStackControl.View.Watching = null;
                }
            }
        }


        void ClearProgress()
        {
            FunctionStatus.Text = "";
            CallStatus.Text = "";
            Progress.Maximum = 100;
            Progress.Value = 0;
            Progress.Visibility = System.Windows.Visibility.Collapsed;
        }

        long lastVersion;

        void OnTick(object sender, EventArgs e)
        {
            if (controller != null && controller.IsAttached)
            {
                Tuple<long, long, long> result = controller.LookupStats();
                if (result != null)
                {
                    long functions = result.Item1;
                    long calls = result.Item2;
                    long version = result.Item3;
                    if (version != lastVersion)
                    {
                        lastVersion = version;
                        controller.WrapAround();
                    }

                    Progress.Maximum = calls;
                    Progress.Value = watcher.CallsRead;
                    Progress.Visibility = System.Windows.Visibility.Visible;

                    FunctionStatus.Text = functions.ToString("N0");
                    CallStatus.Text = calls.ToString("N0");
                }

                watcher.BatchUpdate(Map);
            }
            else
            {
                timer.Stop();
                timer = null;
            }
        }

        void StartTimer()
        {
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(1), DispatcherPriority.Normal, OnTick, Dispatcher);
            timer.Start();
        }

        void StopTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }            
        }

        private void CanLaunch(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (controller == null || !controller.IsAttached);
        }

        private void CanConnect(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = true;
        }

        private void CanSort(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (Map != null && Map.HasBlocks);
        }

        private void OnSortCommand(object sender, ExecutedRoutedEventArgs e)
        {
            Map.Sort();
        }

        private void OnCut(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void CanCut(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = focus != null;
        }

        private void OnCopy(object sender, ExecutedRoutedEventArgs e)
        {
            if (focus != null)
            {
                Clipboard.SetText(focus.Method.FullName);
            }
        }

        private void CanCopy(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = focus != null;
        }

        private void OnDelete(object sender, ExecutedRoutedEventArgs e)
        {

        }

        private void CanDelete(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = focus != null;
        }

        private void CanRefresh(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (controller != null && controller.IsAttached);
        }

        private void OnRefresh(object sender, ExecutedRoutedEventArgs e)
        {
            ReplayHistory();
        }

        private void OnShowDgml(object sender, ExecutedRoutedEventArgs e)
        {
            string temp = GetTempDgmlFileName();
            CallStackControl.View.Graph.Save(temp);
            OpenDgmlFile(temp);
            tempFiles.Add(temp);
        }

        private void OpenDgmlFile(string temp)
        {
            FileUtilities.OpenUrl(IntPtr.Zero, new Uri(temp));
        }

        private string GetTempDgmlFileName()
        {
            string tempPath = System.IO.Path.GetTempPath();
            int i = 0;
            do
            {
                string dgml = System.IO.Path.Combine(tempPath, "callstack" + i + ".dgml");
                if (!File.Exists(dgml))
                {
                    return dgml;
                }
                i++;
            } while (true);
        }

        private void CanShowDgml(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (CallStackControl != null && CallStackControl.View.Watching != null && 
                !CallStackControl.View.Graph.IsEmpty);
        }


        ChangeListRequest changeList;

        bool updateAvailable;

        private const string AppName = "SoftwareTrails.application";
        private static Uri DownloadSite = new Uri("https://lovettsoftwarestorage.blob.core.windows.net/downloads/SoftwareTrails/");

        /// <summary>
        ///  Check if version has changed, and if so show version update info.
        /// </summary>
        private void CheckLastVersion()
        {
            changeList = new ChangeListRequest(AppName, this.settings);
            changeList.Completed += new EventHandler<SetupRequestEventArgs>(OnChangeListRequestCompleted);
            changeList.BeginGetChangeList(DownloadSite);

            // and see if we just installed a new version.
            string exe = FileUtilities.MainExecutable;
            DateTime lastWrite = File.GetLastWriteTime(exe);
            DateTime? lastExeTime = settings.LastExeTimestamp;
            // we use ToString() since we only need second level granularity
            if (lastExeTime == null || lastWrite.ToString() != lastExeTime.Value.ToString())
            {
                string previous = settings.ExeVersion;
                ShowChangeInfo(previous, null, false);
            }

            // save new settings.
            settings.ExeVersion = FileUtilities.GetFileVersion(AppName, exe);
            settings.LastExeTimestamp = lastWrite;
        }

        private void OnChangeListRequestCompleted(object sender, SetupRequestEventArgs e)
        {
            XDocument changes = e.Changes;
            if (changes != null && e.NewVersionAvailable)
            {
                dispatcher.Invoke(new Action(() =>
                {
                    updateAvailable = true;
                    CommandManager.InvalidateRequerySuggested();
                }));
            }
        }

        private void ShowChangeInfo(string previousVersion, XDocument changeDoc, bool installButton)
        {
            if (changeDoc == null)
            {
                changeDoc = changeList.GetBuiltInList();
            }
            if (changeDoc != null)
            {
                FlowDocumentScrollViewer view = this.FlowDocumentViewer;
                view.Visibility = Visibility.Visible;

                var document = view.Document;
                document.Blocks.Clear();

                CloseBox box = new CloseBox();
                box.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;                
                box.Click += OnCloseFlowDocumentViewClick;
                document.Blocks.Add(new BlockUIContainer(box));

                ChangeInfoFormatter report = new ChangeInfoFormatter(previousVersion, changeDoc);
                report.Generate(document);
                
            }
        }

        void OnCloseFlowDocumentViewClick(object sender, RoutedEventArgs e)
        {
            FlowDocumentScrollViewer view = this.FlowDocumentViewer;
            view.Visibility = Visibility.Hidden;
        }

        private void OnInstallUpdate(object sender, ExecutedRoutedEventArgs e)
        {
            FileUtilities.OpenUrl(IntPtr.Zero, new Uri(DownloadSite, AppName));
            this.Close();
        }

        private void CanInstallUpdate(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = updateAvailable;
        }

        void ShowError(string msg)
        {
            Dispatcher.Invoke(new Action(() =>
            {
                ErrorMessage.Text = msg;
            }));
        }
    }
}
