#pragma once

const int RecordSize = sizeof(UINT_PTR) * 2;

class CSharedMemory
{
public:
	CSharedMemory(TCHAR* name, long size);
	~CSharedMemory(void);
	
	HRESULT WriteRecord(UINT_PTR data, UINT_PTR timestamp);

    _int64 GetPosition();
    long GetVersion();
    void Reset();
private:
		
	HRESULT Write(void* data, int len);

	// for setting up shared memory buffer.
	HRESULT SetupSharedMemory(TCHAR* name, long size);
	void CloseSharedMemory();

	HANDLE hMapFile;
	void* _bufferPos;
	long _bufferSize;	
	void* _bufferEnd;
	void* _sharedBuffer;
    long _version;
	LPCRITICAL_SECTION lpCriticalSection;
};

