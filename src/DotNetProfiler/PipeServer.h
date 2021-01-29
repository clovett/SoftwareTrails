#pragma once

class CProfiler;
class CSharedMemory;

class PipeServer
{
public:
    PipeServer(CProfiler &profiler);
    ~PipeServer(void);

	// call this method on a background thread to process pipe control requests.
    int Run();

    // Maximum packet size to send = MaxDataBytes
    static const int MaxDataBytes=1024*1024;

    // Send bytes to the data pipe
    // Returns true if the operation succeeded, false otherwise
    bool WriteToDataPipe(BYTE* bytes, DWORD numBytes);

    // Send the second acknowledgement for the detach down the control pipe when the detach
    // succeeds.
    void SignalDetach();

private:
	bool SetupNamedPipe(const wchar_t* pipeName, DWORD pipeMode, HANDLE &hPipe);

	DWORD ProcessControlRequests();

    HANDLE _hPipeData;
    HANDLE _hPipeControl;
    bool _fTerminated;
};
