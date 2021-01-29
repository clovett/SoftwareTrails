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

#pragma once
#include "resource.h"
#include "DotNetProfiler.h"
#include "resource.h"
#include "PipeServer.h"
#include <unordered_map>
#include "SharedMemory.h"

#if defined(_WIN32_WCE) && !defined(_CE_DCOM) && !defined(_CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA)
#error "Single-threaded COM objects are not properly supported on Windows CE platform, such as the Windows Mobile platforms that do not include full DCOM support. Define _CE_ALLOW_SINGLE_THREADED_OBJECTS_IN_MTA to force ATL to support creating single-thread COM object's and allow use of it's single-threaded COM object implementations. The threading model in your rgs file was set to 'Free' as that is the only threading model supported in non DCOM Windows CE platforms."
#endif

#define ASSERT_HR(x) _ASSERT(SUCCEEDED(x))
#define NAME_BUFFER_SIZE 1024

// CProfiler
class ATL_NO_VTABLE CProfiler :
	public CComObjectRootEx<CComSingleThreadModel>,
	public CComCoClass<CProfiler, &CLSID_ClrProfiler>,
	public ICorProfilerCallback3
{
public:
	CProfiler();

	DECLARE_REGISTRY_RESOURCEID(IDR_PROFILER)
	BEGIN_COM_MAP(CProfiler)
		COM_INTERFACE_ENTRY(ICorProfilerCallback)
		COM_INTERFACE_ENTRY(ICorProfilerCallback2)
		COM_INTERFACE_ENTRY(ICorProfilerCallback3)
	END_COM_MAP()
	DECLARE_PROTECT_FINAL_CONSTRUCT()

	// overridden implementations of FinalConstruct and FinalRelease
	HRESULT FinalConstruct();
	void FinalRelease();
	
	// ICorProfilerCallback interface implementation
    // STARTUP/SHUTDOWN EVENTS
    STDMETHOD(Initialize)(IUnknown *pICorProfilerInfoUnk);
    STDMETHOD(Shutdown)();

 	// APPLICATION DOMAIN EVENTS
   	STDMETHOD(AppDomainCreationStarted)(AppDomainID appDomainID);
	STDMETHOD(AppDomainCreationFinished)(AppDomainID appDomainID, HRESULT hrStatus);
    STDMETHOD(AppDomainShutdownStarted)(AppDomainID appDomainID);
	STDMETHOD(AppDomainShutdownFinished)(AppDomainID appDomainID, HRESULT hrStatus);
 	// ASSEMBLY EVENTS
   	STDMETHOD(AssemblyLoadStarted)(AssemblyID assemblyID);
	STDMETHOD(AssemblyLoadFinished)(AssemblyID assemblyID, HRESULT hrStatus);
    STDMETHOD(AssemblyUnloadStarted)(AssemblyID assemblyID);
	STDMETHOD(AssemblyUnloadFinished)(AssemblyID assemblyID, HRESULT hrStatus);
 	// MODULE EVENTS
   	STDMETHOD(ModuleLoadStarted)(ModuleID moduleID);
	STDMETHOD(ModuleLoadFinished)(ModuleID moduleID, HRESULT hrStatus);
    STDMETHOD(ModuleUnloadStarted)(ModuleID moduleID);
	STDMETHOD(ModuleUnloadFinished)(ModuleID moduleID, HRESULT hrStatus);
	STDMETHOD(ModuleAttachedToAssembly)(ModuleID moduleID, AssemblyID assemblyID);
    // CLASS EVENTS
    STDMETHOD(ClassLoadStarted)(ClassID classID);
    STDMETHOD(ClassLoadFinished)(ClassID classID, HRESULT hrStatus);
 	STDMETHOD(ClassUnloadStarted)(ClassID classID);
	STDMETHOD(ClassUnloadFinished)(ClassID classID, HRESULT hrStatus);
	STDMETHOD(FunctionUnloadStarted)(FunctionID functionID);
    // JIT EVENTS
    STDMETHOD(JITCompilationStarted)(FunctionID functionID, BOOL fIsSafeToBlock);
    STDMETHOD(JITCompilationFinished)(FunctionID functionID, HRESULT hrStatus, BOOL fIsSafeToBlock);
    STDMETHOD(JITCachedFunctionSearchStarted)(FunctionID functionID, BOOL *pbUseCachedFunction);
	STDMETHOD(JITCachedFunctionSearchFinished)(FunctionID functionID, COR_PRF_JIT_CACHE result);
    STDMETHOD(JITFunctionPitched)(FunctionID functionID);
    STDMETHOD(JITInlining)(FunctionID callerID, FunctionID calleeID, BOOL *pfShouldInline);
    // THREAD EVENTS
    STDMETHOD(ThreadCreated)(ThreadID threadID);
    STDMETHOD(ThreadDestroyed)(ThreadID threadID);
    STDMETHOD(ThreadAssignedToOSThread)(ThreadID managedThreadID, DWORD osThreadID);
    // REMOTING EVENTS
    // Client-side events
    STDMETHOD(RemotingClientInvocationStarted)();
    STDMETHOD(RemotingClientSendingMessage)(GUID *pCookie, BOOL fIsAsync);
    STDMETHOD(RemotingClientReceivingReply)(GUID *pCookie, BOOL fIsAsync);
    STDMETHOD(RemotingClientInvocationFinished)();
    // Server-side events
    STDMETHOD(RemotingServerReceivingMessage)(GUID *pCookie, BOOL fIsAsync);
    STDMETHOD(RemotingServerInvocationStarted)();
    STDMETHOD(RemotingServerInvocationReturned)();
    STDMETHOD(RemotingServerSendingReply)(GUID *pCookie, BOOL fIsAsync);
    // CONTEXT EVENTS
	STDMETHOD(UnmanagedToManagedTransition)(FunctionID functionID, COR_PRF_TRANSITION_REASON reason);
    STDMETHOD(ManagedToUnmanagedTransition)(FunctionID functionID, COR_PRF_TRANSITION_REASON reason);
    // SUSPENSION EVENTS
    STDMETHOD(RuntimeSuspendStarted)(COR_PRF_SUSPEND_REASON suspendReason);
    STDMETHOD(RuntimeSuspendFinished)();
    STDMETHOD(RuntimeSuspendAborted)();
    STDMETHOD(RuntimeResumeStarted)();
    STDMETHOD(RuntimeResumeFinished)();
    STDMETHOD(RuntimeThreadSuspended)(ThreadID threadid);
    STDMETHOD(RuntimeThreadResumed)(ThreadID threadid);
    // GC EVENTS
    STDMETHOD(MovedReferences)(ULONG cmovedObjectIDRanges, ObjectID oldObjectIDRangeStart[], ObjectID newObjectIDRangeStart[], ULONG cObjectIDRangeLength[]);
    STDMETHOD(ObjectAllocated)(ObjectID objectID, ClassID classID);
    STDMETHOD(ObjectsAllocatedByClass)(ULONG classCount, ClassID classIDs[], ULONG objects[]);
    STDMETHOD(ObjectReferences)(ObjectID objectID, ClassID classID, ULONG cObjectRefs, ObjectID objectRefIDs[]);
    STDMETHOD(RootReferences)(ULONG cRootRefs, ObjectID rootRefIDs[]);
    // EXCEPTION EVENTS
    // Exception creation
    STDMETHOD(ExceptionThrown)(ObjectID thrownObjectID);
    // Search phase
    STDMETHOD(ExceptionSearchFunctionEnter)(FunctionID functionID);
    STDMETHOD(ExceptionSearchFunctionLeave)();
    STDMETHOD(ExceptionSearchFilterEnter)(FunctionID functionID);
    STDMETHOD(ExceptionSearchFilterLeave)();
    STDMETHOD(ExceptionSearchCatcherFound)(FunctionID functionID);
    STDMETHOD(ExceptionCLRCatcherFound)();
    STDMETHOD(ExceptionCLRCatcherExecute)();
    STDMETHOD(ExceptionOSHandlerEnter)(FunctionID functionID);
    STDMETHOD(ExceptionOSHandlerLeave)(FunctionID functionID);
    // Unwind phase
    STDMETHOD(ExceptionUnwindFunctionEnter)(FunctionID functionID);
    STDMETHOD(ExceptionUnwindFunctionLeave)();
    STDMETHOD(ExceptionUnwindFinallyEnter)(FunctionID functionID);
    STDMETHOD(ExceptionUnwindFinallyLeave)();
    STDMETHOD(ExceptionCatcherEnter)(FunctionID functionID, ObjectID objectID);
    STDMETHOD(ExceptionCatcherLeave)();
	// COM CLASSIC VTable
    STDMETHOD(COMClassicVTableCreated)(ClassID wrappedClassID, REFGUID implementedIID, void *pVTable, ULONG cSlots);
    STDMETHOD(COMClassicVTableDestroyed)(ClassID wrappedClassID, REFGUID implementedIID, void *pVTable);
	// End of ICorProfilerCallback interface implementation

	// ICorProfilerCallback2 interface implementation
	STDMETHOD(ThreadNameChanged)(ThreadID threadId, ULONG cchName, WCHAR name[]);
	STDMETHOD(GarbageCollectionStarted)(int cGenerations, BOOL generationCollected[], COR_PRF_GC_REASON reason);
    STDMETHOD(SurvivingReferences)(ULONG cSurvivingObjectIDRanges, ObjectID objectIDRangeStart[], ULONG cObjectIDRangeLength[]);
    STDMETHOD(GarbageCollectionFinished)();
    STDMETHOD(FinalizeableObjectQueued)(DWORD finalizerFlags, ObjectID objectID);
    STDMETHOD(RootReferences2)(ULONG cRootRefs, ObjectID rootRefIds[], COR_PRF_GC_ROOT_KIND rootKinds[], COR_PRF_GC_ROOT_FLAGS rootFlags[], UINT_PTR rootIds[]);
    STDMETHOD(HandleCreated)(GCHandleID handleId, ObjectID initialObjectId);
    STDMETHOD(HandleDestroyed)(GCHandleID handleId);
	// End of ICorProfilerCallback2 interface implementation

	// ICorProfilerCallback3
	STDMETHOD(InitializeForAttach)(IUnknown * pCorProfilerInfoUnk, void * pvClientData, UINT cbClientData);
    STDMETHOD(ProfilerAttachComplete)();
    STDMETHOD(ProfilerDetachSucceeded)();
	
	// callback functions
	void Enter(FunctionID functionID); //, UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_INFO *argumentInfo);
	void Leave(FunctionID functionID); // , UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo, COR_PRF_FUNCTION_ARGUMENT_RANGE *argumentRange);
	void Tailcall(FunctionID functionID); // , UINT_PTR clientData, COR_PRF_FRAME_INFO frameInfo);

	// mapping functions
	static UINT_PTR _stdcall FunctionMapper(FunctionID functionId, BOOL *pbHookFunction);
	void MapFunction(FunctionID);

	// logging function
    void LogString(char* pszFmtString, ... );
	
    HRESULT RequestDetach();
    HRESULT ClientDetached();

    HRESULT InitSharedMemory(TCHAR* name, int size);
    HRESULT GetFunctionName(FunctionID functionID, WCHAR* buffer, int bufferSize);
    long GetCallCount();
    long GetFunctionCount();
    long GetVersion();
    HRESULT DeleteAll();

	void OnTick();

private:
    // container for ICorProfilerInfo reference
	CComQIPtr<ICorProfilerInfo> m_pICorProfilerInfo;
    // container for ICorProfilerInfo2 reference
	CComQIPtr<ICorProfilerInfo2> m_pICorProfilerInfo2;
    // container for ICorProfilerInfo3 reference
	CComQIPtr<ICorProfilerInfo3> m_pICorProfilerInfo3;

	// the number of levels deep we are in the call stack
	int m_callStackSize;
	// handle and filename of log file
	HANDLE m_hLogFile;
	TCHAR m_logFileName[_MAX_PATH]; 

	// gets the full method name given a function ID
	HRESULT GetFullMethodName(FunctionID functionId, LPWSTR wszMethod, int cMethod );
	// function to set up our event mask
	HRESULT SetEventMask();
	// creates the log file
	void CreateLogFile();
	// closes the log file ;)
	void CloseLogFile();

	// thread for handling pipeserver
	static DWORD WINAPI HandlerThread(PVOID v);
	
	HRESULT PipeRequestHandler();
	
    bool m_terminated;
	PipeServer _pipeServer;
	CSharedMemory* _sharedMemory;

    void CloseSharedMemory();

	PTP_TIMER _timer;
	DWORD _currentTime;
};

OBJECT_ENTRY_AUTO(__uuidof(ClrProfiler), CProfiler)
