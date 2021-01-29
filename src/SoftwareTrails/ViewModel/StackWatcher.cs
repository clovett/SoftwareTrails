using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace SoftwareTrails
{
    class StackWatcher : IDisposable
    {
        private ProfilerControlModel controller;
        CancellationTokenSource cancelTokenSource;
        long callsRead;
        ConcurrentStack<CallHistory> stack = new ConcurrentStack<CallHistory>();
        int? location;
        SpinLock pendingSpinLock = new SpinLock(false);
        CallHistory pending;
        CallHistory pendingTail;
        int batch;
        CallStackView view;
        SpinLock freeSpinLock = new SpinLock(false);
        CallHistory free; // for recyling
        CallHistory freeTail;

        public StackWatcher(ProfilerControlModel controller, CallStackView view)
        {
            this.view = view;
            this.controller = controller;
            cancelTokenSource = new CancellationTokenSource();
            Task.Factory.StartNew(new Action(() =>
            {
                ReadCalls(cancelTokenSource.Token);
            }), cancelTokenSource.Token);
        }

        public long CallsRead { get { return this.callsRead; } }

        public event EventHandler Rewound;

        public void Rewind()
        {
            if (controller != null)
            {
                // replay the current history with the new filter in effect.
                controller.Reset();
            }
            Clear();

            if (Rewound != null)
            {
                Rewound(this, EventArgs.Empty);
            }
        }

        internal void Clear()
        {
            pending = null;
            stack.Clear();
            callsRead = 0;
        }

        public void BatchUpdate(CodeBlockView view)
        {
            batch++;

            CallHistory history = GetPending();

            if (history != null)
            {
                view.AddBlocks(history, batch);

                bool hasFreeLock = false;
                AddFree(ref hasFreeLock, history);

            }
        }

        public ConcurrentStack<CallHistory> Stack
        {
            get { return stack; }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            using (cancelTokenSource)
            {
                if (cancelTokenSource != null)
                {
                    cancelTokenSource.Cancel(false);
                    cancelTokenSource = null;
                }
            }
        }

        void ReadCalls(CancellationToken token)
        {
            // this is it's own thread, so it has it's own version of haslock.
            callsRead = 0;
            bool hasLock = false;
            bool hasFreeLock = false;

            // just let it rip as fast as possible.
            while (!token.IsCancellationRequested && controller != null)
            {
                long timestamp;
                long methodId = controller.ReadMethod(out timestamp);
                if (methodId == 0)
                {
                    // reached end of buffer
                    Thread.Sleep(1000);
                }                    
                else if (methodId == ProfilerControlModel.LeaveMethod || methodId == ProfilerControlModel.TailCall)
                {
                    CallHistory call = null;
                        
                    if (stack.TryPop(out call))
                    {
                        // add elapsed time of this call.
                        call.Elapsed = (int)(timestamp - call.Timestamp);

                        AddPending(ref hasLock, call);
                    }

                    if (location.HasValue)
                    {
                        if (stack.Count < location.Value)
                        {
                            // done with this stack.
                            location = null;
                        }
                        else
                        {
                            // tell it about the elapsed time.
                            CallHistory previous = null;
                            stack.TryPeek(out previous);
                            view.ExitMethod(call, previous);
                        }
                    }
                    callsRead++;
                }
                else // this is a call then
                {
                    callsRead++;

                    //get cached method info
                    MethodCall method = controller.GetMethodName(methodId);
                    if (method != null)
                    {
                        CallHistory call = GetFree(ref hasFreeLock, method);
                        
                        call.Timestamp = timestamp;
                        call.Elapsed = 0; // don't know yet.

                        CallHistory previous = null;
                        stack.TryPeek(out previous);

                        // create timestamped copy
                        stack.Push(call);

                        if (view.Watching != null && method.Matches(view.Watching))
                        {
                            location = stack.Count;

                            // tell the view about the entire stack that we have so far
                            view.ShowStack(stack);
                        }
                        else if (location.HasValue)
                        {
                            // we are going deeper
                            view.EnterMethod(previous, call);
                        }
                    }
                }
            }
            cancelTokenSource = null;
        }

        #region Lite Concurrent Free List

        // get one item of the front of the free list, or allocate new CallHistory if there are no free ones.
        private CallHistory GetFree(ref bool hasFreeLock, MethodCall method)
        {
            CallHistory result = null;
            freeSpinLock.Enter(ref hasFreeLock);
            Debug.Assert(hasFreeLock);

            if (freeTail != null)
            {
                result = free;                
                free = free.Next;
                result.Next = null;

                if (free == null)
                {
                    freeTail = null;
                }
                result.Method = method;
            }
            else
            {
                result = new CallHistory() { Method = method };
            }

            freeSpinLock.Exit();
            hasFreeLock = false;
            return result;
        }

        // adds the whole free chain to the end of free list.
        private void AddFree(ref bool hasFreeLock, CallHistory history)
        {
            freeSpinLock.Enter(ref hasFreeLock);

            if (freeTail != null)
            {
                freeTail.Next = history;
                freeTail = history;
            }
            else
            {
                free = freeTail = history;
            }

            freeSpinLock.Exit();
            hasFreeLock = false;
        }
        #endregion 

        #region Lite Concurrent Pending List

        // add one new item to end of pending list.
        private void AddPending(ref bool hasLock, CallHistory call)
        {
            pendingSpinLock.Enter(ref hasLock);
            Debug.Assert(hasLock);

            if (pendingTail == null)
            {
                pending = pendingTail = call;
            }
            else
            {
                pendingTail.Next = call;
                pendingTail = call;
            }

            pendingSpinLock.Exit();
            hasLock = false;
        }

        // grab the whole pending list.
        private CallHistory GetPending()
        {
            // Note: IntelockedExchange is not good enough here, we need to be able to
            // stop the ReadCalls thread form adding to the pendingTail during this time.
            bool hasLock = false;
            pendingSpinLock.Enter(ref hasLock);
            Debug.Assert(hasLock);
            CallHistory history = pending;
            pending = null;
            pendingTail = null;
            pendingSpinLock.Exit();
            return history;
        }

        #endregion

    }
}
