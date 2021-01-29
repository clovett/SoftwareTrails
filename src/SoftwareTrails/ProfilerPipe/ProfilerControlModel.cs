using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO.Compression;
using System.Collections.Concurrent;
using UIController;

namespace SoftwareTrails
{
    /// <summary>
    /// Controller handles communication of control messages to/from the profiler DLL.
    /// </summary>
    public class ProfilerControlModel : INotifyPropertyChanged, IDisposable
    {
        SharedMemoryBuffer buffer;

        /// <summary>
        /// The threading model describes how messages are sent. For example, with Multiple, requests
        /// like 'BaseSnap' will be sent on a separate thread.
        /// Single should be used for non-UI applications.
        /// </summary>
        public enum ThreadingModel
        {
            Single,
            Multiple
        }

        /// <summary>
        /// The data model must be non-null.
        /// Single-threaded controller
        /// </summary>
        public ProfilerControlModel()
            : this(ThreadingModel.Single)
        { }

        /// <summary>
        /// The data model must be non-null.
        /// </summary>
        public ProfilerControlModel(ThreadingModel threadingModel)
        {
            if (threadingModel == ThreadingModel.Multiple)
            {
                var scheduler = TaskScheduler.FromCurrentSynchronizationContext();
                _uiThreadTaskFactory = new TaskFactory(scheduler);
            }
  
            UseMultipleThreads = (threadingModel == ThreadingModel.Multiple);

            // Set property to initialize UI state, even though the backing field is false by default.
            IsAttached = false;
        }

        /// <summary>
        /// Reset the data model, which must be non-null.
        /// </summary>
        public void SetDataModel(ProfilerDataModel model)
        {
            Debug.Assert(model != null);
            _model = model;
        }

        /// <summary>
        /// Attach to the specified process. Returns the HRESULT from the attach call. 0 == SUCCESS.
        /// </summary>
        public uint Attach(Process process)
        {
            if (process == null)
            {
                Status = "Select a process to attach to.";
                return ProfilerErrorCodes.InvalidProcess;
            }

            if (process.HasExited)
            {
                Status = "Process has exited.";
                return ProfilerErrorCodes.ProcessHasExited;
            }

            _attachedProcess = process;

            bool is64Bit = Environment.Is64BitOperatingSystem && (NativeMethods.IsWow64Process(_attachedProcess) != NativeMethods.IsWow64.Yes);

            if (is64Bit)
            {
                _model = new ProfilerDataModel64();
            }
            else
            {
                _model = new ProfilerDataModel32();
            }

            using (this.buffer)
            {
            }
            this.buffer = new SharedMemoryBuffer(is64Bit);

            if (SendMessage("M:" + SharedMemoryBuffer.SharedMemoryName + "," + SharedMemoryBuffer.SharedMemorySize) == null)
            {
                Status = "Failed to send shared memory name to profiler";
                return ProfilerErrorCodes.ErrorFileNotFound;
            }

            // Wait for the target process to exit before enabling the attach button again. For the detach case we remove the event
            // handler on detach.
            _attachedProcess.EnableRaisingEvents = true;
            _attachedProcess.Exited += new EventHandler(process_Exited);
            IsAttached = true;
            ShowingFile = false;

            return ProfilerErrorCodes.ErrorSuccess;
        }

        public Process AttachedProcess { get { return _attachedProcess; } }

        /// <summary>
        /// Detach the profiler. Does nothing if not attached.
        /// </summary>
        public bool Detach()
        {
            bool result = true;
            if (_attachedProcess != null && _controlPipe != null && _controlPipe.IsConnected)
            {
                
                CallThreaded(() =>
                {
                    if (SendTwoPhaseMessage("Detach"))
                    {
                        IsAttached = false;
                    }
                    else
                    {
                        result = false;
                    }
                    _attachedProcess.Exited -= new EventHandler(process_Exited);
                    _attachedProcess = null;
                });
            }
            using (buffer)
            {
                buffer = null;
            }
            return result;
        }

        /// <summary>
        /// Set the process that we are attached to.
        /// </summary>
        public bool SetAttachedProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                _attachedProcess = process;
            }
            catch (ArgumentException)
            {
                return false;
            }
            
            return true;
        }

        void process_Exited(object sender, EventArgs e)
        {
            IsAttached = false;            
        }


        public Tuple<long, long, long> LookupStats()
        {
            string message = "C:GetCounts";

            long functions = 0;
            long calls = 0;
            long version = 0;

            lock (pipeSync)
            {
                string result = SendMessage(message);
                
                if (string.IsNullOrEmpty(result))
                {
                    Status = String.Format("Failed to get result.");
                    return null;
                }

                string[] parts = result.Split(',');
                if (parts.Length == 3)
                {
                    if (!long.TryParse(parts[0], out functions))
                    {
                        functions = 0;
                    }
                    if (!long.TryParse(parts[1], out calls))
                    {
                        calls = 0;
                    }
                    if (!long.TryParse(parts[2], out version))
                    {
                        version = 0;
                    }
                    buffer.SetSize(calls);
                }
            }

            return new Tuple<long, long, long>(functions, calls, version);
        }

        public const long LeaveMethod = 1;
        public const long TailCall = 2;

        public long ReadMethod(out long timestamp)
        {
            long id = 0;
            timestamp = 0;
            if (buffer == null)
            {
                return id;
            }

            id = buffer.ReadRecord(out timestamp);

            if (id == LeaveMethod)
            {
                // do nothing
                return id;
            }
            else if (id == TailCall)
            {
                // do nothing
                return id;
            }
            else if (id != 0)
            {
                // make sure we have the method name.
                FetchMethodName(id);
            }                
            return id;
        }

        public void WrapAround()
        {
            buffer.SetSize(0);
        }

        private ConcurrentDictionary<long, MethodCall> functionMap = new ConcurrentDictionary<long, MethodCall>();

        public MethodCall GetMethodName(long methodId)
        {
            MethodCall result = null;
            functionMap.TryGetValue(methodId, out result);
            return result;
        }

        private void FetchMethodName(long methodId)
        {
            MethodCall method = GetMethodName(methodId);
            if (method == null)
            {
                lock (pipeSync)
                {
                    string message = "F:" + methodId;

                    string name = SendMessage(message);
                    if (!string.IsNullOrEmpty(name))
                    {
                        functionMap[methodId] = new MethodCall(methodId, name, true);
                    }
                }
            }
        }

        /// <summary>
        /// Clear the shared memory buffer of all call history.
        /// </summary>
        public void Clear()
        {
            lock (pipeSync)
            {
                string message = "X:";

                string result = SendMessage(message);
                if (string.IsNullOrEmpty(result))
                {
                    Status = String.Format("Failed to get result.");
                }

                buffer.MoveTo(0);
            }
        }

        /// <summary>
        /// replay the current history with the new filter in effect
        /// </summary>
        internal void Reset()
        {
            if (buffer != null)
            {
                buffer.MoveTo(0);
            }
        }

        /// <summary>
        /// Send a message that requires only a single acknowledgement. Used for quick operations.
        /// </summary>
        string SendMessage(string message)
        {
            string ret1;
            lock (pipeSync)
            {
                if (!ControlPipe.WriteMessage(message))
                {
                    return null;
                }

                ret1 = ControlPipe.ReadMessage();
                if (string.IsNullOrEmpty(ret1))
                {
                    return null;
                }
            }
            return ret1;
        }

        /// <summary>
        /// Send a message that requires two acks. This is used
        /// for operations that can take some time, i.e. Force GC.
        /// </summary>
        bool SendTwoPhaseMessage(string message)
        {
            lock (ControlPipe)
            {
                WaitingForProfiler = true;
                DateTime begin = DateTime.Now;
                if (SendMessage(message) == null)
                {
                    Status = "Message send failed.";
                    WaitingForProfiler = false;
                    return false;
                }

                Status = "Profiler received message. Waiting for data.";

                var ret2 = ControlPipe.ReadMessage();
                if (string.IsNullOrEmpty(ret2))
                {
                    Status = String.Format("Failed to read second ack.");
                    WaitingForProfiler = false;
                    return false;
                }
                Debug.WriteLine(ret2);
                

                DateTime end = DateTime.Now;
                Status = String.Format("Finished {0}. Took {1:N2} seconds", message, (end - begin).TotalSeconds);
                WaitingForProfiler = false;
            }

            return true;
        }

        NamedPipeReaderWriter ControlPipe
        {
            get
            {
                if (_attachedProcess == null)
                    throw new ApplicationException("Not attached to process");

                lock (pipeSync)
                {
                    //TODO - should dispose this allocated reader/writer
                    if (_controlPipe == null)
                    {
                        _controlPipe = new NamedPipeReaderWriter("D795A307-4F19-4E49-B714-8641DF72F493-Control-" + _attachedProcess.Id.ToString(), System.IO.Pipes.PipeDirection.InOut);
                    }
                    EnsureDataPipeInitialized();
                }

                return _controlPipe;
            }
        }

        void EnsureDataPipeInitialized()
        {
            if (_dataPipe == null)
            {
                if (_attachedProcess == null)
                    throw new ApplicationException("Not attached to process");
                _dataPipe = new NamedPipeReaderWriter("D795A307-4F19-4E49-B714-8641DF72F493-Data-" + _attachedProcess.Id.ToString(), System.IO.Pipes.PipeDirection.In);
            }
            if (_dataPipeReadTask == null)
            {
                _dataPipeReadTask = new Task(() =>
                    {
                        // todo: not currently using this datapipe...

                        _dataPipeReadTask = null;
                    });
                _dataPipeReadTask.Start();
            }
        }

        /// <summary>
        /// Determine if the profiler is attached to a process.
        /// </summary>
        public bool IsAttached
        {
            get
            {
                return _isAttached;
            }
            private set
            {
                _isAttached = value;
                OnPropertyChanged("IsAttached");
                if (_isAttached)
                {
                    if (_attachedProcess != null)
                    {
                        AttachedInfo = String.Format("Attached to: {0} ({1})", _attachedProcess.ProcessName, _attachedProcess.Id);
                    }
                }
                else
                {
                    AttachedInfo = "Not attached.";
                    HasBaselineSnapshot = false;
                    HasSnapshot = false;
                }
            }
        }

        /// <summary>
        /// True if a baseline snapshot has been taken.
        /// </summary>
        public bool HasBaselineSnapshot
        {
            get
            {
                return _hasBaselineSnapshot;
            }
            private set
            {
                _hasBaselineSnapshot = value;
                if (!value && _model != null)
                {
                    // Changing the baseline should clear the existing profiling data.
                    _model.Clear();
                }
                OnPropertyChanged("HasBaselineSnapshot");
            }
        }

        /// <summary>
        /// True if a second snapshot has been taken.
        /// </summary>
        public bool HasSnapshot
        {
            get
            {
                return _hasSnapshot;
            }
            private set
            {
                _hasSnapshot = value;
                OnPropertyChanged("HasSnapshot");
            }
        }

        /// <summary>
        /// True if a long-running command has been issued and we're waiting for a 2nd acknowledgement.
        /// </summary>
        public bool WaitingForProfiler
        {
            get
            {
                return _waitingForProfiler;
            }
            set
            {
                _waitingForProfiler = value;
                OnPropertyChanged("WaitingForProfiler");
            }
        }

        /// <summary>
        /// Status string which may be displayed to the user.
        /// </summary>
        public string Status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;

                if (string.IsNullOrEmpty(value))
                    _status = "";
                else
                    _status = DateTime.Now.ToLongTimeString() + ": " + value;
                OnPropertyChanged("Status");
            }
        }

        /// <summary>
        /// Information about which process we are currently attached to.
        /// </summary>
        public string AttachedInfo
        {
            get
            {
                return _attachedInfo;
            }
            set
            {
                _attachedInfo = value;
                OnPropertyChanged("AttachedInfo");
                Status = "";
            }
        }


        public bool CanAttach
        {
            get
            {
                return !IsAttached && !WaitingForProfiler;
            }
        }

        public bool CanDetach
        {
            get
            {
                return IsAttached && !WaitingForProfiler;
            }
        }

        public bool CanExecuteAction
        {
            get
            {
                return IsAttached && !WaitingForProfiler && !ShowingFile;
            }
        }

        public bool CanTakeSnapshot
        {
            get
            {
                return IsAttached && !WaitingForProfiler && !ShowingFile && HasBaselineSnapshot;
            }
        }


        /// <summary>
        /// True if a file is being shown instead of viewing live data.
        /// </summary>
        public bool ShowingFile
        {
            get
            {
                return _showingFile;
            }
            set
            {
                if (_showingFile != value)
                {
                    _showingFile = value;
                    OnPropertyChanged("ShowingFile");
                }
            }
        }


        /// <summary>
        /// Execute a given action on the main thread.
        /// </summary>
        void CallOnMainThread(Action action)
        {
            Debug.Assert(action != null);
            if (_uiThreadTaskFactory != null)
            {
                _uiThreadTaskFactory.StartNew(action);
            }
            else
            {
                action();
            }
        }

        /// <summary>
        /// If the controller is multi-threaded, run the action on a separate thread, otherwise just execute it.
        /// </summary>
        void CallThreaded(Action action)
        {
            Debug.Assert(action != null);
            if (UseMultipleThreads)
            {
                Task.Factory.StartNew(action);
            }
            else
            {
                action();
            }
        }

        void OnPropertyChanged(string name)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, new PropertyChangedEventArgs(name));
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (pipeSync)
                {
                    if (_controlPipe != null)
                    {
                        // Use a new pipe for new connections.
                        using (_controlPipe)
                        {
                            _controlPipe = null;
                        }
                    }

                    if (_dataPipeReadTask != null)
                    {
                        using (_dataPipeReadTask)
                        {
                            _dataPipeReadTask.Wait();
                            _dataPipeReadTask = null;
                        }
                    }
                    if (_dataPipe != null)
                    {
                        using (_dataPipe)
                        {
                            _dataPipe = null;
                        }
                    }
                    using (buffer)
                    {
                        buffer = null;
                    }
                }
            }
        }

        bool UseMultipleThreads
        {
            get;
            set;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        string _attachedInfo;
        string _status;
        bool _showingFile;
        bool _isAttached;
        bool _hasBaselineSnapshot;
        bool _hasSnapshot;
        bool _waitingForProfiler;
        object pipeSync = new object();
        NamedPipeReaderWriter _controlPipe;
        NamedPipeReaderWriter _dataPipe;
        Task _dataPipeReadTask;
        Process _attachedProcess;
        ProfilerDataModel _model;
        TaskFactory _uiThreadTaskFactory;

    }
}
