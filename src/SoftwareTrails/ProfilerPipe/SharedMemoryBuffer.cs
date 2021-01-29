using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.MemoryMappedFiles;
using System.Threading;

namespace SoftwareTrails
{
    class SharedMemoryBuffer : IDisposable
    {
        internal const string SharedMemoryName = "ProfilerData";
        internal const int SharedMemorySize = 400000000; // 400 megabytes
        private MemoryMappedFile sharedMemory;
        private MemoryMappedViewAccessor sharedMemoryAccessor;
        private long sharedMemoryPosition;
        private long sharedMemoryMaximum;
        private bool is64Bit; // target process is 64bit.
        private int sharedMemFieldSize;

        public SharedMemoryBuffer(bool is64bit)
        {   
            // create large shared buffer for efficient transfer of method call data.
            sharedMemory = MemoryMappedFile.CreateNew(SharedMemoryName, SharedMemorySize);
            sharedMemoryAccessor = sharedMemory.CreateViewAccessor();
            this.is64Bit = is64bit;
            if (is64Bit)
            {
                sharedMemFieldSize = sizeof(Int64);
            }
            else
            {
                sharedMemFieldSize = sizeof(Int32);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~SharedMemoryBuffer()
        {
            Dispose(false);
        }

        public virtual void Dispose(bool disposing)
        {
            using (sharedMemoryAccessor)
            {
                sharedMemoryAccessor = null;
            }
            using (sharedMemory)
            {
                sharedMemory = null;
            }
            sharedMemoryPosition = 0;
        }

        internal void MoveTo(int pos)
        {
            sharedMemoryPosition = pos;
        }

        public void SetSize(long calls) 
        {
            sharedMemoryMaximum = calls * sharedMemFieldSize * 2;
            if (sharedMemoryPosition > sharedMemoryMaximum)
            {
                // must have wrapped around.
                sharedMemoryPosition = 0;
            }
        }

        public long ReadRecord(out long timestamp)
        {
            long id = 0;
            timestamp = 0;
            if (sharedMemoryAccessor != null)
            {
                long pos = sharedMemoryPosition;
                
                if (pos >= sharedMemoryMaximum)
                {
                    // we are at the end, nothing to return yet.
                    return 0;
                }

                if (is64Bit)
                {
                    id = sharedMemoryAccessor.ReadInt64(pos);
                }
                else
                {
                    id = sharedMemoryAccessor.ReadInt32(pos);
                }

                if (id == 0)
                {
                    // do not advance pointer in this case.
                    return 0;
                }

                long timestampPosition = pos + sharedMemFieldSize;

                if (is64Bit)
                {
                    timestamp = sharedMemoryAccessor.ReadInt32(timestampPosition);
                }
                else
                {
                    timestamp = sharedMemoryAccessor.ReadInt64(timestampPosition);
                }
                if (timestamp == 0)
                {
                    return 0;
                }

                sharedMemoryPosition += sharedMemFieldSize; // methodid
                sharedMemoryPosition += sharedMemFieldSize; // timestamp

            }
            return id;
        }

    }
}
