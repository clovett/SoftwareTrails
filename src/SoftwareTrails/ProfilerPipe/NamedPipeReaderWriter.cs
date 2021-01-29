using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO.Pipes;
using System.IO;
using System.Diagnostics;

namespace SoftwareTrails
{
    /// <summary>
    /// Provides read/write functionality from/to named pipes.
    /// Typical usage:
    /// - For short string communications (less than MaxBytes), use:
    ///   ReadMessage
    ///   WriteMessage
    /// 
    ///   These methods always work with Unicode characters.
    /// 
    /// - To read in a sequence of bytes, use:
    ///   ReadBytes
    ///   
    /// - To write out a sequence of bytes, use:
    ///   WriteBytes
    /// </summary>
    public sealed class NamedPipeReaderWriter : IDisposable
    {

        /// <summary>
        /// A named pipe with a default connection timeout.
        /// @see NamedPipeReaderWriter
        /// </summary>
        public NamedPipeReaderWriter(string pipeName, PipeDirection pipeDirection)
            : this(pipeName, pipeDirection, 5000)
        { 
        }

        /// <summary>
        /// Connect a named pipe. Check that the connection succeeded using the IsConnected method.
        /// </summary>
        /// <param name="pipeName">An identifier for the pipe, for example "9993AA01-80A4-4310-A761-738924B29808"</param>
        public NamedPipeReaderWriter(string pipeName, PipeDirection pipeDirection, int timeoutMS)
        {
            Debug.Assert(pipeName != null);

            _pipeDirection = pipeDirection;
            _pipeClient = new NamedPipeClientStream(".", pipeName, pipeDirection);

            // Connect to the pipe or wait until the pipe is available.
            _pipeClient.Connect(timeoutMS);
            
            if (!_pipeClient.IsConnected)
            {
                throw new Exception("Error connecting to pipe");
            }
        }

        /// <summary>
        /// Implement destructor to call dispose
        /// </summary>
        ~NamedPipeReaderWriter()
        {
            Dispose(false);
        }

        /// <summary>
        /// Send a string down the pipe using Unicode encoding.
        /// Only the first MaxBytes will be sent, meaning the max
        /// string length is MaxBytes / BytesPerChar.
        /// </summary>
        public bool WriteMessage(string message)
        {
            if (!IsConnected || _pipeDirection.Equals(PipeDirection.In))
                return false;

            try
            {
                // Don't use a StreamWriter here because the encoding doesn't seem to work well with the pipe.
                byte[] bytes = Encoding.Unicode.GetBytes(message);
                _pipeClient.Write(bytes, 0, Math.Min(bytes.Length, MaxMessageBytes));
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }        

        /// <summary>
        /// Read a string from the pipe. We assume all strings are unicode.
        /// Only MaxBytes characters will be read, meaning the max string length
        /// is MaxBytes / BytesPerChar.
        /// </summary>
        public string ReadMessage()
        {
            if (!IsConnected || _pipeDirection.Equals(PipeDirection.Out))
                return null;

            byte[] buffer = new byte[MaxMessageBytes];
            int numBytesRead = 0;
            try
            {
                numBytesRead = _pipeClient.Read(buffer, 0, MaxMessageBytes);
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

            // Each unicode character takes BytesPerChar. We require at least one character or we return null.
            const int BytesPerChar = 2;
            if (numBytesRead <= BytesPerChar)
                return null;

            Debug.Assert(numBytesRead % BytesPerChar == 0);
            // The subtraction at the end of the Substring removes the '\0' character from the string.
            return Encoding.Unicode.GetString(buffer).Substring(0, numBytesRead / BytesPerChar - 1);
        }
        
        /// <summary>
        /// Send a byte[] down the pipe. The format is specified by the caller.
        /// </summary>
        public bool WriteBytes(byte[] bytes)
        {
            if (!IsConnected || _pipeDirection.Equals(PipeDirection.In))
                return false;

            try
            {
                _pipeClient.Write(bytes, 0, bytes.Length);
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Read in a series of bytes from a packet.
        /// The format is:
        /// [data size (4 bytes)] [data]
        /// The total packet size = 4 + data size.
        /// </summary>
        public byte[] ReadBytes()
        {
            if (!IsConnected || _pipeDirection.Equals(PipeDirection.Out))
                return null;

            const int SizeOfSize = sizeof(UInt32);
            byte[] sizeBuffer = new byte[SizeOfSize];
            int numBytesRead = 0;
            try
            {
                numBytesRead = _pipeClient.Read(sizeBuffer, 0, SizeOfSize);
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

            if (numBytesRead != SizeOfSize)
                return null;

            uint bufferSize;
            try
            {
                bufferSize = BitConverter.ToUInt32(sizeBuffer, 0);
            } catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

            byte[] packetBuffer = new byte[bufferSize];
            try
            {
                numBytesRead = _pipeClient.Read(packetBuffer, 0, packetBuffer.Length);
            }
            catch (System.IO.IOException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

            if (numBytesRead != bufferSize)
                return null;

            return packetBuffer;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing && !IsDisposed)
            {
                _pipeClient.Dispose();
                IsDisposed = true;
            }
        }

        public bool IsDisposed
        {
            get;
            private set;
        }

        public bool IsConnected
        {
            get
            {
                return (_pipeClient != null && _pipeClient.IsConnected);
            }
        }

        // The maximum bytes sent or read in the ReadMessage & WriteMessage functions. The ReadBytes & WriteBytes functions ignore this.
        const int MaxMessageBytes = 512;
        NamedPipeClientStream _pipeClient;
        PipeDirection _pipeDirection;
    }
}
