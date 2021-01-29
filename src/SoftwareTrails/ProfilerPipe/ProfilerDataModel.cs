using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO;
using System.Runtime.InteropServices;

namespace SoftwareTrails
{
    /// <summary>
    /// Combination of Model and ViewModel for profiler data.
    /// </summary>
    public abstract class ProfilerDataModel : INotifyPropertyChanged
    {
        /// <summary>
        /// Reset the model.
        /// </summary>
        public abstract void Clear();

        protected void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;

    }

    /// <summary>
    /// Combination of Model and ViewModel for profiler data.
    /// </summary>
    public abstract class ProfilerDataModel<T> : ProfilerDataModel
        where T : struct
    {
        protected ProfilerDataModel()
        { }

        protected abstract byte SizeOfPointers { get; }
   }

    public class ProfilerDataModel32 : ProfilerDataModel<uint>
    {
        public override void Clear()
        {
        }

        protected override byte SizeOfPointers
        {
            get { return sizeof(uint); }
        }

    }

    public class ProfilerDataModel64 : ProfilerDataModel<ulong>
    {
        public override void Clear()
        {
        }

        protected override byte SizeOfPointers
        {
            get { return sizeof(ulong); }
        }
    }
}
