#include "StdAfx.h"
#include "PipeServer.h"
#include "Profiler.h"
#include <strsafe.h>

#define VERBOSE_LOG
#ifdef VERBOSE_LOG
#define LOG _tprintf
#else
#define LOG (void)
#endif

// TODO - it is easy to keep this static for now to access from threads, but it isn't necessary.
static CProfiler* ProfilerInstance;

namespace
{
    const int BufferSizeChars = 10*512;
   

    const std::wstring AddPidToName(const wchar_t* name, DWORD pid)
    {
        std::wstringstream str;
        str << name << pid;
        return str.str();
    }
}

PipeServer::PipeServer(CProfiler &profiler) :
    _hPipeData(INVALID_HANDLE_VALUE),
    _hPipeControl(INVALID_HANDLE_VALUE),    
    _fTerminated(false)
{
    ProfilerInstance = &profiler;
}

PipeServer::~PipeServer()
{
    _fTerminated = true;

	ProfilerInstance = NULL;

    if (_hPipeData != INVALID_HANDLE_VALUE)
    {
        FlushFileBuffers(_hPipeData);
        DisconnectNamedPipe(_hPipeData); 
        CloseHandle(_hPipeData);
        _hPipeData = INVALID_HANDLE_VALUE;
    }

    if (_hPipeControl != INVALID_HANDLE_VALUE)
    {
        FlushFileBuffers(_hPipeControl);
        DisconnectNamedPipe(_hPipeControl); 
        CloseHandle(_hPipeControl);
        _hPipeControl = INVALID_HANDLE_VALUE;
    }

}


bool PipeServer::SetupNamedPipe(const wchar_t* pipeName, DWORD pipeMode, HANDLE &hPipe)
{
    bool fConnected = FALSE; 
    hPipe = INVALID_HANDLE_VALUE;

    // The main loop creates an instance of the named pipe and 
    // then waits for a client to connect to it. When the client 
    // connects, a thread is created to handle communications 
    // with that client, and this loop is free to wait for the
    // next client connect request. It is an infinite loop.
    LOG( TEXT("\nPipe Server: Main thread awaiting client connection on %s\n"), pipeName);
    hPipe = CreateNamedPipe( 
        pipeName,
        pipeMode,
        PIPE_TYPE_MESSAGE |       // message type pipe 
        PIPE_READMODE_MESSAGE |   // message-read mode 
        PIPE_NOWAIT,                // non-blocking mode so we can shut down cleanly
        1, // max. instances  
        BufferSizeChars,                  // output buffer size 
        BufferSizeChars,                  // input buffer size 
        0,                        // client time-out 
        NULL);                    // default security attribute 

    if (hPipe == INVALID_HANDLE_VALUE) 
    {
        LOG(TEXT("CreateNamedPipe failed, GLE=%d.\n"), GetLastError()); 
        return false;
    }

    // Wait for the client to connect; if it succeeds, 
    // the function returns a nonzero value. We do this in a non-blocking
    // way so that we can shutdown cleanly.
    while (!ConnectNamedPipe(hPipe, NULL)) 
    {
        HRESULT hr = GetLastError();
        if (hr == ERROR_PIPE_CONNECTED)
        {
            break; // we're good!
        }
        else if (hr == ERROR_PIPE_LISTENING)
        {
           SleepEx(200, false);
        }
        else 
        {
            return false;
        }

        if (_fTerminated) {
            return false;
        }
    }

        
    DWORD mode = PIPE_READMODE_MESSAGE | PIPE_WAIT;
    SetNamedPipeHandleState(_hPipeControl, &mode, NULL, NULL);

    return true;
}

// This method is called on a background thread.
int PipeServer::Run() 
{
    BOOL   fConnected = FALSE; 
    DWORD  dwThreadId = 0; 
    HANDLE hThread = NULL; 
    DWORD currentPid = GetCurrentProcessId();
    const std::wstring pipeName = AddPidToName(L"\\\\.\\pipe\\D795A307-4F19-4E49-B714-8641DF72F493-Control-", currentPid);
    const std::wstring pipeDataName = AddPidToName(L"\\\\.\\pipe\\D795A307-4F19-4E49-B714-8641DF72F493-Data-", currentPid); 

    fConnected = SetupNamedPipe(pipeName.c_str(), PIPE_ACCESS_DUPLEX, _hPipeControl) && SetupNamedPipe(pipeDataName.c_str(), PIPE_ACCESS_OUTBOUND, _hPipeData);

    if (_hPipeControl == INVALID_HANDLE_VALUE || _hPipeData == INVALID_HANDLE_VALUE) 
    {
        // TODO - better error handling here.
        return -1;
    }

    if (fConnected) 
    { 
		ProcessControlRequests();
    }

	if (_hPipeData != INVALID_HANDLE_VALUE && !_fTerminated)
    {
        FlushFileBuffers(_hPipeData);
        DisconnectNamedPipe(_hPipeData); 
        CloseHandle(_hPipeData);
        _hPipeData = INVALID_HANDLE_VALUE;
    }

    return 0; 
} 

bool WriteSimpleReply(HANDLE hPipe, LPTSTR msg, TCHAR* pchReplyBuffer)
{
	DWORD cbReplyBytes;
	DWORD cbWritten;

    // Check the outgoing message to make sure it's not too long for the buffer.
    if (FAILED(StringCchCopy( pchReplyBuffer, BufferSizeChars, msg )))
    {
        cbReplyBytes = 0;
        pchReplyBuffer[0] = 0;
        printf("StringCchCopy failed, no outgoing message.\n");
        return false;
    }
    cbReplyBytes = (lstrlen(pchReplyBuffer)+1)*sizeof(TCHAR);

	// Write the reply data to the pipe. 
	LOG( TEXT("Server Reply String:\"%s\"\n"), pchReplyBuffer );
	BOOL fSuccess = WriteFile( 
		hPipe,        // handle to pipe 
		pchReplyBuffer,     // buffer to write from 
		cbReplyBytes, // number of bytes to write 
		&cbWritten,   // number of bytes written 
		NULL);        // not overlapped I/O 

	if (!fSuccess || cbReplyBytes != cbWritten)
	{   
		LOG(TEXT("InstanceThread WriteFile failed, GLE=%d.\n"), GetLastError()); 
		return false;
	}
	return true;
}

void PipeServer::SignalDetach()
{
    if (_hPipeControl == INVALID_HANDLE_VALUE)
        return;

    TCHAR buffer[10];
    WriteSimpleReply(_hPipeControl, L"Ack", buffer);  
}

DWORD PipeServer::ProcessControlRequests()
{ 
    HANDLE hHeap      = GetProcessHeap();
    DWORD BufferSize = BufferSizeChars * sizeof(TCHAR);
    TCHAR* pchRequest = (TCHAR*)HeapAlloc(hHeap, 0, BufferSize);
    TCHAR* pchReply   = (TCHAR*)HeapAlloc(hHeap, 0, BufferSize);
    
    DWORD cbBytesRead = 0, cbReplyBytes = 0, cbWritten = 0; 
    BOOL fSuccess = FALSE;
	HANDLE hPipe  = _hPipeControl;

    // Do some extra error checking since the app will keep running even if this
    // thread fails.

    if (hPipe == NULL)
    {
        printf( "\nERROR - Pipe Server Failure:\n");
        printf( "   ProcessControlRequests got an unexpected NULL value in _hPipeControl.\n");
        printf( "   ProcessControlRequests exitting.\n");
        return (DWORD)-1;
    }

    if (pchRequest == NULL)
    {
        printf( "\nERROR - Pipe Server Failure:\n");
        printf( "   ProcessControlRequests got an unexpected NULL heap allocation.\n");
        printf( "   ProcessControlRequests exitting.\n");
        if (pchReply != NULL) HeapFree(hHeap, 0, pchReply);
        return (DWORD)-1;
    }

    if (pchReply == NULL)
    {
        printf( "\nERROR - Pipe Server Failure:\n");
        printf( "   ProcessControlRequests got an unexpected NULL heap allocation.\n");
        printf( "   ProcessControlRequests exitting.\n");
        if (pchRequest != NULL) HeapFree(hHeap, 0, pchRequest);
        return (DWORD)-1;
    }

    // Print verbose messages. In production code, this should be for debugging only.
    printf("ProcessControlRequests is processing messages...\n");

    // Loop until done reading
    while (ProfilerInstance != NULL && !_fTerminated) 
    { 
        // Read client requests from the pipe. This simplistic code only allows messages
        // up to BufferSizeChars characters in length.
        fSuccess = ReadFile( 
            hPipe,        // handle to pipe 
            pchRequest,   // buffer to receive data 
            BufferSize - sizeof(TCHAR),   // size of buffer, leaving room for null terminator.
            &cbBytesRead, // number of bytes read 
            NULL);        // not overlapped I/O 

        if (!fSuccess || cbBytesRead == 0)
        {   
            if (GetLastError() == ERROR_BROKEN_PIPE)
            {
                LOG(TEXT("ProcessControlRequests: client disconnected, hr=%d.\n"), GetLastError()); 
            }
            else
            {
                LOG(TEXT("ProcessControlRequests ReadFile failed, GLE=%d.\n"), GetLastError()); 
            }
            break;
        }

		if (_fTerminated) {
			break;
		}

        // Add null terminator if there is space in the buffer.
		int charsRead = cbBytesRead / sizeof(TCHAR);
        pchRequest[charsRead] = '\0';

        // TODO actually read the message. Also send back a reply when the detach has completed.
		TCHAR firstChar = (charsRead > 0) ?  pchRequest[0] : '\0';
		TCHAR secondChar = (charsRead > 1) ? pchRequest[1] : '\0';

        bool isDetach = firstChar == L'D';
        bool isSharedMemoryName = firstChar== L'M' && secondChar == L':';
        bool isLookupName = firstChar == L'F' && secondChar == L':';
        bool isGetCount = firstChar == L'C' && secondChar == L':';
        bool isDeleteAll = firstChar == L'X' && secondChar == L':';

        if (isDetach)
        {
			// client expecting inital acknowledgement
			fSuccess = WriteSimpleReply(hPipe, TEXT("ok"), pchReply); 

            // This call will not return if the detach succeeds for a V4 app - FreeLibraryAndExitThread is called here.
            // For a CLR V2 app, detach is not available so this method will do nothing.
            ProfilerInstance->RequestDetach();
			
			// client expecting additional detach message as part of 2 phase commit.
			fSuccess = WriteSimpleReply(hPipe, TEXT("detached"), pchReply); 		
        }
        else if (isSharedMemoryName)
        {
            // separate
            TCHAR* comma = wcsrchr(pchRequest, L',');
            if (comma != NULL) {
                *comma = '\0';
                int size = _ttoi(comma+1);
                ProfilerInstance->InitSharedMemory(pchRequest+2, size);
            }
            
            // provide simple ack to the fact that we received a message.
            fSuccess = WriteSimpleReply(hPipe, TEXT("ok"), pchReply); 		
        }
        else if (isLookupName) 
        {
            __int64 functionId = _ttoi64(pchRequest+2);

            ProfilerInstance->GetFunctionName((FunctionID)functionId, pchRequest, BufferSize);
			
			// client expecting method name reply.
			fSuccess = WriteSimpleReply(hPipe, pchRequest, pchReply); 	
        }
        else if (isGetCount)
        {
            long functions = ProfilerInstance->GetFunctionCount();
            long calls = ProfilerInstance->GetCallCount();
            long version = ProfilerInstance->GetVersion();

            int MAXDIGITS = 50; // BufferSize is plenty big enough for MAXDIGITS.
            _ltow_s(functions, pchRequest, MAXDIGITS, 10);

            size_t len = wcslen(pchRequest);
            
            // add calls
            TCHAR* ptr = pchRequest + len;
            *ptr = L',';
            ptr++;
            _ltow_s(calls, ptr, MAXDIGITS, 10);
                        
            // add version
            len = wcslen(pchRequest);
            ptr = pchRequest + len;
            *ptr = L',';
            ptr++;
            _ltow_s(version, ptr, MAXDIGITS, 10);            

			// client expecting method name reply.
			fSuccess = WriteSimpleReply(hPipe, pchRequest, pchReply); 	
        }
        else if (isDeleteAll) 
        {
            ProfilerInstance->DeleteAll();
            
            // provide simple ack to the fact that we received the message.
            fSuccess = WriteSimpleReply(hPipe, TEXT("ok"), pchReply); 		
        }
        else 
        {
            printf("Unknown message request");
        }

        if (!fSuccess)
        {
            break;
        }
    }

    // Flush the pipe to allow the client to read the pipe's contents 
    // before disconnecting. Then disconnect the pipe, and close the 
    // handle to this pipe instance. 

	if (!_fTerminated) {
		FlushFileBuffers(hPipe); 
		DisconnectNamedPipe(hPipe); 
		CloseHandle(hPipe);
	}

    TCHAR* buffer = pchRequest;
    pchRequest = NULL;
    HeapFree(hHeap, 0, buffer);

    buffer = pchReply;
    pchReply = NULL;
    HeapFree(hHeap, 0, buffer);

	if (ProfilerInstance != NULL) {
		ProfilerInstance->ClientDetached();
	}

    printf("ProcessControlRequests is finished.\n");
    return 1;
}

bool PipeServer::WriteToDataPipe(BYTE* bytes, DWORD numBytes)
{
    if (numBytes > MaxDataBytes || bytes == nullptr)
    {
        // TODO - error!
        return false;
    }

    if (*(DWORD*)bytes != (numBytes - sizeof(DWORD)))
    {
        // TODO - error!
        return false;
    }

    DWORD numBytesWritten=0;
    BOOL fSuccess = WriteFile( 
        _hPipeData,        // handle to pipe 
        bytes,     // buffer to write from 
        numBytes, // number of bytes to write 
        &numBytesWritten,   // number of bytes written 
        NULL);        // not overlapped I/O 


    return fSuccess == TRUE;
}


