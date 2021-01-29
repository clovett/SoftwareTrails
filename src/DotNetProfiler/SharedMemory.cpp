#include "StdAfx.h"
#include "SharedMemory.h"

CSharedMemory::CSharedMemory(TCHAR* name, long size)
{
    SetupSharedMemory(name, size);
	lpCriticalSection = new CRITICAL_SECTION();
	InitializeCriticalSection(lpCriticalSection);
}


CSharedMemory::~CSharedMemory(void)
{
    CloseSharedMemory();
	DeleteCriticalSection(lpCriticalSection);
	delete lpCriticalSection;
	lpCriticalSection = NULL;
}

HRESULT CSharedMemory::WriteRecord(UINT_PTR data, UINT_PTR timestamp)
{
	UINT_PTR buffer[2] = { data, timestamp };
	Write(&buffer, RecordSize);
	return S_OK;
}


HRESULT CSharedMemory::Write(void* data, int len)
{	
    if (_bufferPos != NULL) 
    {		
		EnterCriticalSection(lpCriticalSection);

        if ((UINT_PTR)_bufferPos + (UINT_PTR)len > (UINT_PTR)_bufferEnd) 
        {
            // start over.
            Reset();
        }
        
		// bugbug: Shock horror, memcpy corrupts memory on 64bit windows 8, but a manual for-loop doesn't.
		// (I'm wondering if the inline memcpy instruction on 64bit machine is using
		// a register that my assembler code isn't saving...)
		// memcpy(_bufferPos, data, len);		

		int num = (len / sizeof(UINT_PTR));
#ifdef DEBUG
		if (num * sizeof(UINT_PTR) != len) 
		{
			MessageBox(NULL, L"The Write function doesn't support this record length", L"Profiler Debug Prompt", MB_ICONINFORMATION);
		}
#endif
		UINT_PTR* source = (UINT_PTR*)data;
		UINT_PTR* target = (UINT_PTR*)_bufferPos;
		for (int i = 0; i < len; i++)
		{
			target[i] = source[i];
		}

        _bufferPos = (void*)((UINT_PTR)_bufferPos + (UINT_PTR)len);

		LeaveCriticalSection(lpCriticalSection);
    }
    return S_OK;
}

HRESULT CSharedMemory::SetupSharedMemory(TCHAR* name, long size)
{
    _bufferSize = size;
    _bufferPos = NULL;
    _bufferEnd = NULL;
	_sharedBuffer = NULL;
    _version = 0;
 
    hMapFile = OpenFileMapping(FILE_MAP_WRITE, TRUE, name);

    if (hMapFile == NULL)
    {
        HRESULT hr = GetLastError();
		
		MessageBox(NULL, L"Could not create file mapping object", L"Shared Memory Not Found", MB_ICONINFORMATION);

        //LogString("Could not create file mapping object (%d).\n", hr);
        return hr;
    }

    _sharedBuffer = (void*) MapViewOfFile(hMapFile,   // handle to map object
        FILE_MAP_ALL_ACCESS, // read/write permission
        0,
        0,
        size);

    if (_sharedBuffer == NULL)
    {
        HRESULT hr = GetLastError();
        //LogString("Could not map view of file (%d).\n", hr);
        CloseHandle(hMapFile);
        return hr;
    }

    ZeroMemory(_sharedBuffer, _bufferSize);
    _bufferPos = _sharedBuffer;
    _bufferEnd = (void*)((UINT_PTR)_sharedBuffer + (UINT_PTR)_bufferSize);
	
    return 0;

}

_int64 CSharedMemory::GetPosition()
{
	EnterCriticalSection(lpCriticalSection);
    _int64 result = (_int64)_bufferPos - (_int64)_sharedBuffer;
	LeaveCriticalSection(lpCriticalSection);
	return result;
}

long CSharedMemory::GetVersion()
{
    return _version;
}

void CSharedMemory::CloseSharedMemory()
{
	EnterCriticalSection(lpCriticalSection);
    _bufferPos = NULL;
    _bufferEnd = NULL;
    void* buffer = _sharedBuffer;    
    _sharedBuffer = NULL;    
    UnmapViewOfFile(buffer);
    CloseHandle(hMapFile);
	LeaveCriticalSection(lpCriticalSection);
}

void CSharedMemory::Reset()
{
	EnterCriticalSection(lpCriticalSection);
    ZeroMemory(_sharedBuffer, _bufferSize);
    _bufferPos = _sharedBuffer;
    _version++;
	LeaveCriticalSection(lpCriticalSection);
}