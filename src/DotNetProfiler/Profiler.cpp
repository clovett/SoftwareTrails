/*****************************************************************************
 * DotNetProfiler
 * 
 * Copyright (c) 2006 Scott Hackett
 * 
 * This software is provided 'as-is', without any express or implied warranty.
 * In no event will the author be held liable for any damages arising from the
 * use of this software. Permission to use, copy, modify, distribute and sell
 * this software for any purpose is hereby granted without fee, provided that
 * the above copyright notice appear in all copies and that both that
 * copyright notice and this permission notice appear in supporting
 * documentation.
 * 
 * Scott Hackett (code@scotthackett.com)
 *****************************************************************************/

#include <assert.h>
#include "winnt.h"
#include "stdafx.h"
#include "Profiler.h"
// CLR
#include <metahost.h>
#include <winuser.h>
#include <winbase.h>
#include <unordered_map>


#pragma warning (disable: 4996) 

#define ARRAY_SIZE(s) (sizeof(s) / sizeof(s[0]))
#define dimensionof(a) 		(sizeof(a)/sizeof(*(a)))

const UINT_PTR LeaveCallId = 1;
const UINT_PTR TailCallId = 2;

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

// global reference to the profiler object (this) used by the static functions
CProfiler* g_pICorProfilerCallback = NULL;


/***************************************************************************************
 ********************                                               ********************
 ********************   Global Functions Used by Function Hooks     ********************
 ********************                                               ********************
 ***************************************************************************************/

//
// The functions EnterStub, LeaveStub and TailcallStub are wrappers. The use of 
// of the extended attribute "__declspec( naked )" does not allow a direct call
// to a profiler callback (e.g., ProfilerCallback::Enter( functionID )).
//
// The enter/leave function hooks must necessarily use the extended attribute
// "__declspec( naked )". Please read the corprof.idl for more details. 
//

/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
EXTERN_C void __stdcall EnterStub( FunctionID functionID )
{
	if (g_pICorProfilerCallback != NULL) 
	{
		g_pICorProfilerCallback->Enter( functionID );
	}
    
} // EnterStub


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
EXTERN_C void __stdcall LeaveStub( FunctionID functionID )
{
	if (g_pICorProfilerCallback != NULL) 
	{
		g_pICorProfilerCallback->Leave( functionID );
	}
    
} // LeaveStub


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
EXTERN_C void __stdcall TailcallStub( FunctionID functionID )
{
	if (g_pICorProfilerCallback != NULL) 
	{
	    g_pICorProfilerCallback->Tailcall( functionID );
	}
    
} // TailcallStub

// ----  CALLBACK FUNCTIONS ------------------
/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
#ifdef _X86_
void __declspec( naked ) EnterNaked()
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call EnterStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // EnterNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) LeaveNaked()
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call LeaveStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // LeaveNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) TailcallNaked()
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call TailcallStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // TailcallNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) EnterNaked2(FunctionID funcId, 
                                     UINT_PTR clientData, 
                                     COR_PRF_FRAME_INFO func, 
                                     COR_PRF_FUNCTION_ARGUMENT_INFO *argumentInfo)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call EnterStub
        pop edx
        pop ecx
        pop eax
        ret 16
    }
} // EnterNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) LeaveNaked2(FunctionID funcId, 
                                     UINT_PTR clientData, 
                                     COR_PRF_FRAME_INFO func, 
                                     COR_PRF_FUNCTION_ARGUMENT_RANGE *retvalRange)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call LeaveStub
        pop edx
        pop ecx
        pop eax
        ret 16
    }
} // LeaveNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) TailcallNaked2(FunctionID funcId, 
                                        UINT_PTR clientData, 
                                        COR_PRF_FRAME_INFO func)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call TailcallStub
        pop edx
        pop ecx
        pop eax
        ret 12
    }
} // TailcallNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) EnterNaked3(FunctionIDOrClientID functionIDOrClientID)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call EnterStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // EnterNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) LeaveNaked3(FunctionIDOrClientID functionIDOrClientID)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call LeaveStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // LeaveNaked


/***************************************************************************************
 *  Method:
 *
 *
 *  Purpose:
 *
 *
 *  Parameters: 
 *
 *
 *  Return value:
 *
 *
 *  Notes:
 *
 ***************************************************************************************/
void __declspec( naked ) TailcallNaked3(FunctionIDOrClientID functionIDOrClientID)
{
    __asm
    {
        push eax
        push ecx
        push edx
        push [esp + 16]
        call TailcallStub
        pop edx
        pop ecx
        pop eax
        ret 4
    }
} // TailcallNaked

#elif defined(_AMD64_)
// these are linked in AMD64 assembly (amd64\asmhelpers.asm)
EXTERN_C void EnterNaked2(FunctionID funcId, 
                          UINT_PTR clientData, 
                          COR_PRF_FRAME_INFO func, 
                          COR_PRF_FUNCTION_ARGUMENT_INFO *argumentInfo);
EXTERN_C void LeaveNaked2(FunctionID funcId, 
                          UINT_PTR clientData, 
                          COR_PRF_FRAME_INFO func, 
                          COR_PRF_FUNCTION_ARGUMENT_RANGE *retvalRange);
EXTERN_C void TailcallNaked2(FunctionID funcId, 
                             UINT_PTR clientData, 
                             COR_PRF_FRAME_INFO func);

EXTERN_C void EnterNaked3(FunctionIDOrClientID functionIDOrClientID);
EXTERN_C void LeaveNaked3(FunctionIDOrClientID functionIDOrClientID);
EXTERN_C void TailcallNaked3(FunctionIDOrClientID functionIDOrClientID);
#endif // _X86_    



// CProfiler
CProfiler::CProfiler() : 
            _pipeServer(*this),
    _sharedMemory(NULL)
{
	m_hLogFile = INVALID_HANDLE_VALUE;
	m_callStackSize = 0;	
    m_terminated = FALSE;
}

HRESULT CProfiler::FinalConstruct()
{
	// create the log file
	CreateLogFile();

	// log that we have reached FinalConstruct
	LogString("Entering FinalConstruct\r\n\r\n");	

	return S_OK;
}

void CProfiler::FinalRelease()
{
	// log that we have reached FinalRelease
	LogString("\r\n\r\nEntering FinalRelease\r\n\r\n");

	// close the log file
	CloseLogFile();

    CloseSharedMemory();
	
	m_terminated = true;
}

//
//// this function simply forwards the FunctionEnter call the global profiler object
//void __stdcall FunctionEnterGlobal(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_INFO *argInfo)
//{
//	// make sure the global reference to our profiler is valid
//    if (g_pICorProfilerCallback != NULL)
//        g_pICorProfilerCallback->Enter(functionID, clientData, frameInfo, argInfo);
//}
//
//// this function is called by the CLR when a function has been entered
//void _declspec(naked) FunctionEnterNaked(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO func, COR_PRF_FUNCTION_ARGUMENT_INFO *argumentInfo)
//{
//    __asm
//    {
//        push    ebp                 // Create a standard frame
//        mov     ebp,esp
//        pushad                      // Preserve all registers
//
//        mov     eax,[ebp+0x14]      // argumentInfo
//        push    eax
//        mov     ecx,[ebp+0x10]      // func
//        push    ecx
//        mov     edx,[ebp+0x0C]      // clientData
//        push    edx
//        mov     eax,[ebp+0x08]      // functionID
//        push    eax
//        call    FunctionEnterGlobal
//
//        popad                       // Restore all registers
//        pop     ebp                 // Restore EBP
//        ret     16
//    }
//}
//
//// this function simply forwards the FunctionLeave call the global profiler object
//void __stdcall FunctionLeaveGlobal(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE *retvalRange)
//{
//	// make sure the global reference to our profiler is valid
//    if (g_pICorProfilerCallback != NULL)
//        g_pICorProfilerCallback->Leave(functionID,clientData,frameInfo,retvalRange);
//}
//
//// this function is called by the CLR when a function is exiting
//void _declspec(naked) FunctionLeaveNaked(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO func, COR_PRF_FUNCTION_ARGUMENT_RANGE *retvalRange)
//{
//    __asm
//    {
//        push    ebp                 // Create a standard frame
//        mov     ebp,esp
//        pushad                      // Preserve all registers
//
//        mov     eax,[ebp+0x14]      // argumentInfo
//        push    eax
//        mov     ecx,[ebp+0x10]      // func
//        push    ecx
//        mov     edx,[ebp+0x0C]      // clientData
//        push    edx
//        mov     eax,[ebp+0x08]      // functionID
//        push    eax
//        call    FunctionLeaveGlobal
//
//        popad                       // Restore all registers
//        pop     ebp                 // Restore EBP
//        ret     16
//    }
//}
//
//// this function simply forwards the FunctionLeave call the global profiler object
//void __stdcall FunctionTailcallGlobal(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo)
//{
//    if (g_pICorProfilerCallback != NULL)
//        g_pICorProfilerCallback->Tailcall(functionID,clientData,frameInfo);
//}
//
//// this function is called by the CLR when a tailcall occurs.  A tailcall occurs when the 
//// last action of a method is a call to another method.
//void _declspec(naked) FunctionTailcallNaked(FunctionID functionID, UINT_PTR clientData, COR_PRF_FRAME_INFO func)
//{
//    __asm
//    {
//        push    ebp                 // Create a standard frame
//        mov     ebp,esp
//        pushad                      // Preserve all registers
//
//        mov     eax,[ebp+0x14]      // argumentInfo
//        push    eax
//        mov     ecx,[ebp+0x10]      // func
//        push    ecx
//        mov     edx,[ebp+0x0C]      // clientData
//        push    edx
//        mov     eax,[ebp+0x08]      // functionID
//        push    eax
//        call    FunctionTailcallGlobal
//
//        popad                       // Restore all registers
//        pop     ebp                 // Restore EBP
//        ret     16
//    }
//}

// ----  MAPPING FUNCTIONS ------------------

// this function is called by the CLR when a function has been mapped to an ID
UINT_PTR CProfiler::FunctionMapper(FunctionID functionID, BOOL *pbHookFunction)
{
	// make sure the global reference to our profiler is valid.  Forward this
	// call to our profiler object
    if (g_pICorProfilerCallback != NULL)
        g_pICorProfilerCallback->MapFunction(functionID);

	if (pbHookFunction != NULL) {
		// hook all functions.
		*pbHookFunction = TRUE;
	}

	// we must return the function ID passed as a parameter
	return (UINT_PTR)functionID;
}

static long g_functionCount;

// the static function called by .Net when a function has been mapped to an ID
void CProfiler::MapFunction(FunctionID functionID)
{
	g_functionCount++;
}

HRESULT CProfiler::GetFunctionName(FunctionID functionID, WCHAR* buffer, int bufferSize)
{
	// see if this function is in the map
	
		// declared in this block so they are not created if the function is found
		
	// get the method name
	HRESULT hr = GetFullMethodName(functionID, buffer, bufferSize); 
	if (FAILED(hr))
	{
		// if we couldn't get the function name, then log it
		LogString("Unable to find the name for function %i\r\n", functionID);
	}
    return hr;
}

// ----  CALLBACK HANDLER FUNCTIONS ------------------

// our real handler for FunctionEnter notification
void CProfiler::Enter(FunctionID functionID) // , UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_INFO *argumentInfo)
{	    
    FunctionID id = functionID;
    if (id != 0) 
    {
	    static long callCount = 0;

	    if (callCount == 0) 
	    {		
			if (getenv("COR_PROFILER_ATTACHING") == NULL) 
			{		    
				MessageBox(NULL, L"You can now attach the profiler client.\r\nThe process being profiled will start up slowly so please be patient.", L"Profiler Ready", MB_ICONINFORMATION);
			}
	    }
	    callCount++;

        if (_sharedMemory != NULL) {
	        _sharedMemory->WriteRecord(id, _currentTime);
        }
    }

	m_callStackSize++;
}

// our real handler for FunctionLeave notification
void CProfiler::Leave(FunctionID functionID) // , UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE *argumentRange)
{    
    if (_sharedMemory != NULL) {
        FunctionID leaveId = (FunctionID)LeaveCallId;
		_sharedMemory->WriteRecord(leaveId, _currentTime);
    }

	// decrement the call stack size
	if (m_callStackSize > 0)
		m_callStackSize--;
}

// our real handler for the FunctionTailcall notification
void CProfiler::Tailcall(FunctionID functionID) // , UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo)
{
    if (_sharedMemory != NULL) {
        FunctionID id = (FunctionID)TailCallId;
		_sharedMemory->WriteRecord(id, _currentTime);
    }

	// decrement the call stack size
	if (m_callStackSize > 0)
		m_callStackSize--;
}

void CALLBACK timer_tick(PTP_CALLBACK_INSTANCE i, void* context, PTP_TIMER timer)
{
	CProfiler* profiler = (CProfiler*)context;
	profiler->OnTick();
}

// ----  ICorProfilerCallback IMPLEMENTATION ------------------

// called when the profiling object is created by the CLR
STDMETHODIMP CProfiler::Initialize(IUnknown *pICorProfilerInfoUnk)
{	
	//MessageBox(NULL, L"The profiler is loaded, so you can debug it now", L"Profiler Debug Prompt", MB_ICONINFORMATION);
	
	// set up our global access pointer
	g_pICorProfilerCallback = this;

	// log that we are initializing
	LogString("Initializing...\r\n\r\n");

	// get the ICorProfilerInfo interface
    HRESULT hr = pICorProfilerInfoUnk->QueryInterface(IID_ICorProfilerInfo, (LPVOID*)&m_pICorProfilerInfo);
    if (FAILED(hr))
        return E_FAIL;

	// determine if this object implements ICorProfilerInfo2
    hr = pICorProfilerInfoUnk->QueryInterface(IID_ICorProfilerInfo2, (LPVOID*)&m_pICorProfilerInfo2);
    if (FAILED(hr))
	{		
		m_pICorProfilerInfo2.Detach();
		return E_FAIL; // runtime is too old..
	}
	
	// determine if this object implements ICorProfilerInfo3
    hr = pICorProfilerInfoUnk->QueryInterface(IID_ICorProfilerInfo3, (LPVOID*)&m_pICorProfilerInfo3);
    if (FAILED(hr))
	{
		// we still want to work if this call fails, might be .NET 2.0
		m_pICorProfilerInfo3.Detach();
	}
	

	// Indicate which events we're interested in.
	hr = SetEventMask();
    if (FAILED(hr))
        LogString("Error setting the event mask\r\n\r\n");

	
    if (m_pICorProfilerInfo3.p == NULL)
    {

        hr = m_pICorProfilerInfo2->SetEnterLeaveFunctionHooks2( (FunctionEnter2 *)EnterNaked2,
                                                            (FunctionLeave2 *)LeaveNaked2,
                                                            (FunctionTailcall2 *)TailcallNaked2 );
    }
    else
    {
        hr = m_pICorProfilerInfo3->SetEnterLeaveFunctionHooks3( (FunctionEnter3 *)EnterNaked3,
                                                            (FunctionLeave3 *)LeaveNaked3,
                                                            (FunctionTailcall3 *)TailcallNaked3 );
    }
	
	if (SUCCEEDED(hr)) {
		hr = m_pICorProfilerInfo->SetFunctionIDMapper((FunctionIDMapper*)&FunctionMapper);
	}

	// start the thread for handling named pipe
    HANDLE hThread = CreateThread(NULL, 0, &HandlerThread, (PVOID)this, 0, NULL);
    CloseHandle(hThread);
	
	_currentTime = GetTickCount();
	FILETIME relativeTime = { (DWORD)-1000, 0 };
	_timer = CreateThreadpoolTimer(timer_tick, this, NULL);

	// Get a 1 millisecond callback so we can increment our global time clock efficiently (without having to do it in profiler callbacks).
	SetThreadpoolTimer(_timer, &relativeTime, 1, 0);

	// report our success or failure to the log file
    if (FAILED(hr))
        LogString("Error setting the enter, leave and tailcall hooks\r\n\r\n");
	else
		LogString("Successfully initialized profiling\r\n\r\n" );


    return S_OK;
}

void CProfiler::OnTick() 
{
	_currentTime = GetTickCount();
}

// called when the profiler is being terminated by the CLR
STDMETHODIMP CProfiler::Shutdown()
{
	// log the we're shutting down
	LogString("\r\n\r\nShutdown... writing function list\r\n\r\n" );

	// tear down our global access pointers
	g_pICorProfilerCallback = NULL;

    m_terminated = true;

	CloseThreadpoolTimer(_timer);
	_timer = NULL;

    return S_OK;
}

// Creates the log file.  It uses the LOG_FILENAME environment variable if it 
// exists, otherwise it creates the file "ICorProfilerCallback Log.log" in the 
// executing directory.  This function doesn't report success or not because 
// LogString always checks for a valid file handle whenever the file is written
// to.
void CProfiler::CreateLogFile()
{
	// get the log filename
	memset(m_logFileName, 0, sizeof(m_logFileName));
	// get the log file name (stored in an environment var)
	if (GetEnvironmentVariable(_T("LOG_FILENAME"), m_logFileName, _MAX_PATH) == 0)
	{
		// just write to "ICorProfilerCallback Log.log"
		_tcscpy(m_logFileName, _T("ICorProfilerCallback Log.log"));
	}
	// delete any existing log file
	::DeleteFile(m_logFileName);
	// set up log file in the current working directory
	m_hLogFile = CreateFile(m_logFileName, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
}

// Closes the log file
void CProfiler::CloseLogFile()
{
	// close the log file
	if (m_hLogFile != INVALID_HANDLE_VALUE)
	{
		CloseHandle(m_hLogFile);
		m_hLogFile = INVALID_HANDLE_VALUE;
	}
}

// Writes a string to the log file.  Uses the same calling convention as printf.
void CProfiler::LogString(char *pszFmtString, ...)
{
	CHAR szBuffer[4096]; DWORD dwWritten = 0;

	if(m_hLogFile != INVALID_HANDLE_VALUE)
	{
		va_list args;
		va_start( args, pszFmtString );
		vsprintf(szBuffer, pszFmtString, args );
		va_end( args );

		// write out to the file if the file is open
		WriteFile(m_hLogFile, szBuffer, (DWORD)strlen(szBuffer), &dwWritten, NULL);
	}
}

///<summary>
// We are monitoring events that are interesting for determining
// the hot spots of a managed CLR program (profilee). This includes
// thread related events, function enter/leave events, exception 
// related events, and unmanaged/managed transition events. Note 
// that we disable inlining. Although this does indeed affect the 
// execution time, it provides better accuracy for determining
// hot spots.
//
// If the system does not support high precision counters, then
// do not profile anything. This is determined in the constructor.
///</summary>
HRESULT CProfiler::SetEventMask()
{
	//COR_PRF_MONITOR_NONE	= 0,
	//COR_PRF_MONITOR_FUNCTION_UNLOADS	= 0x1,
	//COR_PRF_MONITOR_CLASS_LOADS	= 0x2,
	//COR_PRF_MONITOR_MODULE_LOADS	= 0x4,
	//COR_PRF_MONITOR_ASSEMBLY_LOADS	= 0x8,
	//COR_PRF_MONITOR_APPDOMAIN_LOADS	= 0x10,
	//COR_PRF_MONITOR_JIT_COMPILATION	= 0x20,
	//COR_PRF_MONITOR_EXCEPTIONS	= 0x40,
	//COR_PRF_MONITOR_GC	= 0x80,
	//COR_PRF_MONITOR_OBJECT_ALLOCATED	= 0x100,
	//COR_PRF_MONITOR_THREADS	= 0x200,
	//COR_PRF_MONITOR_REMOTING	= 0x400,
	//COR_PRF_MONITOR_CODE_TRANSITIONS	= 0x800,
	//COR_PRF_MONITOR_ENTERLEAVE	= 0x1000,
	//COR_PRF_MONITOR_CCW	= 0x2000,
	//COR_PRF_MONITOR_REMOTING_COOKIE	= 0x4000 | COR_PRF_MONITOR_REMOTING,
	//COR_PRF_MONITOR_REMOTING_ASYNC	= 0x8000 | COR_PRF_MONITOR_REMOTING,
	//COR_PRF_MONITOR_SUSPENDS	= 0x10000,
	//COR_PRF_MONITOR_CACHE_SEARCHES	= 0x20000,
	//COR_PRF_MONITOR_CLR_EXCEPTIONS	= 0x1000000,
	//COR_PRF_MONITOR_ALL	= 0x107ffff,
	//COR_PRF_ENABLE_REJIT	= 0x40000,
	//COR_PRF_ENABLE_INPROC_DEBUGGING	= 0x80000,
	//COR_PRF_ENABLE_JIT_MAPS	= 0x100000,
	//COR_PRF_DISABLE_INLINING	= 0x200000,
	//COR_PRF_DISABLE_OPTIMIZATIONS	= 0x400000,
	//COR_PRF_ENABLE_OBJECT_ALLOCATED	= 0x800000,
	// New in VS2005
	//	COR_PRF_ENABLE_FUNCTION_ARGS	= 0x2000000,
	//	COR_PRF_ENABLE_FUNCTION_RETVAL	= 0x4000000,
	//  COR_PRF_ENABLE_FRAME_INFO	= 0x8000000,
	//  COR_PRF_ENABLE_STACK_SNAPSHOT	= 0x10000000,
	//  COR_PRF_USE_PROFILE_IMAGES	= 0x20000000,
	// End New in VS2005
	//COR_PRF_ALL	= 0x3fffffff,
	//COR_PRF_MONITOR_IMMUTABLE	= COR_PRF_MONITOR_CODE_TRANSITIONS | COR_PRF_MONITOR_REMOTING | COR_PRF_MONITOR_REMOTING_COOKIE | COR_PRF_MONITOR_REMOTING_ASYNC | COR_PRF_MONITOR_GC | COR_PRF_ENABLE_REJIT | COR_PRF_ENABLE_INPROC_DEBUGGING | COR_PRF_ENABLE_JIT_MAPS | COR_PRF_DISABLE_OPTIMIZATIONS | COR_PRF_DISABLE_INLINING | COR_PRF_ENABLE_OBJECT_ALLOCATED | COR_PRF_ENABLE_FUNCTION_ARGS | COR_PRF_ENABLE_FUNCTION_RETVAL | COR_PRF_ENABLE_FRAME_INFO | COR_PRF_ENABLE_STACK_SNAPSHOT | COR_PRF_USE_PROFILE_IMAGES

	// set the event mask 
	DWORD eventMask = (DWORD)(COR_PRF_MONITOR_ENTERLEAVE);
	return m_pICorProfilerInfo->SetEventMask(eventMask);
}



// creates the fully scoped name of the method in the provided buffer
HRESULT CProfiler::GetFullMethodName(FunctionID functionID, LPWSTR wszMethod, int cMethod)
{
	IMetaDataImport* pIMetaDataImport = 0;
	HRESULT hr = S_OK;
	mdToken funcToken = 0;
	WCHAR szFunction[NAME_BUFFER_SIZE];
	WCHAR szClass[NAME_BUFFER_SIZE];
			
	// get the token for the function which we will use to get its name	
	hr = m_pICorProfilerInfo->GetTokenAndMetaDataFromFunction(functionID, IID_IMetaDataImport, (LPUNKNOWN *) &pIMetaDataImport, &funcToken);
	if(SUCCEEDED(hr))
	{
		mdTypeDef classTypeDef;
		ULONG cchFunction;
		ULONG cchClass;

		// retrieve the function properties based on the token
		hr = pIMetaDataImport->GetMethodProps(funcToken, &classTypeDef, szFunction, NAME_BUFFER_SIZE, &cchFunction, 0, 0, 0, 0, 0);
		if (SUCCEEDED(hr))
		{
			// get the function name
			hr = pIMetaDataImport->GetTypeDefProps(classTypeDef, szClass, NAME_BUFFER_SIZE, &cchClass, 0, 0);
			if (!SUCCEEDED(hr))
			{
				// mark this as a weird global object then.
				int bufChars = (NAME_BUFFER_SIZE - 2) / sizeof(TCHAR);
				_snwprintf_s(szClass, bufChars, bufChars, L"::");
			}

			// create the fully qualified name
			int maxChars = (cMethod - 2) / sizeof(TCHAR); // give room for null terminator.
			_snwprintf_s(wszMethod, maxChars, maxChars, L"%s.%s", szClass, szFunction);                
		}
		else {
			MessageBox(NULL, L"GetMethodProps failed", L"Profiler Debug Prompt", MB_ICONINFORMATION);
		}
		// release our reference to the metadata
		pIMetaDataImport->Release();
	}


	return hr;
}


HRESULT CProfiler::RequestDetach()
{
    HRESULT hr = S_OK;
    if (m_pICorProfilerInfo3 != NULL)
    {
        hr = m_pICorProfilerInfo3->RequestProfilerDetach(5000);        
    }

    CloseSharedMemory();
    return hr;
}

HRESULT CProfiler::ClientDetached()
{
    CloseSharedMemory();
    return S_OK;
}


HRESULT CProfiler::InitSharedMemory(TCHAR* name, int size)
{
    _sharedMemory = new CSharedMemory(name, size);
    return S_OK;
}

void CProfiler::CloseSharedMemory()
{
    if (_sharedMemory != NULL) 
    {
        // must be thread safe.
        CSharedMemory* temp = _sharedMemory;
        _sharedMemory = NULL;
        delete temp;
    }
}

long CProfiler::GetCallCount()
{
    if (_sharedMemory != NULL) 
    {
        return (long)(_sharedMemory->GetPosition() / RecordSize);
    }
    return 0;
}

long CProfiler::GetFunctionCount()
{
    return g_functionCount;
}

long CProfiler::GetVersion() 
{
    if (_sharedMemory != NULL) 
    {
        return _sharedMemory->GetVersion();
    }
    return 0;
}

HRESULT CProfiler::DeleteAll()
{
    if (_sharedMemory != NULL) 
    {
        _sharedMemory->Reset();
    }
    return S_OK;
}

DWORD WINAPI CProfiler::HandlerThread(PVOID v)
{
    CProfiler * profiler = (CProfiler *) v;
    profiler->PipeRequestHandler();
    return 1;
}

HRESULT CProfiler::PipeRequestHandler()
{
    HRESULT hr = S_OK;
    while (!m_terminated)
    {
        _pipeServer.Run();
		Sleep(1); // so we don't spin our wheels too much here...
    }
    return hr;
}
