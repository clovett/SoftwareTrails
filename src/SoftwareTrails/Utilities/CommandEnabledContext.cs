using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SoftwareTrails
{
    /// <summary>
    /// This class exists because on .NET 4.0 the CommandManager.RequerySuggested event isn't working.
    /// And the Command.CanExecuteChanged is also not working.
    /// </summary>
    public class CommandStatusWatcher
    {
        public static CommandStatusWatcher Instance;

        static CommandStatusWatcher()
        {
            Instance = new CommandStatusWatcher();
        }

        public event EventHandler CommandStatusChanged;

        /// <summary>
        /// Call this on the CommandStatusWatcher any time the command status changes
        /// </summary>
        public static void OnCommandStatusChanged()
        {
            Instance.RaiseEvent();
            CommandManager.InvalidateRequerySuggested(); 
        }

        private void RaiseEvent()
        {
            if (CommandStatusChanged != null)
            {
                CommandStatusChanged(this, EventArgs.Empty);
            }
        }

    }

}
