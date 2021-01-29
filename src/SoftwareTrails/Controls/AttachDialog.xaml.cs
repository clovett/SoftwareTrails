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
using System.Windows.Shapes;
using System.Diagnostics;
using UIController;

namespace SoftwareTrails
{
    /// <summary>
    /// Interaction logic for AttachDialog.xaml
    /// </summary>
    public partial class AttachDialog : Window
    {
        ProfilerControlModel controller;

        public AttachDialog()
        {
            InitializeComponent();

            // so we can handle event raised by double click 
            AddHandler(AttachControl.AttachEvent, new RoutedEventHandler(AttachCommandExecuted));
        }

        public ProfilerControlModel Controller
        {
            get { return controller; }
            set { controller = value; }
        }
        
        private void AttachCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = AttachPanel.SelectedProcess != null && controller.CanAttach;
        }

        private void AttachCommandExecuted(object sender, RoutedEventArgs e)
        {
            TryAttach(AttachPanel.SelectedProcess);
        }

        private void AttachCommandExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Process targetProcess = e.Parameter as Process;
            TryAttach(targetProcess);
        }

        private void TryAttach(Process targetProcess)
        {
            if (targetProcess != null && AttachProcess(controller, targetProcess, false))
            {
                this.DialogResult = true;
                this.Hide();
            }
        }

        public static bool AttachProcess(ProfilerControlModel controller, Process targetProcess, bool wasLaunched)
        {
            uint result = 0;
            try
            {
                result = controller.Attach(targetProcess);
            }
            catch (Exception)
            {
                result = ProfilerErrorCodes.InvalidProcess;
            }
            
            // Concurrent GC is on
            if (result == ClrProfilerConstants.CorProfEConcurrentGCNotProfilable)
            {
                MessageBox.Show("Profiler failed because concurrent GC.", "Attach Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            else if (result != ProfilerErrorCodes.ErrorSuccess)
            {
                if (wasLaunched)
                {
                    MessageBox.Show("Attach failed - " + Properties.Resources.ProfilerNotFound, "Attach Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    MessageBox.Show("Attach failed - probable cause is that the profiler was not enabled for this process, please use the Launch button to setup profiler for this application.", "Attach Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            return result == ProfilerErrorCodes.ErrorSuccess;
        }

    }
}
